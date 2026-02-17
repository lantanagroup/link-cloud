@echo off
SETLOCAL

::: =========================================================================================
::: Clean Docker Environment Script
::: 
::: This script runs clean.py with pre-configured parameters for the local 
::: docker-compose environment.
:::
::: Usage:
:::   clean-docker.bat [extra_args]
:::
::: Example:
:::   clean-docker.bat --drop-tables
:::   clean-docker.bat --bypass-prompt --drop-tables
::: =========================================================================================

::: Check for Python
where python >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Python not found in PATH. Please install Python to run this script.
    exit /b 1
)

echo [INFO] Running clean.py with docker-compose parameters...
echo.

python.exe "%~dp0clean.py" ^
  --db-connection-string "Server=localhost,1433;UID=sa;PWD=${LINK_DB_PASS};TrustServerCertificate=yes" ^
  --account-db-name "link-account" ^
  --audit-db-name "link-audit" ^
  --census-db-name "link-census" ^
  --data-acquisition-db-name "link-dataacquisition" ^
  --normalization-db-name "link-normalization" ^
  --query-dispatch-db-name "link-querydispatch" ^
  --submission-db-name "link-submission" ^
  --tenant-db-name "link-tenant" ^
  --validation-db-name "link-validation" ^
  --mongo-connection-string "mongodb://localhost:17017" ^
  --report-db-name "link-report" ^
  --measureeval-db-name "link-measureeval" ^
  --kafka-broker "localhost:9094" ^
  --kafka-username "${KAFKA_SASL_CLIENT_USER}" ^
  --kafka-password "${KAFKA_SASL_CLIENT_PASSWORD}" ^
  --kafka-security-protocol SASL_PLAINTEXT ^
  --kafka-sasl-mechanism PLAIN ^
  --redis-host "localhost" ^
  --redis-port 6379 ^
  --redis-password "${REDIS_PASS}" ^
  %*

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Script failed with exit code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo.
echo [SUCCESS] Clean operation completed.
ENDLOCAL
