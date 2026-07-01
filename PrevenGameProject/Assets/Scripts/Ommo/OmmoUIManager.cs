using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class OmmoUIManager : MonoBehaviour
{
    [Header("── Panels ──────────────────────────")]
    public Canvas MainCanvas;
    public Camera GridCamera;
    public Canvas HUDCanvas;

    [Header("── Hardware Panel Header ──────────────")]
    public TextMeshProUGUI ServiceStatusText;
    public TextMeshProUGUI GrpcPortText;
    public TextMeshProUGUI ErrorsText;
    public Button StartPairingButton;
    public Button ResetWirelessButton;

    [Header("── Device Lists ─────────────────────")]
    public Transform ConnectedContainer;
    public Transform DisconnectedContainer;
    public Transform BlockedContainer;
    public GameObject DeviceRowPrefab;

    [Header("── Section Labels ───────────────────")]
    public GameObject ConnectedSection;
    public GameObject DisconnectedSection;
    public GameObject BlockedSection;

    [Header("── Navegação ───────────────────────────")]
    public Button OpenViewButton;
    public Button BackButton;

    [Header("── HUD 3D ──────────────────────────────")]
    public TextMeshProUGUI HUDTopBarText;
    public TextMeshProUGUI HUDDeviceCountText;
    public TextMeshProUGUI HUDBaseStationText;
    public TextMeshProUGUI HUDDeviceInfoText;

    [Header("── Referências ─────────────────────────")]
    public OmmoHardwareMonitor HardwareMonitor;
    public OmmoGridVisualizer GridVisualizer;
    public OmmoDeviceManager DeviceManager;

    // Cache separado por secção
    private Dictionary<string, OmmoDeviceRow> _connectedCache = new Dictionary<string, OmmoDeviceRow>();
    private Dictionary<string, OmmoDeviceRow> _disconnectedCache = new Dictionary<string, OmmoDeviceRow>();
    private Dictionary<string, OmmoDeviceRow> _blockedCache = new Dictionary<string, OmmoDeviceRow>();

    void Start()
    {
        if (OpenViewButton) OpenViewButton.onClick.AddListener(ShowGridView);
        if (BackButton) BackButton.onClick.AddListener(ShowHardwarePanel);
        if (StartPairingButton) StartPairingButton.onClick.AddListener(() => Debug.Log("[Ommo] Start Pairing"));
        if (ResetWirelessButton) ResetWirelessButton.onClick.AddListener(() => Debug.Log("[Ommo] Reset Wireless"));

        ShowHardwarePanel();

        if (HardwareMonitor)
            HardwareMonitor.OnHardwareUpdated += RefreshHardwareUI;
    }

    void Update()
    {
        // Atualiza HUD em tempo real quando a vista 3D está ativa
        if (HUDCanvas != null && HUDCanvas.gameObject.activeSelf)
            RefreshHUDRealtime();
    }

    void RefreshHUDRealtime()
    {
        var devices = System.Array.FindAll(UnityEngine.Object.FindObjectsOfType<OmmoDevice>(), d => d.gameObject.activeInHierarchy);

        // Device count = numero de GameObjects OmmoDevice ativos (cada um é um dispositivo físico)
        if (HUDDeviceCountText)
            HUDDeviceCountText.text = "Device Count: " + devices.Length;

        if (HUDBaseStationText)
            HUDBaseStationText.text = "Base Station  X: 0.00  Y: 0.00  Z: 0.00  [Reference]";

        if (devices.Length == 0)
        {
            if (HUDTopBarText) HUDTopBarText.text = "0,000000   (UUID: 0) (Port: 0) (Sensor: 0)   Fusion Mode: Default";
            if (HUDDeviceInfoText) HUDDeviceInfoText.text = "";
            return;
        }

        // TopBar usa o primeiro sensor válido de qualquer dispositivo
        var dev0 = devices[0];
        Vector3 pos0 = Vector3.zero;
        for (int i = 0; i < dev0.transform.childCount; i++)
        {
            var c = dev0.transform.GetChild(i);
            if (c.position != Vector3.zero) { pos0 = c.position; break; }
        }

        if (HUDTopBarText)
            HUDTopBarText.text = (pos0.x * 10f).ToString("F6") +
                "   (UUID: " + dev0.name.Replace("OmmoDevice_", "") +
                ") (Port: 0) (Sensor: 0)   Fusion Mode: Default";

        // Lista todos os sensores de todos os dispositivos
        string info = "";
        int sIdx = 0;
        foreach (var dev in devices)
        {
            for (int i = 0; i < dev.transform.childCount && i < 4; i++)
            {
                var p = dev.transform.GetChild(i).position;
                if (p == Vector3.zero) continue;
                var q = dev.transform.GetChild(i).rotation;
                info += "S" + sIdx + " X: " + (p.x * 10f).ToString("F2") +
                        "  Y: " + (p.y * 10f).ToString("F2") +
                        "  Z: " + (p.z * 10f).ToString("F2") + System.Environment.NewLine;
                info += "S" + sIdx + " W: " + q.w.ToString("F2") +
                        "  X:" + q.x.ToString("F2") +
                        "  Y: " + q.y.ToString("F2") +
                        "  Z: " + q.z.ToString("F2") + System.Environment.NewLine;
                sIdx++;
            }
        }
        if (HUDDeviceInfoText) HUDDeviceInfoText.text = info.TrimEnd();
    }

    void OnDestroy()
    {
        if (HardwareMonitor)
            HardwareMonitor.OnHardwareUpdated -= RefreshHardwareUI;
    }

    public void ShowGridView()
    {
        if (MainCanvas) MainCanvas.gameObject.SetActive(false);
        if (GridCamera) GridCamera.enabled = true;
        if (HUDCanvas) HUDCanvas.gameObject.SetActive(true);
        if (GridVisualizer) GridVisualizer.enabled = true;

        if (DeviceManager) DeviceManager.StartTracking();
    }

    public void ShowHardwarePanel()
    {
        if (MainCanvas) MainCanvas.gameObject.SetActive(true);
        if (GridCamera) GridCamera.enabled = false;
        if (HUDCanvas) HUDCanvas.gameObject.SetActive(false);
        if (GridVisualizer) GridVisualizer.enabled = false;

        StopBaseStationMotor();
        if (DeviceManager) DeviceManager.StopTracking();
    }
    void StopBaseStationMotor()
    {
        if (HardwareMonitor == null) return;
        foreach (var d in HardwareMonitor.CurrentInfo.Connected)
        {
            if (!d.IsBaseStation) continue;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var handler = new Cysharp.Net.Http.YetAnotherHttpHandler { Http2Only = true };
                    var channel = Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost:50051",
                                      new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = handler });
                    var client = new Ommo.CoreService.CoreServiceClient(channel);
                    client.SetBaseStationMotorRunning(false);
                    channel.ShutdownAsync().Wait();
                    string uuid = d.UUID;
                    UnityMainThreadDispatcher.Enqueue(() => {
                        // Força Idle imediatamente no UI
                        HardwareMonitor.SetMotorRunningState(uuid, false);
                        foreach (var dev in HardwareMonitor.CurrentInfo.Connected)
                            if (dev.UUID == uuid) { dev.Status = "Idle"; dev.IsRunning = false; }
                        RefreshHardwareUI(HardwareMonitor.CurrentInfo);

                    });
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("[UIManager] Stop motor error: " + e.Message);
                }
            });
        }
    }

    public void RefreshHardwareUI(OmmoHardwareMonitor.ServiceInfo info)
    {
        // Header
        if (ServiceStatusText)
        {
            ServiceStatusText.text = info.IsConnected ? "Running" : "Disconnected";
            ServiceStatusText.color = info.IsConnected
                ? new Color(0.18f, 0.75f, 0.18f)
                : new Color(0.85f, 0.2f, 0.2f);
        }
        if (GrpcPortText) GrpcPortText.text = "localhost:50051";
        if (ErrorsText) ErrorsText.text = info.ErrorCount.ToString();

        // Atualiza cada secção
        UpdateSection(info.Connected, ConnectedContainer, _connectedCache, ConnectedSection);
        UpdateSection(info.Disconnected, DisconnectedContainer, _disconnectedCache, DisconnectedSection);
        UpdateSection(info.Blocked, BlockedContainer, _blockedCache, BlockedSection);

        RefreshHUD(info);
    }

    void UpdateSection(
        List<OmmoHardwareMonitor.DeviceInfo> devices,
        Transform container,
        Dictionary<string, OmmoDeviceRow> cache,
        GameObject sectionLabel)
    {
        if (container == null || DeviceRowPrefab == null) return;

        // Mostra/esconde secção consoante tem dispositivos
        if (sectionLabel) sectionLabel.SetActive(devices.Count > 0);

        var activeKeys = new HashSet<string>();

        foreach (var device in devices)
        {
            string key = device.UUID + "_" + device.Name;
            activeKeys.Add(key);

            if (cache.TryGetValue(key, out var existingRow))
            {
                existingRow.Populate(device);
            }
            else
            {
                var row = Instantiate(DeviceRowPrefab, container);
                row.SetActive(true);
                var rowCtrl = row.GetComponent<OmmoDeviceRow>();
                if (rowCtrl)
                {
                    rowCtrl.SetMonitor(HardwareMonitor);
                    rowCtrl.Populate(device);
                    cache[key] = rowCtrl;
                }
            }
        }

        // Remove rows de dispositivos que saíram desta secção
        var toRemove = new List<string>();
        foreach (var kv in cache)
        {
            if (!activeKeys.Contains(kv.Key))
            {
                Destroy(kv.Value.gameObject);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var k in toRemove) cache.Remove(k);
    }

    void RefreshHUD(OmmoHardwareMonitor.ServiceInfo info)
    {
        int total = info.Connected.Count + info.Disconnected.Count;
        if (HUDDeviceCountText)
            HUDDeviceCountText.text = $"Device Count: {total}";

        if (HUDBaseStationText)
            HUDBaseStationText.text = "Base Station  X: 0.00  Y: 0.00  Z: 0.00  [Reference]";

        if (HUDTopBarText)
            HUDTopBarText.text = info.Connected.Count > 0
                ? $"(UUID: {info.Connected[0].UUID}) (Port: 0) (Sensor: 0)   Fusion Mode: Default"
                : "0,000000   (UUID: 0) (Port: 0) (Sensor: 0)   Fusion Mode: Default";

        if (HUDDeviceInfoText)
            HUDDeviceInfoText.text = info.Connected.Count > 0
                ? $"UUID: {info.Connected[0].UUID}  Port: 0"
                : "UUID: —  Port: —  [No Device]";
    }
}