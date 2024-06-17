@echo off
setlocal enabledelayedexpansion

:: Check if the correct number of arguments are provided
if "%~3"=="" (
    echo Usage: %~nx0 REST_PROXY_URL USERNAME PASSWORD
    exit /b 1
)

:: Assign arguments to variables
set REST_PROXY_URL=%~1
set USERNAME=%~2
set PASSWORD=%~3

:: Kafka configuration
set PARTITIONS=1
set REPLICATION_FACTOR=1

:: List of topics to create
set TOPICS=ReportScheduled,ReportScheduled-Error,ReportScheduled-Retry,RetentionCheckScheduled,RetentionCheckScheduled-Error,RetentionCheckScheduled-Retry,PatientCensusScheduled,PatientCensusScheduled-Error,PatientCensusScheduled-Retry,PatientEvent,PatientEvent-Error,PatientEvent-Retry,DataAcquisitionRequested,DataAcquisitionRequested-Error,DataAcquisitionRequested-Retry,PatientIDsAcquired,PatientIDsAcquired-Error,PatientIDsAcquired-Retry,ResourceAcquired,ResourceAcquired-Error,ResourceAcquired-Retry,ResourceNormalized,ResourceNormalized-Error,ResourceNormalized-Retry,ResourceEvaluated,ResourceEvaluated-Error,ResourceEvaluated-Retry,SubmitReport,SubmitReport-Error,SubmitReport-Retry,ReportSubmitted,ReportSubmitted-Error,ReportSubmitted-Retry,NotificationRequested,NotificationRequested-Error,NotificationRequested-Retry,AuditableEventOccurred,AuditableEventOccurred-Error,AuditableEventOccurred-Retry

:: Split topics into an array
set "TOPICS_ARRAY="
for %%A in (%TOPICS%) do (
    set "TOPICS_ARRAY=!TOPICS_ARRAY! %%A"
)

:: Check if topic exists and create if it does not
for %%T in (%TOPICS_ARRAY%) do (
    echo Checking if topic %%T exists
    curl -s -o /dev/null -w "%%{http_code}" -X GET "%REST_PROXY_URL%/topics/%%T" ^
        -u %USERNAME%:%PASSWORD% | findstr /r /c:"404"

    if errorlevel 1 (
        echo Topic %%T already exists.
    ) else (
        echo Creating topic: %%T
        curl -X POST "%REST_PROXY_URL%/topics/%%T" ^
            -H "Content-Type: application/vnd.kafka.v2+json" ^
            -u %USERNAME%:%PASSWORD% ^
            -d "{\"partitions\": %PARTITIONS%, \"replication_factor\": %REPLICATION_FACTOR%}"
    )
    echo.
)

endlocal
