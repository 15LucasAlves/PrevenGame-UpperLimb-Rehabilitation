@echo off
rem ============================================================================
rem  Mantém o Quest ACORDADO enquanto o jogador tira/põe os óculos.
rem
rem  Desativa o comportamento do sensor de proximidade via adb: o headset deixa
rem  de adormecer quando é tirado da cara (e por isso não volta a pedir a conta
rem  ao ser posto outra vez). Ideal para sessões de reabilitação.
rem
rem  Requisitos:
rem    - Quest ligado por cabo USB (Link) e com o modo de programador ativo
rem    - adb no PATH, ou instalado com o Meta Quest Developer Hub / platform-tools
rem    - Na primeira vez, aceitar o pedido "Allow USB debugging" DENTRO do headset
rem
rem  Reverter: correr restaurar-sono-oculos.bat (ou reiniciar o headset).
rem ============================================================================

where adb >nul 2>nul
if errorlevel 1 (
    echo [ERRO] adb nao encontrado no PATH.
    echo Instala o Meta Quest Developer Hub ou o Android platform-tools,
    echo ou corre isto a partir da pasta onde o adb.exe esta.
    pause
    exit /b 1
)

echo A verificar dispositivos adb...
adb devices

echo.
echo A desativar o sono por sensor de proximidade...
adb shell am broadcast -a com.oculus.vrpowermanager.prox_close
adb shell am broadcast -a com.oculus.vrpowermanager.automation_disable

echo.
echo [OK] O headset fica acordado mesmo quando o jogador o tira.
echo      Para voltar ao normal: restaurar-sono-oculos.bat ou reiniciar o Quest.
pause
