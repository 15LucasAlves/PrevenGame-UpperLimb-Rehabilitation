using UnityEngine;
using System.Diagnostics;
using System.IO;
using System;
using System.Collections;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

/// <summary>
/// OmmoServiceLauncher - Lança o OmmoService.exe e só inicializa o cliente gRPC
/// depois do warmup. Usa um evento estático para sinalizar que o serviço está pronto.
/// </summary>
public class OmmoServiceLauncher : MonoBehaviour
{
    [Header("Configuração")]
    public string ServiceExeName = "ommo_service_v0.22.0.exe";
    public float WarmupSeconds = 2.5f;
    public bool KillOnExit = true;

    // Evento que outros scripts escutam para saber quando o serviço está pronto
    public static event Action OnServiceReady;
    public static bool ServiceReady { get; private set; } = false;

    private Process _launchedProcess;
    private bool _wasAlreadyRunning = false;

    void Start()
    {
        ServiceReady = false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // Janela do jogo/editor, para devolver o foco se o serviço o roubar ao arrancar.
        _janelaJogo = GetActiveWindow();
#endif
        string exePath = FindServicePath();

        if (string.IsNullOrEmpty(exePath))
        {
            UnityEngine.Debug.LogError($"[OmmoLauncher] Ficheiro não encontrado: {ServiceExeName}");
            // Tenta mesmo assim — pode já estar a correr
            StartCoroutine(WarmupThenReady(0f));
            return;
        }

        if (IsServiceRunning())
        {
            UnityEngine.Debug.Log("[OmmoLauncher] Serviço já está a correr.");
            _wasAlreadyRunning = true;
            StartCoroutine(WarmupThenReady(0.5f)); // pequeno delay para garantir
            return;
        }

        LaunchService(exePath);
    }

    void OnDestroy()
    {
        ServiceReady = false;
        if (KillOnExit && !_wasAlreadyRunning && _launchedProcess != null)
        {
            try
            {
                if (!_launchedProcess.HasExited)
                {
                    UnityEngine.Debug.Log("[OmmoLauncher] A fechar o serviço...");
                    _launchedProcess.Kill();
                    _launchedProcess.WaitForExit(3000);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[OmmoLauncher] Erro ao fechar: " + e.Message);
            }
            finally
            {
                _launchedProcess?.Dispose();
                _launchedProcess = null;
            }
        }
    }

    void LaunchService(string exePath)
    {
        try
        {
            UnityEngine.Debug.Log($"[OmmoLauncher] A lançar: {exePath}");
            _launchedProcess = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                // Hidden: o Qt respeita o STARTUPINFO no primeiro show da janela principal
                // (SW_SHOWDEFAULT), por isso na maioria dos arranques o splash nem chega a
                // aparecer. Não afeta o gRPC nem o ícone no system tray.
                WindowStyle = ProcessWindowStyle.Hidden
            });
            UnityEngine.Debug.Log($"[OmmoLauncher] Lançado (PID {_launchedProcess.Id}). Warmup: {WarmupSeconds}s");
            StartCoroutine(WarmupThenReady(WarmupSeconds));
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Rede de segurança: janelas que o Qt mostre à força (splash/main window) são
            // escondidas no próprio frame em que aparecem, e o foco volta ao jogo. O serviço
            // continua a correr e fica acessível pelo ícone do system tray.
            StartCoroutine(EsconderJanelasServico(_launchedProcess.Id));
#endif
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("[OmmoLauncher] Falha ao lançar: " + e.Message);
            StartCoroutine(WarmupThenReady(1f));
        }
    }

    IEnumerator WarmupThenReady(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        UnityEngine.Debug.Log("[OmmoLauncher] Serviço pronto — a inicializar cliente gRPC...");
        ServiceReady = true;
        OnServiceReady?.Invoke();
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // ── Esconder as janelas do serviço Ommo (splash Qt) ───────────────
    private const int SW_HIDE = 0;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // Delegate cacheado (evita alocar um novo por frame) + estado da enumeração.
    private static readonly EnumWindowsProc _enumProc = JanelaEnumerada;
    private static uint _pidAlvo;
    private static bool _escondeuAlguma;
    private static IntPtr _janelaJogo;

    IEnumerator EsconderJanelasServico(int pid)
    {
        // O splash pode surgir a qualquer momento durante o arranque — verificamos TODOS os
        // frames nos primeiros segundos, para nenhuma janela ficar visível mais de 1 frame.
        float fim = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < fim)
        {
            if (EsconderJanelasDoProcesso(pid) && _janelaJogo != IntPtr.Zero)
                SetForegroundWindow(_janelaJogo); // o serviço roubou o foco — devolve-o ao jogo
            yield return null;
        }
    }

    /// <summary>Esconde as janelas visíveis do processo. True se escondeu alguma.</summary>
    static bool EsconderJanelasDoProcesso(int pid)
    {
        _pidAlvo        = (uint)pid;
        _escondeuAlguma = false;
        EnumWindows(_enumProc, IntPtr.Zero);
        return _escondeuAlguma;
    }

    static bool JanelaEnumerada(IntPtr hWnd, IntPtr lParam)
    {
        GetWindowThreadProcessId(hWnd, out uint janelaPid);
        if (janelaPid == _pidAlvo && IsWindowVisible(hWnd))
        {
            ShowWindow(hWnd, SW_HIDE);
            _escondeuAlguma = true;
        }
        return true; // continua a enumerar
    }
#endif

    bool IsServiceRunning()
    {
        string procName = Path.GetFileNameWithoutExtension(ServiceExeName);
        return Process.GetProcessesByName(procName).Length > 0;
    }

    string FindServicePath()
    {
        // 1. Mesmo diretório do .exe (standalone)
        string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        string c = Path.Combine(exeDir, ServiceExeName);
        if (File.Exists(c)) return c;

        // 2. Assets/Plugins/ommo.sdk/ (Editor)
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        c = Path.Combine(projectRoot, "Assets", "Plugins", "ommo.sdk", ServiceExeName);
        if (File.Exists(c)) return c;

        // 3. StreamingAssets
        c = Path.Combine(Application.streamingAssetsPath, ServiceExeName);
        if (File.Exists(c)) return c;

        return null;
    }
}