# Path to your JSON file
$jsonPath = "$env:BUILD_SOURCESDIRECTORY/DotNet/$env:serviceName/appsettings.json"

# Load the JSON file
$json = Get-Content $jsonPath -Raw | ConvertFrom-Json

$old = $json.ServiceInformation.GitCommit

$new = "$env:GIT_COMMIT"

(Get-Content -Path "$env:BUILD_SOURCESDIRECTORY/DotNet/$env:serviceName/appsettings.json") | ForEach-Object { $_ -replace $old, $new } | Set-Content -Path "$env:BUILD_SOURCESDIRECTORY/DotNet/Account/appsettings.json"

Get-Content -Path "$env:BUILD_SOURCESDIRECTORY/DotNet/$env:serviceName/appsettings.json"