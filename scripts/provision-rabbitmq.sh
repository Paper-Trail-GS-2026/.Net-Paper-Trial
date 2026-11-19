#!/usr/bin/env bash
set -euo pipefail
export MSYS_NO_PATHCONV=1

: "${AZURE_BROKER_SSH_PUBLIC_KEY:?Configure the SSH public key for the broker VM.}"
: "${AZURE_BROKER_SUBSCRIPTION_ID:?Configure the broker subscription.}"
app_subscription="${AZURE_SUBSCRIPTION_ID:-44b8b12c-4907-4842-9af8-2ea45fc8d44a}"
app_group="${AZURE_RESOURCE_GROUP:-rm561584-papertrail-gs}"
resource_group="${AZURE_BROKER_RESOURCE_GROUP:-rm562194-papertrail-gs}"
location="${AZURE_LOCATION:-mexicocentral}"
vm_size="${AZURE_BROKER_VM_SIZE:-Standard_B2ls_v2}"
zone="${AZURE_BROKER_ZONE:-2}"
app_vnet="vnet-papertrail-rm561584"
vnet="vnet-papertrail-rabbitmq"
nsg="nsg-papertrail-rabbitmq"
vm="vm-papertrail-rabbitmq"

az() {
  local attempt
  for attempt in 1 2 3; do
    if command az "$@"; then return 0; fi
    printf 'Azure command failed. Attempt %s/3.\n' "$attempt" >&2
    sleep 3
  done
  return 1
}

az group create --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --name "$resource_group" \
  --location "$location" --tags project=PaperTrail --output none
if ! az network vnet show --subscription "$app_subscription" --resource-group "$app_group" --name "$app_vnet" --output none 2>/dev/null; then
  az network vnet create --subscription "$app_subscription" --resource-group "$app_group" --name "$app_vnet" \
    --location "$location" --address-prefixes 10.33.0.0/16 --output none
fi
az network vnet subnet create --subscription "$app_subscription" --resource-group "$app_group" --vnet-name "$app_vnet" \
  --name webapps --address-prefixes 10.33.2.0/26 --delegations Microsoft.Web/serverFarms --output none
if ! az network vnet show --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --resource-group "$resource_group" --name "$vnet" --output none 2>/dev/null; then
  az network vnet create --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --resource-group "$resource_group" --name "$vnet" \
    --location "$location" --address-prefixes 10.34.0.0/16 --output none
fi
az network vnet subnet create --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --resource-group "$resource_group" \
  --vnet-name "$vnet" --name broker --address-prefixes 10.34.1.0/24 --output none
az network nsg create --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --resource-group "$resource_group" \
  --name "$nsg" --location "$location" --output none
az network nsg rule create --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --resource-group "$resource_group" \
  --nsg-name "$nsg" --name AllowWebAppsAmqp --priority 100 --direction Inbound --access Allow --protocol Tcp \
  --source-address-prefixes 10.33.2.0/26 --source-port-ranges '*' \
  --destination-address-prefixes 10.34.1.4 --destination-port-ranges 5672 --output none
az network nsg rule create --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --resource-group "$resource_group" \
  --nsg-name "$nsg" --name DenyOtherInbound --priority 200 --direction Inbound --access Deny --protocol '*' \
  --source-address-prefixes '*' --source-port-ranges '*' --destination-address-prefixes '*' --destination-port-ranges '*' --output none

# Public IP is used for outbound downloads. Inbound access is blocked by the NSG.
az network public-ip create --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --resource-group "$resource_group" \
  --name pip-papertrail-rabbitmq --location "$location" --sku Standard --allocation-method Static --zone "$zone" --output none
az network nic create --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --resource-group "$resource_group" \
  --name nic-papertrail-rabbitmq --location "$location" --vnet-name "$vnet" --subnet broker \
  --private-ip-address 10.34.1.4 --network-security-group "$nsg" --public-ip-address pip-papertrail-rabbitmq --output none
if ! az vm show --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --resource-group "$resource_group" --name "$vm" --output none 2>/dev/null; then
  az vm create --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --resource-group "$resource_group" --name "$vm" \
    --location "$location" --zone "$zone" --size "$vm_size" \
    --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest --security-type TrustedLaunch \
    --admin-username papertrail --authentication-type ssh --ssh-key-values "$AZURE_BROKER_SSH_PUBLIC_KEY" \
    --nics nic-papertrail-rabbitmq --storage-sku StandardSSD_LRS --output none
fi

app_vnet_id="/subscriptions/$app_subscription/resourceGroups/$app_group/providers/Microsoft.Network/virtualNetworks/$app_vnet"
broker_vnet_id="/subscriptions/$AZURE_BROKER_SUBSCRIPTION_ID/resourceGroups/$resource_group/providers/Microsoft.Network/virtualNetworks/$vnet"
az network vnet peering create --subscription "$app_subscription" --resource-group "$app_group" \
  --vnet-name "$app_vnet" --name papertrail-to-broker --remote-vnet "$broker_vnet_id" --allow-vnet-access --output none
az network vnet peering create --subscription "$AZURE_BROKER_SUBSCRIPTION_ID" --resource-group "$resource_group" \
  --vnet-name "$vnet" --name broker-to-papertrail --remote-vnet "$app_vnet_id" --allow-vnet-access --output none
for app in papertrail-java-rm561584 papertrail-dotnet-rm561584; do
  site_url="https://management.azure.com/subscriptions/$app_subscription/resourceGroups/$app_group/providers/Microsoft.Web/sites/$app/networkConfig/virtualNetwork?api-version=2024-11-01"
  az rest --method put --url "$site_url" \
    --body "{\"properties\":{\"subnetResourceId\":\"$app_vnet_id/subnets/webapps\",\"swiftSupported\":true}}" --output none
 done
printf 'Broker VM: %s\nPrivate AMQP endpoint: 10.34.1.4:5672\n' "$vm"
