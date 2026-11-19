#!/usr/bin/env bash
set -euo pipefail
export MSYS_NO_PATHCONV=1

az() {
    local attempt
    for attempt in 1 2 3; do
        if command az "$@"; then return 0; fi
        printf 'Azure command failed. Attempt %s/3.\n' "$attempt" >&2
        sleep 2
    done
    return 1
}

subscription_id="${AZURE_SUBSCRIPTION_ID:-$(az account show --query id -o tsv | tr -d '\r')}"
location="${AZURE_LOCATION:-mexicocentral}"
resource_group="${AZURE_RESOURCE_GROUP:-rm561584-papertrail-gs}"
plan_name="${AZURE_PLAN_NAME:-asp-papertrail-rm561584}"
sql_server="${AZURE_SQL_SERVER:-papertrail-sql-rm561584}"
java_app="${AZURE_JAVA_APP:-papertrail-java-rm561584}"
dotnet_app="${AZURE_DOTNET_APP:-papertrail-dotnet-rm561584}"
pipeline_identity="${AZURE_PIPELINE_IDENTITY:-id-papertrail-pipelines-rm561584}"

if [[ -n "${PIPELINE_FEDERATION_ISSUER:-}" || -n "${PIPELINE_FEDERATION_SUBJECT:-}" ]]; then
    : "${PIPELINE_FEDERATION_ISSUER:?Configure the service connection issuer.}"
    : "${PIPELINE_FEDERATION_SUBJECT:?Configure the service connection subject.}"
fi

admin_id="${AZURE_ADMIN_OBJECT_ID:-$(az ad signed-in-user show --query id -o tsv | tr -d '\r')}"
admin_name="${AZURE_ADMIN_NAME:-$(az account show --query user.name -o tsv | tr -d '\r')}"

printf 'Creating resource group...\n'
az group create --subscription "$subscription_id" --name "$resource_group" --location "$location" --tags project=PaperTrail owner=rm561584 --output none
printf 'Creating SQL server...\n'
az sql server create --subscription "$subscription_id" --resource-group "$resource_group" --name "$sql_server" --location "$location" --enable-ad-only-auth --external-admin-principal-type User --external-admin-name "$admin_name" --external-admin-sid "$admin_id" --minimal-tls-version 1.2 --output none
printf 'Creating SQL database...\n'
az sql db create --subscription "$subscription_id" --resource-group "$resource_group" --server "$sql_server" --name papertrail --edition Basic --service-objective Basic --max-size 2GB --backup-storage-redundancy Local --output none

printf 'Creating App Service plan...\n'
az appservice plan create --subscription "$subscription_id" --resource-group "$resource_group" --name "$plan_name" --location "$location" --is-linux --sku B2 --number-of-workers 1 --output none

for app in "$java_app" "$dotnet_app"; do
    printf 'Creating Web App: %s\n' "$app"
    if [[ "$app" == "$java_app" ]]; then
        runtime="JAVA|17-java17"
    else
        runtime="DOTNETCORE|10.0"
    fi
    site_url="https://management.azure.com/subscriptions/$subscription_id/resourceGroups/$resource_group/providers/Microsoft.Web/sites/$app?api-version=2024-11-01"
    site_body="$(cat <<JSON
{
  "location": "$location",
  "kind": "app,linux",
  "identity": {"type": "SystemAssigned"},
  "properties": {
    "serverFarmId": "/subscriptions/$subscription_id/resourceGroups/$resource_group/providers/Microsoft.Web/serverfarms/$plan_name",
    "httpsOnly": true,
    "siteConfig": {
      "linuxFxVersion": "$runtime",
      "appCommandLine": "bash /home/site/wwwroot/startup.sh",
      "alwaysOn": true,
      "ftpsState": "Disabled",
      "cors": {
        "allowedOrigins": ["http://localhost:8081", "http://127.0.0.1:8081"],
        "supportCredentials": false
      }
    }
  }
}
JSON
)"
    az rest --method put --url "$site_url" --body "$site_body" --output none

    index=0
    outbound_ips="$(az rest --method get --url "$site_url" --query properties.outboundIpAddresses -o tsv | tr -d '\r' | tr ',' ' ')"
    if [[ -z "$outbound_ips" ]]; then printf 'Web App has no outbound IPs.\n' >&2; exit 1; fi
    for public_ip in $outbound_ips; do
        az sql server firewall-rule create --subscription "$subscription_id" --resource-group "$resource_group" --server "$sql_server" --name "$app-$index" --start-ip-address "$public_ip" --end-ip-address "$public_ip" --output none
        index=$((index + 1))
    done
done

printf 'Creating pipeline identity...\n'
az identity create --subscription "$subscription_id" --resource-group "$resource_group" --name "$pipeline_identity" --location "$location" --output none
principal_id="$(az identity show --subscription "$subscription_id" --resource-group "$resource_group" --name "$pipeline_identity" --query principalId -o tsv | tr -d '\r')"
az role assignment create --subscription "$subscription_id" --assignee-object-id "$principal_id" --assignee-principal-type ServicePrincipal --role 'Website Contributor' --scope "/subscriptions/$subscription_id/resourceGroups/$resource_group" --output none

if [[ -n "${PIPELINE_FEDERATION_ISSUER:-}" ]]; then
    az identity federated-credential create --subscription "$subscription_id" --resource-group "$resource_group" --identity-name "$pipeline_identity" --name papertrail-azure --issuer "$PIPELINE_FEDERATION_ISSUER" --subject "$PIPELINE_FEDERATION_SUBJECT" --audiences api://AzureADTokenExchange --output none
fi

printf 'Resource group: %s\nSQL server: %s.database.windows.net\n' "$resource_group" "$sql_server"
for app in "$java_app" "$dotnet_app"; do
    site_url="https://management.azure.com/subscriptions/$subscription_id/resourceGroups/$resource_group/providers/Microsoft.Web/sites/$app?api-version=2024-11-01"
    host_name="$(az rest --method get --url "$site_url" --query properties.defaultHostName -o tsv | tr -d '\r')"
    printf '%s: https://%s\n' "$app" "$host_name"
done

