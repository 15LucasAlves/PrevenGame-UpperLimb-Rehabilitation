@echo off
rem ============================================================================
rem  Restaura o comportamento NORMAL do sensor de proximidade do Quest
rem  (volta a adormecer quando e tirado da cara).
rem  Par do manter-oculos-acordados.bat.
rem ============================================================================

where adb >nul 2>nul
if errorlevel 1 (
    echo [ERRO] adb nao encontrado no PATH.
    pause
    exit /b 1
)

echo A restaurar o sensor de proximidade...
adb shell am broadcast -a com.oculus.vrpowermanager.automation_enable

echo.
echo [OK] Comportamento normal restaurado (tambem podes simplesmente reiniciar o Quest).
pause
