param (
    [Parameter(Mandatory=$true)]
    [string]$NewVersion
)

# Import the YAML module
Import-Module powershell-yaml

# Dictionary mapping URLs to destination files
$specMap = @{
    "https://dev-account.nhsnlink.org/swagger/v1/swagger.json" = "services/AccountService/openapi.yml"
    "https://dev-audit.nhsnlink.org/swagger/v1/swagger.json" = "services/AuditService/openapi.yml"
    "https://dev-census.nhsnlink.org/swagger/v1/swagger.json" = "services/CensusService/openapi.yml"
    "https://dev-data.nhsnlink.org/swagger/v1/swagger.json"  = "services/DataAcquisitionService/openapi.yml"
    "https://dev-measure.nhsnlink.org/v3/api-docs"  = "services/MeasureEvalService/openapi.yml"
    #"http://localhost:8068/swagger/v1/swagger.json"  = "services/NormalizationService/openapi.yml"
    "https://dev-normalization.nhsnlink.org/swagger/v1/swagger.json"  = "services/NormalizationService/openapi.yml"
    "https://dev-report.nhsnlink.org/swagger/v1/swagger.json"  = "services/ReportService/openapi.yml"
    #"http://localhost:8073/swagger/v1/swagger.json"  = "services/SubmissionService/openapi.yml"
    "https://dev-submission.nhsnlink.org/swagger/v1/swagger.json"  = "services/SubmissionService/openapi.yml"
    "https://dev-tenant.nhsnlink.org/swagger/v1/swagger.json"  = "services/TenantService/openapi.yml"
    "https://dev-validation.nhsnlink.org/v3/api-docs"  = "services/ValidationService/openapi.yml"
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