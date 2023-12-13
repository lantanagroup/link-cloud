Param(
  [Parameter(Mandatory=$true, Position=0)]
  [ValidateSet('Docker','ConsoleApp','IISExpress')]
  [string]$ActiveDebugProfile
)

$files = @(
    "Account/Account.csproj.user", 
	"Audit/Audit.csproj.user",
    "Api/Api.csproj.user",
    "Census/Census.csproj.user",
    "DataAcquisition/DataAcquisition.csproj.user",
    "MeasureEval/MeasureEval.csproj.user",
    "Normalization/Normalization.csproj.user",
	"Notification/Notification.csproj.user",
    "PatientList/PatientList.csproj.user",
    "Report/Report.csproj.user",
    "Tenant/Tenant.csproj.user",
    "TenantDataCustodian/TenantDataCustodian.csproj.user",
    "Validation/Validation.csproj.user"
)

foreach ($csprojFile in $files) {
    $csprojFileAbs = Resolve-Path $csprojFile
    [xml]$csproj = Get-Content -Path $csprojFileAbs
    $changed = $False

    foreach ($propertyGroup in $csproj.Project.PropertyGroup) {
        if ($propertyGroup.ActiveDebugProfile) {
            $propertyGroup.ActiveDebugProfile = $ActiveDebugProfile
            $changed = $True
        }
    }

    if ($changed) {
        $csproj.Save($csprojFileAbs)
    }
}