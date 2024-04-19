@echo off

REM Check if the correct number of parameters is passed
if "%~3"=="" (
    echo Usage: %0 CONTAINER_NAME [rep|rev] ID
    exit /b 1
)

REM Set the values of parameters
set CONTAINER_NAME=%1
set ARG_TYPE=%2
set ARG_VALUE=%3

REM Check if the argument type is 'replica' or 'rev'
if /i "%ARG_TYPE%"=="rep" (
    set ARG_OPTION=--replica
    set ARG_VALUE=%3
) else if /i "%ARG_TYPE%"=="rev" (
    set ARG_OPTION=--revision
    set ARG_VALUE=%3
) else (
    set ARG_OPTION=
    set ARG_VALUE=%2
)

REM Run the command
az containerapp logs show --follow --tail 300 -n %CONTAINER_NAME% %ARG_OPTION% %ARG_VALUE% --format text
