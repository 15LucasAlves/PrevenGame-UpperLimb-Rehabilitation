using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

/// <summary>
/// BLEManager — wrapper C# para o BLEDll.dll via P/Invoke.
/// Singleton na Manager Scene — persiste durante toda a sessão.
///
/// IMPORTANTE (domain reload): antes de qualquer recompilação de scripts
/// no editor, chamamos DLL_UnregisterAllCallbacks() para que o DLL nativo
/// deixe de ter ponteiros para delegates managed que vão ser destruídos.
/// Sem isto, editar um script durante o Play mode crasha o editor.
/// </summary>
public class BLEManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────
    public static BLEManager Instance { get; private set; }

    // ── P/Invoke ──────────────────────────────────────────────────
    private const string DLL = "BLEDll";

    [DllImport(DLL)] private static extern void DLL_InitializeBLE();
    [DllImport(DLL)] private static extern void DLL_StartScan();
    [DllImport(DLL)] private static extern void DLL_StopScan();
    [DllImport(DLL)] private static extern void DLL_ConnectToDevice(ulong address);
    [DllImport(DLL)] private static extern void DLL_DisconnectDevice(ulong address);
    [DllImport(DLL)] private static extern bool DLL_IsDeviceConnected(ulong address);
    [DllImport(DLL)] private static extern bool DLL_SendData(ulong address, string data);
    [DllImport(DLL)] private static extern void DLL_Cleanup();
    [DllImport(DLL)] private static extern int DLL_GetConnectedDeviceCount();
    [DllImport(DLL)] private static extern void DLL_UnregisterAllCallbacks();
    [DllImport(DLL)] private static extern void DLL_SetVerboseLogging(bool enabled);

    [DllImport(DLL)] private static extern void DLL_RegisterLogCallback(LogCallback callback);
    [DllImport(DLL)] private static extern void DLL_RegisterDeviceFoundCallback(DeviceFoundCallback callback);
    [DllImport(DLL)] private static extern void DLL_RegisterDeviceConnectedCallback(DeviceConnectedCallback callback);
    [DllImport(DLL)] private static extern void DLL_RegisterDeviceDisconnectedCallback(DeviceDisconnectedCallback callback);
    [DllImport(DLL)] private static extern void DLL_RegisterDataReceivedCallback(DataReceivedCallback callback);
    [DllImport(DLL)] private static extern void DLL_RegisterRSSIUpdatedCallback(RSSIUpdatedCallback callback);
    [DllImport(DLL)] private static extern void DLL_RegisterScanStateChangedCallback(ScanStateChangedCallback callback);

    // ── Delegates (devem ser static para evitar GC) ───────────────
    private delegate void LogCallback(string message);
    private delegate void DeviceFoundCallback(string name, ulong address);
    private delegate void DeviceConnectedCallback(ulong address);
    private delegate void DeviceDisconnectedCallback(ulong address);
    private delegate void DataReceivedCallback(ulong address, string data);
    private delegate void RSSIUpdatedCallback(ulong address, int rssi);
    private delegate void ScanStateChangedCallback(bool isScanning);

    private static LogCallback _logCallback;
    private static DeviceFoundCallback _deviceFoundCallback;
    private static DeviceConnectedCallback _deviceConnectedCallback;
    private static DeviceDisconnectedCallback _deviceDisconnectedCallback;
    private static DataReceivedCallback _dataReceivedCallback;
    private static RSSIUpdatedCallback _rssiUpdatedCallback;
    private static ScanStateChangedCallback _scanStateChangedCallback;

    // ── Modelos de dados ──────────────────────────────────────────
    public class BLEDevice
    {
        public string Name;
        public ulong Address;
        public int RSSI;
        public bool IsConnected;
        public string LastData;
        public float Weight1;
        public float Weight2;

        public string AddressString => Address.ToString("X12");
        public string RSSIString => $"{RSSI} dBm";
    }

    // ── Eventos (disparados no main thread) ───────────────────────
    public event Action<BLEDevice> OnDeviceFound;
    public event Action<BLEDevice> OnDeviceConnected;
    public event Action<BLEDevice> OnDeviceDisconnected;
    public event Action<BLEDevice, float, float> OnSensorData;  // device, weight1, weight2
    public event Action<BLEDevice> OnRSSIUpdated;
    public event Action<bool> OnScanStateChanged;

    // ── Configuração ──────────────────────────────────────────────
    [Tooltip("Logs por-pacote do DLL nativo (dados UART, advertisements, envios). " +
             "Manter desligado em uso normal — só para debug de comunicação BLE.")]
    [SerializeField] private bool verboseNativeLogging = false;

    // ── Estado ────────────────────────────────────────────────────
    public bool IsScanning { get; private set; }
    public bool IsInitialized { get; private set; }

    private readonly Dictionary<ulong, BLEDevice> _devices = new Dictionary<ulong, BLEDevice>();
    public IReadOnlyDictionary<ulong, BLEDevice> Devices => _devices;

    // Queue para despachar callbacks para o main thread
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _mainThreadQueue
        = new System.Collections.Concurrent.ConcurrentQueue<Action>();

    // ── Unity ─────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        // Despacha callbacks do DLL para o main thread
        while (_mainThreadQueue.TryDequeue(out var action))
            action?.Invoke();
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        // Antes de QUALQUER domain reload (recompilação de scripts, incluindo
        // a meio do Play mode), corta as pontes nativo→managed no DLL.
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
    }

    private void OnDisable()
    {
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
    }

    private void HandleBeforeAssemblyReload()
    {
        Debug.Log("[BLEManager] Domain reload iminente — a desregistar callbacks nativos.");
        SafeUnregisterNativeCallbacks();
        Cleanup();
    }
