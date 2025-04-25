# Path to your JSON file
$jsonPath = "$env:BUILD_SOURCESDIRECTORY/DotNet/Account/appsettings.json"

# Load the JSON file
$json = Get-Content $jsonPath -Raw | ConvertFrom-Json

$old = $json.ServiceInformation.GitCommit

$new = "12344"

(Get-Content -Path "$env:BUILD_SOURCESDIRECTORY/DotNet/Account/appsettings.json") | ForEach-Object { $_ -replace $old, $new } | Set-Content -Path "$env:BUILD_SOURCESDIRECTORY/DotNet/Account/appsettings.json"

Get-Content -Path "$env:BUILD_SOURCESDIRECTORY/DotNet/Account/appsettings.json"