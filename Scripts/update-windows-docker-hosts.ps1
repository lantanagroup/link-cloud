# Get the list of running containers and their IP addresses
$containers = docker ps --format "{{.Names}}"
$hostsEntries = @()

foreach ($container in $containers) {
    $ip = docker inspect -f '{{range.NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $container
    if ($ip) {
        $hostsEntries += "127.0.0.1`t$container"
    }
}

# Define the path to the hosts file
$hostsPath = "C:\Windows\System32\drivers\etc\hosts"

# Backup the current hosts file
Copy-Item -Path $hostsPath -Destination "$hostsPath.bak" -Force

# Remove existing Docker entries between custom markers
$hostsContent = Get-Content $hostsPath
$startMarker = "# DOCKER-HOSTS-START"
$endMarker = "# DOCKER-HOSTS-END"
$startIndex = $hostsContent.IndexOf($startMarker)
$endIndex = $hostsContent.IndexOf($endMarker)

if ($startIndex -ge 0 -and $endIndex -gt $startIndex) {
    $hostsContent = $hostsContent[0..($startIndex - 1)] + $hostsContent[($endIndex + 1)..($hostsContent.Length - 1)]
}

# Add new Docker entries
$dockerHosts = @($startMarker) + $hostsEntries + $endMarker
$hostsContent += $dockerHosts

# Write the updated content back to the hosts file
Set-Content -Path $hostsPath -Value $hostsContent -Force
