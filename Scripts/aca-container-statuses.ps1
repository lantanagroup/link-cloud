$containerApps = az containerapp list --environment BotW-Apps | ConvertFrom-Json
$services = [System.Collections.ArrayList]@()

foreach ($containerApp in $containerApps) {
    $properties = @{
        Name=$containerApp.Name
        RunningStatus=$containerApp.properties.runningStatus
        ProvisioningState=$containerApp.properties.provisioningState
        MinReplicas=$containerApp.properties.template.scale.minReplicas
        MaxReplicas=$containerApp.properties.template.scale.maxReplicas
    }
    $service = New-Object PSObject -Property $properties
    $services.Add($service)
}

$services | Sort -Property Name | Format-Table -Property Name, MinReplicas, MaxReplicas, RunningStatus, ProvisioningState