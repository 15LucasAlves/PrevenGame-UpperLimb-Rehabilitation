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
                // Minimizada: o serviço arranca para trás (barra de tarefas) em vez de
                // aparecer em frente ao jogo/main menu. Não afeta o gRPC.
                WindowStyle = ProcessWindowStyle.Minimized
            });
            UnityEngine.Debug.Log($"[OmmoLauncher] Lançado (PID {_launchedProcess.Id}). Warmup: {WarmupSeconds}s");
            StartCoroutine(WarmupThenReady(WarmupSeconds));
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // A app Qt do serviço mostra o seu próprio splash e ignora o WindowStyle. Escondemos
            // as janelas do processo durante os primeiros segundos para o splash não aparecer à
            // frente do jogo (o serviço continua a correr e vai para o system tray).
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
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    IEnumerator EsconderJanelasServico(int pid)
    {
        // O splash pode surgir com atraso e mudar de janela — repetimos durante alguns segundos.
        float t = 0f;
        while (t < 6f)
        {
            EsconderJanelasDoProcesso(pid);
            t += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }
    }

    static void EsconderJanelasDoProcesso(int pid)
    {
        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint janelaPid);
            if (janelaPid == (uint)pid) ShowWindow(hWnd, SW_HIDE);
            return true; // continua a enumerar
        }, IntPtr.Zero);
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