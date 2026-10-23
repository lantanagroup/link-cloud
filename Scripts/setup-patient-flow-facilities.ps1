#Requires -Version 7.0
<#
.SYNOPSIS
    Creates (or removes) the Link facilities the Patient Flow Simulator feeds.

.DESCRIPTION
    Reads the facilities and their measures from the simulator scenario file, so Link and the generated
    data cannot drift apart, and sets up each facility through the Admin BFF:

      - the Tenant facility with its scheduled reports (Monthly, or Daily for a measure named *Daily*)
      - the Census schedule, which is how often Link reads the facility's six census Lists
      - the DataAcquisition FHIR query configuration (the FHIR server the simulator uploads to)
      - the DataAcquisition FHIR list configuration (the six census List ids the simulator maintains)
      - the Discharge, Daily and Monthly query plans (from Scripts/seed_data/query_plan.json)
      - the QueryDispatch discharge dispatch schedule

    -LoadMeasures first loads the measure bundles from DotNet/Automation/measures into MeasureEval.
    Items that already exist are left alone; -UpdateExisting changes them to match. -Remove tears the
    facilities down again. -WhatIf shows what would be done.

    When the Tenant service hosts the DMRP module the facility's schedule is either written through to
    the Mock DMRP API (measure mappings are created first) or derived from reporting plans (the
    facility is created with an empty schedule, enrolled for this month and the next, then saved
    again). See FacilitySetupHelper in DotNet/Automation.Link for the three paths.

.PARAMETER AdminBffUrl
    Admin BFF base URL. Default: LINK_ADMIN_BFF_URL or http://localhost:8063.
.PARAMETER AccessToken
    Bearer token for the Admin BFF. Default: LINK_ADMIN_BFF_TOKEN. Not needed on the local stack.
.PARAMETER FhirServerBaseUrl
    FHIR base URL as the Link services reach it (inside docker-compose that is the container name, not
    localhost). Default: LINK_FACILITY_FHIR_BASE_URL or http://fhir-server:8080/fhir.
.PARAMETER ScenarioPath
    Simulator scenario file. Default: DotNet/PatientFlowSimulator/scenarios/default.json.
.PARAMETER FacilityId
    Only set up (or remove) these facility ids from the scenario.
.PARAMETER CensusCron
    Quartz cron for the census schedule. Default: every 15 minutes.
.PARAMETER DischargeDispatchDelay
    ISO-8601 delay between a discharge and data acquisition. Default: PT0S.
.PARAMETER QueryPlanPath
    Query plan template. Default: Scripts/seed_data/query_plan.json.
.PARAMETER EhrDescription
    EHR description stamped on the query plans. Default: Epic.
.PARAMETER LoadMeasures
    Load the facilities' measure bundles into MeasureEval when they are not there yet.
.PARAMETER MeasuresPath
    Directory holding <measure id>.json bundles. Default: DotNet/Automation/measures.
.PARAMETER UpdateExisting
    Change existing facilities and configurations to match the scenario instead of leaving them alone.
.PARAMETER Remove
    Remove the facilities and all of their configuration instead of creating them.

.EXAMPLE
    ./setup-patient-flow-facilities.ps1 -LoadMeasures
.EXAMPLE
    ./setup-patient-flow-facilities.ps1 -AdminBffUrl https://link-qa.example.org -AccessToken $token `
        -FhirServerBaseUrl https://fhir-qa.example.org/fhir -LoadMeasures
.EXAMPLE
    ./setup-patient-flow-facilities.ps1 -Remove
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$AdminBffUrl = ($env:LINK_ADMIN_BFF_URL ?? 'http://localhost:8063'),
    [string]$AccessToken = $env:LINK_ADMIN_BFF_TOKEN,
    [string]$FhirServerBaseUrl = ($env:LINK_FACILITY_FHIR_BASE_URL ?? 'http://fhir-server:8080/fhir'),
    [string]$ScenarioPath = (Join-Path $PSScriptRoot '..' 'DotNet' 'PatientFlowSimulator' 'scenarios' 'default.json'),
    [string[]]$FacilityId,
    [string]$CensusCron = '0 0/15 * * * ?',
    [string]$DischargeDispatchDelay = 'PT0S',
    [string]$QueryPlanPath = (Join-Path $PSScriptRoot 'seed_data' 'query_plan.json'),
    [string]$EhrDescription = 'Epic',
    [switch]$LoadMeasures,
    [string]$MeasuresPath = (Join-Path $PSScriptRoot '..' 'DotNet' 'Automation' 'measures'),
    [switch]$UpdateExisting,
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

