# Get the list of running containers and their IP addresses
$containers = docker ps --format "{{.Names}}"
$hostsEntries = @()

foreach ($container in $containers) {
    $ip = docker inspect -f '{{range.NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $container
    if ($ip) {
        $hostsEntries += "127.0.0.1`t$container"
    }
}

# Check for admin privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script requires administrative privileges to modify the hosts file. Please run as Administrator."
    exit 1
}

# Define the path to the hosts file
$hostsPath = "C:\Windows\System32\drivers\etc\hosts"

# Confirm with user before proceeding
Write-Host "This script will modify your system's hosts file at $hostsPath"
Write-Host "A backup will be created at $hostsPath.bak"
$confirmation = Read-Host "Do you want to continue? (y/n)"
if ($confirmation -ne 'y') {
    Write-Host "Operation cancelled by user."
    exit 0
}

# Backup the current hosts file
try {
    Copy-Item -Path $hostsPath -Destination "$hostsPath.bak" -Force
    Write-Host "Backup created successfully at $hostsPath.bak"
} catch {
    Write-Error "Failed to create backup: $_"
    exit 1
}
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
