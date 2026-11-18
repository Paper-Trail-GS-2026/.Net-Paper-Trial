#!/usr/bin/env bash
set -euo pipefail
broker_ip="${PAPERTRAIL_BROKER_IP:-10.34.1.4}"

apt-get update -qq
DEBIAN_FRONTEND=noninteractive apt-get install -y -qq docker.io openssl
systemctl enable --now docker
install -d -m 700 /etc/papertrail

if [[ ! -f /etc/papertrail/rabbitmq.env ]]; then
  umask 077
  printf 'RABBITMQ_DEFAULT_USER=papertrail-admin\nRABBITMQ_DEFAULT_PASS=%s\n' \
    "$(openssl rand -hex 24)" > /etc/papertrail/rabbitmq.env
fi
if ! docker container inspect papertrail-rabbitmq >/dev/null 2>&1; then
  docker run --detach --name papertrail-rabbitmq --hostname papertrail-rabbitmq \
    --restart unless-stopped --env-file /etc/papertrail/rabbitmq.env \
    --volume papertrail-rabbitmq-data:/var/lib/rabbitmq \
    --publish "$broker_ip:5672:5672" --publish 127.0.0.1:15672:15672 \
    rabbitmq:4.2-management >/dev/null
fi

ready=false
for attempt in {1..45}; do
  if docker exec papertrail-rabbitmq rabbitmq-diagnostics -q check_running >/dev/null 2>&1; then
    ready=true
    break
  fi
  sleep 2
done
if [[ "$ready" != true ]]; then printf 'RabbitMQ did not start.\n' >&2; exit 1; fi

if [[ ! -f /etc/papertrail/rabbitmq-app.password ]]; then
  umask 077
  openssl rand -hex 24 > /etc/papertrail/rabbitmq-app.password
fi
app_password="$(cat /etc/papertrail/rabbitmq-app.password)"
if ! docker exec papertrail-rabbitmq rabbitmqctl list_users -q | cut -f1 | grep -qx papertrail; then
  docker exec papertrail-rabbitmq rabbitmqctl add_user papertrail "$app_password" >/dev/null
fi
docker exec papertrail-rabbitmq rabbitmqctl set_permissions -p / papertrail \
  '^(papertrail\.catalog|papertrail\.dotnet\.catalog-version)$' \
  '^(papertrail\.catalog|papertrail\.dotnet\.catalog-version)$' \
  '^(papertrail\.catalog|papertrail\.dotnet\.catalog-version)$' >/dev/null
docker exec papertrail-rabbitmq rabbitmqctl set_user_tags papertrail >/dev/null
docker exec papertrail-rabbitmq rabbitmq-diagnostics -q check_running
printf 'RabbitMQ ready on the private network.\n'