$script:BaseUrl = $AdminBffUrl.TrimEnd('/')
$script:AccessToken = $AccessToken
$script:Summary = [System.Collections.Generic.List[object]]::new()

# The six census lists Link expects per facility, in the order DataAcquisition stores them. The ids
# must match CensusListBuilder.ListId in the simulator.
$script:CensusLists = @(
    @{ status = 'Admit';     timeFrame = 'LessThan24Hours';    suffix = 'admit-lt24' },
    @{ status = 'Admit';     timeFrame = 'Between24To48Hours'; suffix = 'admit-24to48' },
    @{ status = 'Admit';     timeFrame = 'MoreThan48Hours';    suffix = 'admit-gt48' },
    @{ status = 'Discharge'; timeFrame = 'LessThan24Hours';    suffix = 'discharge-lt24' },
    @{ status = 'Discharge'; timeFrame = 'Between24To48Hours'; suffix = 'discharge-24to48' },
    @{ status = 'Discharge'; timeFrame = 'MoreThan48Hours';    suffix = 'discharge-gt48' }
)

$script:QueryPlanTypes = @('Discharge', 'Daily', 'Monthly')

function Invoke-LinkApi {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        $Body,
        [switch]$RawBody,
        [int[]]$AllowedStatus = @()
    )

    $uri = "$script:BaseUrl/$($Path.TrimStart('/'))"
    $headers = @{ Accept = 'application/json' }
    if ($script:AccessToken) {
        $headers['Authorization'] = "Bearer $script:AccessToken"
    }

    $request = @{ Method = $Method; Uri = $uri; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) {
        $request['ContentType'] = 'application/json'
        $request['Body'] = if ($RawBody) { $Body } else { $Body | ConvertTo-Json -Depth 32 }
    }

    $response = Invoke-WebRequest @request
    $status = [int]$response.StatusCode
    $content = [string]$response.Content
    $parsed = $null
    if (-not [string]::IsNullOrWhiteSpace($content)) {
        try { $parsed = $content | ConvertFrom-Json } catch { $parsed = $content }
    }

    if ($status -ge 400 -and $status -notin $AllowedStatus) {
        throw "$Method $uri failed with HTTP $status.`n$content"
    }

    return [pscustomobject]@{ Status = $status; Body = $parsed }
}

# A GET that answers 404, 204 or an empty body all mean "not there".
function Get-LinkResource {
    param([Parameter(Mandatory)][string]$Path)

    $result = Invoke-LinkApi -Method GET -Path $Path -AllowedStatus 404
    if ($result.Status -eq 404 -or $null -eq $result.Body) {
        return $null
    }

    return $result.Body
}

function Read-Scenario {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Scenario file '$Path' was not found."
    }

    # The scenario file carries // comment lines, which ConvertFrom-Json does not accept.
    $json = (Get-Content $Path -Raw) -replace '(?m)^\s*//.*$', ''
    return $json | ConvertFrom-Json
}

function Get-MeasureFrequency {
    param([Parameter(Mandatory)][string]$Measure)

    if ($Measure -match 'Daily') { return 'Daily' }
    return 'Monthly'
}

function Get-FacilitySchedule {
    param([Parameter(Mandatory)]$Facility)

    $measures = @($Facility.measures)
    return @{
        daily   = @($measures | Where-Object { (Get-MeasureFrequency $_) -eq 'Daily' })
        weekly  = @()
        monthly = @($measures | Where-Object { (Get-MeasureFrequency $_) -eq 'Monthly' })
    }
}

function Test-SameSchedule {
    param($Existing, [Parameter(Mandatory)]$Wanted)

    foreach ($frequency in 'daily', 'weekly', 'monthly') {
        $have = @($Existing.$frequency) | Sort-Object
        $want = @($Wanted[$frequency]) | Sort-Object
        if (($have -join ',') -ne ($want -join ',')) {
            return $false
        }
    }

    return $true
}

function Add-Summary {
    param([string]$Facility, [string]$Step, [string]$Result)

    $script:Summary.Add([pscustomobject]@{ Facility = $Facility; Step = $Step; Result = $Result })
    Write-Host "  [$Step] $Result"
}

