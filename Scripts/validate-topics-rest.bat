@echo off
setlocal enabledelayedexpansion

:: Check if at least 3 arguments are provided
if "%~3"=="" (
    echo Usage: %~nx0 REST_PROXY_URL USERNAME PASSWORD [TOPICS_FILE]
    exit /b 1
)

:: Assign required arguments
set REST_PROXY_URL=%~1
set USERNAME=%~2
set PASSWORD=%~3

:: Optional topic file path
set TOPICS_FILE=topics.txt
if not "%~4"=="" set TOPICS_FILE=%~4

:: === Get cluster ID ===
for /f "usebackq delims=" %%A in (`curl -s %REST_PROXY_URL%/v3/clusters`) do (
    set "LINE=%%A"
    set "JSON=!LINE!"
)

:: Extract cluster_id from inline JSON
set CLUSTER_ID=

for %%A in (!JSON!) do (
    echo %%A | findstr /c:"cluster_id" >nul
    if !errorlevel! == 0 (
        set "LINE=%%A"
        for /f "tokens=2 delims=:" %%B in ("!LINE!") do (
            set "TMP=%%B"
        )
    )
)

:: Clean up cluster ID
for /f "tokens=1 delims=,}" %%C in ("!TMP!") do (
    set "CLUSTER_ID=%%C"
)
set "CLUSTER_ID=!CLUSTER_ID:"=!"

echo Detected Kafka Cluster ID: !CLUSTER_ID!
echo Using topics file: %TOPICS_FILE%
echo.

:: === Load topics from file ===
if not exist "%TOPICS_FILE%" (
    echo ERROR: Topics file not found at: %TOPICS_FILE%
    exit /b 1
)

echo Checking topics existence...
echo.

:: Each line: <topic>:<partitions>:<replicas>
for /f "usebackq tokens=1,2,3 delims=:" %%A in ("%TOPICS_FILE%") do (
    set "TOPIC=%%A"

    echo Checking if topic !TOPIC! exists...

    for /f %%R in ('curl -s -o nul -w "%%{http_code}" -X GET "%REST_PROXY_URL%/topics/!TOPIC!" -u %USERNAME%:%PASSWORD%') do (
        set "STATUS_CODE=%%R"
    )

    if not "!STATUS_CODE!"=="404" (
        echo [EXISTS] Topic !TOPIC! is present in the cluster
    ) else (
        echo [MISSING] Topic !TOPIC! does not exist in the cluster
        echo !TOPIC!>> "%TEMP%\missing_topics.tmp"
    )
    echo.
)

echo Topic validation complete.
echo.
echo ========== MISSING TOPICS SUMMARY ==========
if exist "%TEMP%\missing_topics.tmp" (
    echo The following topics are missing:
    echo.
    type "%TEMP%\missing_topics.tmp"
    del "%TEMP%\missing_topics.tmp"
) else (
    echo All topics exist in the cluster
)
echo =========================================
endlocal
