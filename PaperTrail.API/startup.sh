#!/usr/bin/env bash
set -euo pipefail

: "${JWT_PUBLIC_KEY_PEM_B64:?Configure JWT_PUBLIC_KEY_PEM_B64 in App Service.}"
umask 077
key_dir=/home/papertrail-keys
mkdir -p "$key_dir"
printf '%s' "$JWT_PUBLIC_KEY_PEM_B64" | base64 --decode > "$key_dir/public.pem"
export Jwt__PublicKeyPath="$key_dir/public.pem"
unset JWT_PUBLIC_KEY_PEM_B64

export ASPNETCORE_URLS="http://0.0.0.0:${PORT:-8080}"

exec dotnet /home/site/wwwroot/PaperTrail.API.dll
