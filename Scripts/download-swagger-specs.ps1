param (
    [Parameter(Mandatory=$true)]
    [string]$NewVersion
)

# Import the YAML module
Import-Module powershell-yaml

# Dictionary mapping URLs to destination files
$specMap = @{
    "http://localhost:8060/swagger/v1/swagger.json" = "docs/domains/Compliance/services/AccountService/openapi.yml"
    "http://localhost:8062/swagger/v1/swagger.json" = "docs/domains/Compliance/services/AuditService/openapi.yml"
    "http://localhost:8064/swagger/v1/swagger.json" = "docs/domains/Tenant/services/CensusService/openapi.yml"
    "http://localhost:8065/swagger/v1/swagger.json" = "docs/domains/DataAccess/services/DataAcquisitionService/openapi.yml"
    "http://localhost:8078/swagger/v1/swagger.json" = "docs/domains/DataAccess/services/DataAcquisitionWorkerService/openapi.yml"
    "http://localhost:8067/v3/api-docs" = "docs/domains/Report/services/MeasureEvalService/openapi.yml"
    "http://localhost:8068/swagger/v1/swagger.json" = "docs/domains/Report/services/NormalizationService/openapi.yml"
    "http://localhost:8072/swagger/v1/swagger.json" = "docs/domains/Report/services/ReportService/openapi.yml"
    "http://localhost:8073/swagger/v1/swagger.json" = "docs/domains/Report/services/SubmissionService/openapi.yml"
    "http://localhost:8074/swagger/v1/swagger.json" = "docs/domains/Tenant/services/TenantService/openapi.yml"
    "http://localhost:8075/v3/api-docs" = "docs/domains/Compliance/services/ValidationService/openapi.yml"
}

foreach ($url in $specMap.Keys) {
    try {
        Write-Host "Processing $url..."

        # Download Swagger JSON
        $jsonContent = Invoke-RestMethod -Uri $url
        
        $jsonContent.info = @{
            title   = $jsonContent.info.title
            version = $NewVersion
        }
        
        $jsonContent.PSObject.Properties.Remove("servers")

        $yamlContent = $jsonContent | ConvertTo-Yaml

        # Get destination path
        $outputPath = $specMap[$url]

        # Ensure the directory exists
        $directory = Split-Path -Path $outputPath
        if (-not (Test-Path $directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }

        # Write YAML to file
        Set-Content -Path $outputPath -Value $yamlContent -Encoding UTF8

        Write-Host "Saved YAML to $outputPath"
    } catch {
        Write-Error "Failed to process $url due to $_"
    }
}