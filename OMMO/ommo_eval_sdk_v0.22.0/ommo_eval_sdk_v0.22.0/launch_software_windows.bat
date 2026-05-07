@echo off
setlocal

REM === Get the directory this script is in ===
set "ROOT_DIR=%~dp0"

REM === Locate the service exe ===
for %%F in ("%ROOT_DIR%ommo_service\ommo_service_cl*.exe") do set "SERVICE_EXE=%%~nxF"

REM == Check if service is already running ===
tasklist | findstr /I "ommo_service_" >nul

if errorlevel 1 (
    echo Starting %SERVICE_EXE%...
    start "" "%ROOT_DIR%ommo_service\%SERVICE_EXE%"
) else (
    echo Service is already running, skipping...
)

REM === Check if ommo app is already running ===
tasklist | findstr /I "\<ommo_app.exe\>" >nul

if errorlevel 1 (
    echo Starting ommo_app.exe...
    start "" "%ROOT_DIR%ommo_app\ommo_app.exe"
) else (
    echo ommo app is already running, skipping...
)

endlocal
