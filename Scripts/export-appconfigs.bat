@echo off
REM Check if the correct number of arguments are provided
IF "%~1"=="" (
    echo Usage: export-appconfigs.bat ^<app-config-name^> ^<output-directory^>
    exit /b 1
)
IF "%~2"=="" (
    echo Usage: export-appconfigs.bat ^<app-config-name^> ^<output-directory^>
    exit /b 1
)

SET app_config_name=%~1
SET output_directory=%~2
SET timestamp=%date:~10,4%%date:~4,2%%date:~7,2%_%time:~0,2%%time:~3,2%%time:~6,2%

REM Replace spaces with leading zero in time format
SET timestamp=%timestamp: =0%

SET labels=Account Audit Census DataAcquisition LinkAdminBFF MeasureEval Normalization Notification QueryDispatch Report Submission Tenant Validation

REM Export without any label
SET output_file=%output_directory%\%app_config_name%_nolabel_%timestamp%.json
echo Exporting app configuration without any label...
call az appconfig kv export -n %app_config_name% -d file --path %output_file% --format json --yes

IF %ERRORLEVEL% NEQ 0 (
    echo Export without label failed
    exit /b 1
)

echo Export successful: %output_file%

REM Enable delayed variable expansion
SETLOCAL ENABLEDELAYEDEXPANSION

REM Loop through each label and export configurations
FOR %%L IN (%labels%) DO (
    SET label=%%L
    SET output_file=%output_directory%\%app_config_name%_!label!_%timestamp%.json
    echo Exporting app configuration with label !label!...
    call az appconfig kv export -n %app_config_name% -d file --path !output_file! --format json --label !label! --yes
    
    IF !ERRORLEVEL! NEQ 0 (
        echo Export with label !label! failed
        exit /b 1
    )
    
    echo Export successful: !output_file!
)

ENDLOCAL

echo All exports completed successfully.
