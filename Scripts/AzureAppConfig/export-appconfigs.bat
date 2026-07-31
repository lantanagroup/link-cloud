@echo off
REM Export an Azure App Configuration store to the committed Config/app-config.<env>.json shape.
REM
REM   export-appconfigs.bat <app-config-name> <output-file> [auth-mode]
REM
REM   export-appconfigs.bat nhsnlink-ac-dev  Config\app-config.dev.json
REM   export-appconfigs.bat nhnslink-ac-qa   Config\app-config.qa.json   login
REM
REM Two flags below are load-bearing and were both missing before:
REM
REM   --profile appconfig/kvset
REM       Produces the { "items": [ ... ] } shape with key/value/label/content_type/tags per
REM       row. The default profile writes a nested configuration tree with no labels at all,
REM       which is not what Config/app-config.*.json contains and cannot be round-tripped.
REM
REM   --label "*"
REM       Exports every label. Omitting it exports ONLY rows with no label. The previous
REM       version of this script iterated a hardcoded label list that was missing
REM       DataAcquisitionWorker, "Link Automation UI" and Terminology, so exports produced by
REM       it silently dropped those rows -- and re-importing such a file with --strict would
REM       have deleted them from the store. The asterisk is only accepted alongside the kvset
REM       profile, which is why the two go together.
REM
REM For the import direction use the same profile, so label, content_type and tags are read
REM from the file rather than the command line:
REM
REM   az appconfig kv import -n <store> -s file --path <file> --format json ^
REM       --profile appconfig/kvset --yes
REM
REM Import is additive by default: rows deleted from the file are NOT removed from the store
REM unless you add --strict, which makes the store match the file exactly.

IF "%~1"=="" GOTO :usage
IF "%~2"=="" GOTO :usage

SET app_config_name=%~1
SET output_file=%~2
SET auth_mode=%~3
IF "%auth_mode%"=="" SET auth_mode=login

echo Exporting %app_config_name% to %output_file% ...

call az appconfig kv export ^
    --name %app_config_name% ^
    --destination file ^
    --path "%output_file%" ^
    --format json ^
    --profile appconfig/kvset ^
    --label "*" ^
    --auth-mode %auth_mode% ^
    --yes

IF %ERRORLEVEL% NEQ 0 (
    echo Export failed.
    exit /b 1
)

echo Export successful: %output_file%
echo.
echo Now verify before committing:
echo     python Scripts/AzureAppConfig/validate_aac_secrets.py "%output_file%" --strict
exit /b 0

:usage
echo Usage: export-appconfigs.bat ^<app-config-name^> ^<output-file^> [auth-mode]
echo.
echo   app-config-name  Store name, e.g. nhsnlink-ac-dev
echo   output-file      Destination path, e.g. Config\app-config.dev.json
echo   auth-mode        "login" (default) or "key"
exit /b 1
