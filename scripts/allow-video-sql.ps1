param([switch]$Close)

$ErrorActionPreference = 'Stop'
$azCommand = Get-Command az -ErrorAction SilentlyContinue
$azPath = if ($azCommand) { $azCommand.Source } else {
    Join-Path ${env:ProgramFiles(x86)} 'Microsoft SDKs\Azure\CLI2\wbin\az.cmd'
}
if (-not (Test-Path -LiteralPath $azPath)) {
    throw 'Instale o Azure CLI e abra o terminal novamente.'
}

if ($Close) {
    $clientIp = '192.0.2.1'
} else {
    $clientIp = (& curl.exe --ipv4 --fail --silent --show-error --connect-timeout 10 --max-time 20 https://api.ipify.org) -join ''
    if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel consultar o IPv4 publico.' }
    $address = $null
    if (-not [Net.IPAddress]::TryParse($clientIp, [ref]$address) -or
        $address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) {
        throw 'O servico de consulta nao retornou um IPv4 valido.'
    }
    $parts = $address.GetAddressBytes()
    if ($parts[0] -in @(0, 10, 127) -or $parts[0] -ge 224 -or
        ($parts[0] -eq 169 -and $parts[1] -eq 254) -or
        ($parts[0] -eq 172 -and $parts[1] -ge 16 -and $parts[1] -le 31) -or
        ($parts[0] -eq 192 -and $parts[1] -eq 168)) {
        throw 'O endereco retornado nao e um IPv4 publico.'
    }
}

$token = (& $azPath account get-access-token --resource https://management.azure.com/ --query accessToken --output tsv) -join ''
if ($LASTEXITCODE -ne 0 -or -not $token) {
    throw 'Entre no Azure CLI com a conta FIAP antes de executar este script.'
}

$url = 'https://management.azure.com/subscriptions/44b8b12c-4907-4842-9af8-2ea45fc8d44a/resourceGroups/rm561584-papertrail-gs/providers/Microsoft.Sql/servers/papertrail-sql-rm561584/firewallRules/andre-video?api-version=2023-08-01'
$body = @{properties = @{startIpAddress = $clientIp; endIpAddress = $clientIp}} | ConvertTo-Json -Compress
$escaped = $body.Replace('\', '\\').Replace('"', '\"')
$config = @(('url = "' + $url + '"'), ('header = "Authorization: Bearer ' + $token + '"'),
    'request = "PUT"', 'header = "Content-Type: application/json"', ('data = "' + $escaped + '"')) -join [Environment]::NewLine
try {
    $response = ($config | & curl.exe --ipv4 --fail-with-body --silent --show-error --connect-timeout 15 --max-time 40 --config -) -join [Environment]::NewLine
    if ($LASTEXITCODE -ne 0) { throw "A regra nao foi atualizada: $response" }
    $updated = $response | ConvertFrom-Json
    if ($updated.properties.startIpAddress -ne $clientIp -or $updated.properties.endIpAddress -ne $clientIp) {
        throw 'O endereco da regra nao corresponde ao solicitado.'
    }
    if ($Close) {
        Write-Output 'Acesso da gravacao fechado. As regras das aplicacoes foram preservadas.'
    } else {
        Write-Output "Acesso da gravacao liberado para $clientIp. Aguarde ate cinco minutos e abra o Query Editor."
    }
} finally {
    $token = $null
    $config = $null
}
