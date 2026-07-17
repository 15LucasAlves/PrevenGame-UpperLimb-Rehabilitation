@echo off
rem ============================================================================
rem  Reinicia o runtime da Meta no PC (OVRService).
rem
rem  Quando uma sessao Unity crasha/e morta, os servidores IPC da Meta
rem  (anchor_persistence_server, SlamAnchorServer, ...) ficam num estado zombie
rem  e a sessao seguinte CONGELA na splash com loops de
rem  "WaitForServerFinalize FAILED in 5000ms" no Editor.log.
rem  Reiniciar o OVRService limpa-os sem precisar de reiniciar o PC.
rem
rem  COMO USAR (apos um crash do Unity):
rem    1. Fechar o Unity e a app Meta Quest Link (tray -> Quit)
rem    2. Correr ESTE script como ADMINISTRADOR
rem    3. Reabrir: app do Link -> ligar o Link no headset -> Unity
rem ============================================================================

net session >nul 2>&1
if errorlevel 1 (
    echo [ERRO] Corre como Administrador: botao direito -^> "Executar como administrador".
    pause
    exit /b 1
)

echo A parar o OVRService (runtime da Meta)...
net stop OVRService
echo.
echo A arrancar o OVRService...
net start OVRService
echo.
echo [OK] Runtime reiniciado. Abre a app Meta Quest Link, ativa o Link no
echo      headset, e SO DEPOIS abre o Unity.
pause