function Get-DmrpStatus {
    [CmdletBinding()]
    param()

    $status = Invoke-LinkApi -Method GET -Path 'api/dmrp/dmrp-status' -AllowedStatus 404
    if ($status.Status -ne 404 -and $null -ne $status.Body) {
        return [pscustomobject]@{ Enabled = [bool]$status.Body.dmrpEnabled; Mock = [bool]$status.Body.mockDmrpEnabled }
    }

    # An older Tenant has no dmrp-status route; a disabled module answers 404 on every DMRP route.
    $probe = Invoke-LinkApi -Method GET -Path 'api/dmrp/reporting-plans?pageSize=1&pageNumber=1' -AllowedStatus 404
    return [pscustomobject]@{ Enabled = ($probe.Status -ne 404); Mock = $false }
}

function Get-MeasureMappingId {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$Measure)

    $frequency = Get-MeasureFrequency $Measure
    $query = "api/dmrp/measure-mappings/search?measure=$([uri]::EscapeDataString($Measure))&pageSize=50&pageNumber=1"
    $search = Invoke-LinkApi -Method GET -Path $query -AllowedStatus 404
    $records = @()
    if ($null -ne $search.Body -and $null -ne $search.Body.records) {
        $records = @($search.Body.records)
    }

    $match = $records | Where-Object { $_.measure -eq $Measure } | Select-Object -First 1
    if ($match) {
        if ($match.dqm -ne $Measure -or "$($match.frequency)" -ne $frequency) {
            if ($PSCmdlet.ShouldProcess($Measure, "Correct DMRP measure mapping to $frequency")) {
                $body = @{ id = $match.id; measure = $Measure; dqm = $Measure; frequency = $frequency }
                Invoke-LinkApi -Method PUT -Path "api/dmrp/measure-mappings/$($match.id)" -Body $body | Out-Null
            }
        }

        return $match.id
    }

    if (-not $PSCmdlet.ShouldProcess($Measure, "Create DMRP measure mapping ($frequency)")) {
        return $null
    }

    $created = Invoke-LinkApi -Method POST -Path 'api/dmrp/measure-mappings' `
        -Body @{ measure = $Measure; dqm = $Measure; frequency = $frequency }
    return $created.Body.id
}

# Enroll the facility for the month it is in and the next one, in its own time zone, the same way the
# Automation facility setup does.
function Add-ReportingPlans {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)]$Facility, [Parameter(Mandatory)][hashtable]$MappingIds)

    $zone = [TimeZoneInfo]::FindSystemTimeZoneById($Facility.timeZone)
    $now = [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow, $zone)
    $periods = @($now, $now.AddMonths(1))

    foreach ($measure in $Facility.measures) {
        foreach ($period in $periods) {
            $target = "$($Facility.facilityId) $measure $($period.Year)-$($period.Month.ToString('00'))"
            if (-not $PSCmdlet.ShouldProcess($target, 'Create DMRP reporting plan')) {
                continue
            }

            $body = @{
                facilityId       = $Facility.facilityId
                measureMappingId = $MappingIds[$measure]
                reportingMonth   = $period.Month
                reportingYear    = $period.Year
                isReporting      = $true
            }
            $result = Invoke-LinkApi -Method POST -Path 'api/dmrp/reporting-plans' -Body $body -AllowedStatus 409
            if ($result.Status -eq 409) {
                Write-Host "    reporting plan $target already exists"
            }
        }
    }
}

function Initialize-Facility {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)]$Facility, [Parameter(Mandatory)]$Dmrp)

    $id = $Facility.facilityId
    $schedule = Get-FacilitySchedule $Facility
    $derived = $Dmrp.Enabled -and -not $Dmrp.Mock
    $mappingIds = @{}
    if ($Dmrp.Enabled) {
        foreach ($measure in $Facility.measures) {
            $mappingIds[$measure] = Get-MeasureMappingId -Measure $measure
        }
    }

    $existing = Get-LinkResource "api/facility/$id"
    $body = @{
        facilityId       = $id
        facilityName     = $Facility.name
        timeZone         = $Facility.timeZone
        scheduledReports = if ($derived) { @{ daily = @(); weekly = @(); monthly = @() } } else { $schedule }
    }

    if ($null -eq $existing) {
        if ($PSCmdlet.ShouldProcess($id, 'Create facility')) {
            Invoke-LinkApi -Method POST -Path 'api/facility' -Body $body | Out-Null
            Add-Summary $id 'facility' 'created'
        }
    }
    elseif ($derived) {
        Add-Summary $id 'facility' 'exists (schedule derived from DMRP reporting plans)'
    }
    elseif (Test-SameSchedule $existing.scheduledReports $schedule) {
        Add-Summary $id 'facility' 'exists with the expected schedule'
    }
    elseif ($UpdateExisting) {
        if ($PSCmdlet.ShouldProcess($id, 'Update facility schedule')) {
            $body['id'] = $existing.id
            $body['vendor'] = $existing.vendor
            $body['vendorVersionId'] = $existing.vendorVersionId
            Invoke-LinkApi -Method PUT -Path "api/facility/$id" -Body $body | Out-Null
            Add-Summary $id 'facility' 'schedule updated'
        }
    }
    else {
        Add-Summary $id 'facility' 'exists with a different schedule (use -UpdateExisting)'
    }

    if (-not $derived) {
        return
    }

    Add-ReportingPlans -Facility $Facility -MappingIds $mappingIds
    $saved = Get-LinkResource "api/facility/$id"
    if ($null -ne $saved -and $PSCmdlet.ShouldProcess($id, 'Save facility so Tenant derives its schedule')) {
        $body['id'] = $saved.id
        $body['vendor'] = $saved.vendor
        $body['vendorVersionId'] = $saved.vendorVersionId
        Invoke-LinkApi -Method PUT -Path "api/facility/$id" -Body $body | Out-Null
        Add-Summary $id 'facility' 'schedule derived from reporting plans'
    }
}

function Initialize-CensusConfig {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$Id)

    $existing = Get-LinkResource "api/census/config/$Id"
    $body = @{ facilityId = $Id; scheduledTrigger = $CensusCron; enabled = $true }

    if ($null -eq $existing) {
        if ($PSCmdlet.ShouldProcess($Id, "Create census schedule '$CensusCron'")) {
            Invoke-LinkApi -Method POST -Path 'api/census/config' -Body $body | Out-Null
            Add-Summary $Id 'census' "created ($CensusCron)"
        }
    }
    elseif ($existing.scheduledTrigger -eq $CensusCron -and $existing.enabled) {
        Add-Summary $Id 'census' 'exists'
    }
    elseif ($UpdateExisting) {
        if ($PSCmdlet.ShouldProcess($Id, "Update census schedule to '$CensusCron'")) {
            Invoke-LinkApi -Method PUT -Path "api/census/config/$Id" -Body $body | Out-Null
            Add-Summary $Id 'census' "updated ($CensusCron)"
        }
    }
    else {
        Add-Summary $Id 'census' "exists with '$($existing.scheduledTrigger)', enabled=$($existing.enabled) (use -UpdateExisting)"
    }
}

function Initialize-QueryConfig {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)]$Facility)

    $id = $Facility.facilityId
    $existing = Get-LinkResource "api/data-acquisition/$id/fhirQueryConfiguration"
    $body = @{
        facilityId            = $id
        fhirServerBaseUrl     = $FhirServerBaseUrl
        authentication        = $null
        maxConcurrentRequests = 8
        maxRetries            = 3
        timeZone              = $Facility.timeZone
    }

    if ($null -eq $existing) {
        if ($PSCmdlet.ShouldProcess($id, "Create FHIR query configuration for $FhirServerBaseUrl")) {
            Invoke-LinkApi -Method POST -Path 'api/data-acquisition/fhirQueryConfiguration' -Body $body | Out-Null
            Add-Summary $id 'query config' "created ($FhirServerBaseUrl)"
        }
    }
    elseif ($existing.fhirServerBaseUrl -eq $FhirServerBaseUrl) {
        Add-Summary $id 'query config' 'exists'
    }
    elseif ($UpdateExisting) {
        if ($PSCmdlet.ShouldProcess($id, "Point FHIR query configuration at $FhirServerBaseUrl")) {
            Invoke-LinkApi -Method PUT -Path 'api/data-acquisition/fhirQueryConfiguration' -Body $body | Out-Null
            Add-Summary $id 'query config' "updated ($FhirServerBaseUrl)"
        }
    }
    else {
        Add-Summary $id 'query config' "exists for '$($existing.fhirServerBaseUrl)' (use -UpdateExisting)"
    }
}

function Initialize-ListConfig {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$Id)

    $lists = @($script:CensusLists | ForEach-Object {
        @{ status = $_.status; timeFrame = $_.timeFrame; fhirId = "census-$Id-$($_.suffix)" }
    })
    $body = @{ facilityId = $Id; fhirBaseServerUrl = $FhirServerBaseUrl; ehrPatientLists = $lists }
    $wantedIds = ($lists | ForEach-Object { $_.fhirId } | Sort-Object) -join ','

    $existing = Get-LinkResource "api/data-acquisition/$Id/fhirQueryList"
    if ($null -eq $existing) {
        if ($PSCmdlet.ShouldProcess($Id, 'Create the six census list configurations')) {
            Invoke-LinkApi -Method POST -Path 'api/data-acquisition/fhirQueryList' -Body $body | Out-Null
            Add-Summary $Id 'census lists' 'created (6 lists)'
        }

        return
    }

    $existingIds = (@($existing.ehrPatientLists) | ForEach-Object { $_.fhirId } | Sort-Object) -join ','
    if ($existingIds -eq $wantedIds -and $existing.fhirBaseServerUrl -eq $FhirServerBaseUrl) {
        Add-Summary $Id 'census lists' 'exist'
    }
    elseif ($UpdateExisting) {
        if ($PSCmdlet.ShouldProcess($Id, 'Replace the census list configurations')) {
            Invoke-LinkApi -Method PUT -Path 'api/data-acquisition/fhirQueryList' -Body $body | Out-Null
            Add-Summary $Id 'census lists' 'updated (6 lists)'
        }
    }
    else {
        Add-Summary $Id 'census lists' 'exist with different ids or server (use -UpdateExisting)'
    }
}

function Initialize-QueryPlans {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)]$Facility)

    if (-not (Test-Path $QueryPlanPath)) {
        throw "Query plan template '$QueryPlanPath' was not found."
    }

    $id = $Facility.facilityId
    $planName = if (@($Facility.measures).Count -gt 0) { $Facility.measures[0] } else { $id }

    foreach ($type in $script:QueryPlanTypes) {
        $existing = Get-LinkResource "api/data-acquisition/$id/QueryPlan?type=$type"
        if ($null -ne $existing -and -not $UpdateExisting) {
            Add-Summary $id "query plan $type" 'exists'
            continue
        }

        if (-not $PSCmdlet.ShouldProcess($id, "Create $type query plan")) {
            continue
        }

        if ($null -ne $existing) {
            Invoke-LinkApi -Method DELETE -Path "api/data-acquisition/$id/QueryPlan?type=$type" -AllowedStatus 404 | Out-Null
        }

        # Re-read the template for every plan: it is edited in place below.
        $plan = Get-Content $QueryPlanPath -Raw | ConvertFrom-Json
        $plan.FacilityId = $id
        $plan.PlanName = $planName
        $plan.EHRDescription = $EhrDescription
        $plan.Type = $type
        Invoke-LinkApi -Method POST -Path "api/data-acquisition/$id/QueryPlan" -Body $plan | Out-Null
        Add-Summary $id "query plan $type" $(if ($null -ne $existing) { 'replaced' } else { 'created' })
    }
}

function Initialize-DispatchConfig {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$Id)

    $existing = Get-LinkResource "api/querydispatch/configuration/facility/$Id"
    $body = @{
        facilityId        = $Id
        dispatchSchedules = @(@{ event = 'Discharge'; duration = $DischargeDispatchDelay })
    }

    if ($null -eq $existing) {
        if ($PSCmdlet.ShouldProcess($Id, "Create discharge dispatch schedule ($DischargeDispatchDelay)")) {
            Invoke-LinkApi -Method POST -Path 'api/querydispatch/configuration' -Body $body | Out-Null
            Add-Summary $Id 'dispatch' "created ($DischargeDispatchDelay)"
        }

        return
    }

    $discharge = @($existing.dispatchSchedules) | Where-Object { $_.event -eq 'Discharge' } | Select-Object -First 1
    if ($null -ne $discharge -and $discharge.duration -eq $DischargeDispatchDelay) {
        Add-Summary $Id 'dispatch' 'exists'
    }
    elseif ($UpdateExisting) {
        if ($PSCmdlet.ShouldProcess($Id, "Update discharge dispatch schedule to $DischargeDispatchDelay")) {
            Invoke-LinkApi -Method PUT -Path "api/querydispatch/configuration/facility/$Id" -Body $body | Out-Null
            Add-Summary $Id 'dispatch' "updated ($DischargeDispatchDelay)"
        }
    }
    else {
        Add-Summary $Id 'dispatch' "exists with '$($discharge.duration)' (use -UpdateExisting)"
    }
}

function Initialize-MeasureDefinitions {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string[]]$Measures)

    foreach ($measure in $Measures) {
        $existing = Invoke-LinkApi -Method GET -Path "api/measureeval/measure-definition/$measure" -AllowedStatus 404
        if ($existing.Status -ne 404 -and $null -ne $existing.Body) {
            Add-Summary '(measures)' $measure 'already loaded'
            continue
        }

        $file = Join-Path $MeasuresPath "$measure.json"
        if (-not (Test-Path $file)) {
            throw "Measure bundle '$file' was not found."
        }

        if ($PSCmdlet.ShouldProcess($measure, 'Load measure definition into MeasureEval')) {
            Invoke-LinkApi -Method PUT -Path 'api/measureeval/measure-definition' -Body (Get-Content $file -Raw) -RawBody | Out-Null
            Add-Summary '(measures)' $measure 'loaded'
        }
    }
}

function Remove-FacilitySetup {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$Id)

    if (-not $PSCmdlet.ShouldProcess($Id, 'Remove facility and all of its configuration')) {
        return
    }

    $steps = @(
        @{ Step = 'dispatch';     Path = "api/querydispatch/configuration/facility/$Id" }
    )
    foreach ($type in $script:QueryPlanTypes) {
        $steps += @{ Step = "query plan $type"; Path = "api/data-acquisition/$Id/QueryPlan?type=$type" }
    }
    $steps += @(
        @{ Step = 'census lists'; Path = "api/data-acquisition/$Id/fhirQueryList" },
        @{ Step = 'query config'; Path = "api/data-acquisition/$Id/fhirQueryConfiguration" },
        @{ Step = 'census';       Path = "api/census/config/$Id" },
        @{ Step = 'facility';     Path = "api/facility/$Id" }
    )

    foreach ($step in $steps) {
        $result = Invoke-LinkApi -Method DELETE -Path $step.Path -AllowedStatus 404
        Add-Summary $Id $step.Step $(if ($result.Status -eq 404) { 'not found' } else { 'removed' })
    }
}

# ---------------------------------------------------------------------------------------------------

$scenario = Read-Scenario $ScenarioPath
$facilities = @($scenario.facilities)
if ($FacilityId) {
    $facilities = @($facilities | Where-Object { $_.facilityId -in $FacilityId })
    if ($facilities.Count -eq 0) {
        throw "None of the requested facility ids are in '$ScenarioPath'."
    }
}

Write-Host "Scenario '$($scenario.name)': $($facilities.Count) facilit$(if ($facilities.Count -eq 1) { 'y' } else { 'ies' }) via $script:BaseUrl"

if ($Remove) {
    foreach ($facility in $facilities) {
        Write-Host "== $($facility.name) ($($facility.facilityId))"
        Remove-FacilitySetup -Id $facility.facilityId
    }
}
else {
    if ($LoadMeasures) {
        Write-Host '== Measure definitions'
        $measures = @($facilities | ForEach-Object { $_.measures } | Select-Object -Unique)
        Initialize-MeasureDefinitions -Measures $measures
    }

    $dmrp = Get-DmrpStatus
    Write-Host $(if (-not $dmrp.Enabled) { 'DMRP is off: schedules are posted with the facilities.' }
                 elseif ($dmrp.Mock) { 'DMRP is on with the Mock DMRP API: schedules are posted and written through as enrollment.' }
                 else { 'DMRP is on: schedules are derived from reporting plans.' })

    foreach ($facility in $facilities) {
        Write-Host "== $($facility.name) ($($facility.facilityId)) -> $($facility.measures -join ', ')"
        Initialize-Facility -Facility $facility -Dmrp $dmrp
        Initialize-CensusConfig -Id $facility.facilityId
        Initialize-QueryConfig -Facility $facility
        Initialize-ListConfig -Id $facility.facilityId
        Initialize-QueryPlans -Facility $facility
        Initialize-DispatchConfig -Id $facility.facilityId
    }
}

if ($script:Summary.Count -gt 0) {
    Write-Host ''
    $script:Summary | Format-Table -AutoSize | Out-String | Write-Host
}
