# Path to your JSON file
$jsonPath = "$env:BUILD_SOURCESDIRECTORY/DotNet/$env:SERVICENAME/appsettings.json"

# Service Name
$serviceName = $env:SERVICENAME

# Load the JSON file
$json = Get-Content $jsonPath -Raw | ConvertFrom-Json

# Gathers the current GitCommit value
$old = $json.ServiceInformation.GitCommit

# Updates the GitCommit value
$new = "$env:GIT_COMMIT"

(Get-Content -Path "$env:BUILD_SOURCESDIRECTORY/DotNet/$env:SERVICENAME/appsettings.json") | ForEach-Object { $_ -replace $old, $new } | Set-Content -Path "$env:BUILD_SOURCESDIRECTORY/DotNet/$env:SERVICENAME/appsettings.json"

# ECHOs the Appsettings.json value
Get-Content -Path "$env:BUILD_SOURCESDIRECTORY/DotNet/$env:SERVICENAME/appsettings.json"