#endif

    private void OnDestroy()
    {
        // OnApplicationQuit nem sempre corre no editor — OnDestroy é a rede
        // de segurança. Só limpa se formos a instância ativa.
        if (Instance == this)
            Cleanup();
    }

    private void OnApplicationQuit()
    {
        Cleanup();
    }

    // ── API pública ───────────────────────────────────────────────

    public void Initialize()
    {
        if (IsInitialized) return;

        // Registar callbacks (manter referências static para evitar GC)
        _logCallback = OnLog;
        _deviceFoundCallback = OnDeviceFoundNative;
        _deviceConnectedCallback = OnDeviceConnectedNative;
        _deviceDisconnectedCallback = OnDeviceDisconnectedNative;
        _dataReceivedCallback = OnDataReceivedNative;
        _rssiUpdatedCallback = OnRSSIUpdatedNative;
        _scanStateChangedCallback = OnScanStateChangedNative;

        DLL_RegisterLogCallback(_logCallback);
        DLL_RegisterDeviceFoundCallback(_deviceFoundCallback);
        DLL_RegisterDeviceConnectedCallback(_deviceConnectedCallback);
        DLL_RegisterDeviceDisconnectedCallback(_deviceDisconnectedCallback);
        DLL_RegisterDataReceivedCallback(_dataReceivedCallback);

        // Callbacks adicionais — só disponíveis após recompilar o DLL
        try { DLL_RegisterRSSIUpdatedCallback(_rssiUpdatedCallback); }
        catch (EntryPointNotFoundException) { Debug.LogWarning("[BLEManager] DLL antigo — RSSI callback não disponível. Recompila o DLL."); }

        try { DLL_RegisterScanStateChangedCallback(_scanStateChangedCallback); }
        catch (EntryPointNotFoundException) { Debug.LogWarning("[BLEManager] DLL antigo — ScanState callback não disponível. Recompila o DLL."); }

        // DLL_InitializeBLE também repõe a flag interna de shutdown do DLL,
        // reativando os callbacks depois de um domain reload.
        DLL_InitializeBLE();

        SetVerboseLogging(verboseNativeLogging);

        IsInitialized = true;
        Debug.Log("[BLEManager] Inicializado.");
    }

    /// <summary>
    /// Ativa/desativa os logs por-pacote do DLL nativo em runtime.
    /// Útil para diagnosticar problemas de comunicação BLE sem recompilar.
    /// </summary>
    public void SetVerboseLogging(bool enabled)
    {
        verboseNativeLogging = enabled;
        try { DLL_SetVerboseLogging(enabled); }
        catch (EntryPointNotFoundException)
        {
            if (enabled)
                Debug.LogWarning("[BLEManager] DLL antigo — DLL_SetVerboseLogging não disponível. Recompila o DLL.");
        }
    }

    public void StartScan()
    {
        if (!IsInitialized) Initialize();
        // Remove apenas dispositivos não conectados — mantém os conectados
        var toRemove = new List<ulong>();
        foreach (var kv in _devices)
            if (!kv.Value.IsConnected) toRemove.Add(kv.Key);
        foreach (var addr in toRemove) _devices.Remove(addr);
        DLL_StartScan();
    }

    public void StopScan()
    {
        DLL_StopScan();
    }

    public void Connect(ulong address)
    {
        DLL_ConnectToDevice(address);
    }

    public void Disconnect(ulong address)
    {
        DLL_DisconnectDevice(address);
    }

    public bool IsConnected(ulong address) => DLL_IsDeviceConnected(address);

    public bool SendData(ulong address, string data) => DLL_SendData(address, data);

    public int GetConnectedCount() => DLL_GetConnectedDeviceCount();

    public void Cleanup()
    {
        if (!IsInitialized) return;

        // 1. Cortar callbacks nativo→managed ANTES de tudo o resto —
        //    garante que nenhuma thread do DLL chama código managed
        //    durante o teardown.
        SafeUnregisterNativeCallbacks();

        // 2. Parar scan e fechar conexões
        try
        {
            DLL_StopScan();
            DLL_Cleanup();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BLEManager] Erro no cleanup nativo: {ex.Message}");
        }

        IsInitialized = false;
    }

    /// <summary>
    /// Desregista todos os callbacks no DLL nativo. Seguro chamar múltiplas
    /// vezes e com DLLs antigos (sem o export).
    /// </summary>
    private static void SafeUnregisterNativeCallbacks()
    {
        try
        {
            DLL_UnregisterAllCallbacks();
        }
        catch (EntryPointNotFoundException)
        {
            Debug.LogWarning("[BLEManager] DLL antigo — DLL_UnregisterAllCallbacks não disponível. " +
                             "Recompila o DLL para evitar crashes em domain reload.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BLEManager] Erro ao desregistar callbacks nativos: {ex.Message}");
        }
    }

    // ── Callbacks nativos (vêm de threads do DLL) ─────────────────

    [AOT.MonoPInvokeCallback(typeof(LogCallback))]
    private static void OnLog(string message)
    {
        Debug.Log(message);
    }

    [AOT.MonoPInvokeCallback(typeof(DeviceFoundCallback))]
    private static void OnDeviceFoundNative(string name, ulong address)
    {
        Instance?._mainThreadQueue.Enqueue(() =>
        {
            if (!Instance._devices.TryGetValue(address, out var device))
            {
                device = new BLEDevice { Name = name, Address = address };
                Instance._devices[address] = device;
            }
            else
            {
                if (!string.IsNullOrEmpty(name)) device.Name = name;
            }
            device.IsConnected = DLL_IsDeviceConnected(address);
            Instance.OnDeviceFound?.Invoke(device);
        });
    }

    [AOT.MonoPInvokeCallback(typeof(DeviceConnectedCallback))]
    private static void OnDeviceConnectedNative(ulong address)
    {
        Instance?._mainThreadQueue.Enqueue(() =>
        {
            if (!Instance._devices.TryGetValue(address, out var device))
                device = new BLEDevice { Address = address, Name = "Unknown" };
            device.IsConnected = true;
            Instance._devices[address] = device;
            Instance.OnDeviceConnected?.Invoke(device);
        });
    }

    [AOT.MonoPInvokeCallback(typeof(DeviceDisconnectedCallback))]
    private static void OnDeviceDisconnectedNative(ulong address)
    {
        Instance?._mainThreadQueue.Enqueue(() =>
        {
            if (Instance._devices.TryGetValue(address, out var device))
            {
                device.IsConnected = false;
                Instance.OnDeviceDisconnected?.Invoke(device);
            }
        });
    }

    [AOT.MonoPInvokeCallback(typeof(DataReceivedCallback))]
    private static void OnDataReceivedNative(ulong address, string data)
    {
        Instance?._mainThreadQueue.Enqueue(() =>
        {
            if (!Instance._devices.TryGetValue(address, out var device)) return;
            device.LastData = data;

            // Parse CSV: "weight1,weight2\n "
            var clean = data.Trim();
            var parts = clean.Split(',');
            if (parts.Length >= 1 && float.TryParse(parts[0],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float w1))
                device.Weight1 = w1;
            if (parts.Length >= 2 && float.TryParse(parts[1],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float w2))
                device.Weight2 = w2;

            Instance.OnSensorData?.Invoke(device, device.Weight1, device.Weight2);
        });
    }

    [AOT.MonoPInvokeCallback(typeof(RSSIUpdatedCallback))]
    private static void OnRSSIUpdatedNative(ulong address, int rssi)
    {
        Instance?._mainThreadQueue.Enqueue(() =>
        {
            if (!Instance._devices.TryGetValue(address, out var device)) return;
            device.RSSI = rssi;
            Instance.OnRSSIUpdated?.Invoke(device);
        });
    }

    [AOT.MonoPInvokeCallback(typeof(ScanStateChangedCallback))]
    private static void OnScanStateChangedNative(bool isScanning)
    {
        Instance?._mainThreadQueue.Enqueue(() =>
        {
            Instance.IsScanning = isScanning;
            Instance.OnScanStateChanged?.Invoke(isScanning);
        });
    }
}