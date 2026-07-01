using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Net.Http;
using Grpc.Net.Client;

public class OmmoDeviceRow : MonoBehaviour
{
    public Image           DeviceIcon;
    public TextMeshProUGUI DeviceNameText;
    public TextMeshProUGUI DeviceUUIDText;
    public Image           StatusDot;
    public TextMeshProUGUI StatusText;
    public TextMeshProUGUI PPSText;
    public TextMeshProUGUI ChannelInfoText;
    public Button          ActionButton;
    public TextMeshProUGUI ActionButtonText;

    static readonly Color ColorRunning = new Color(0.18f, 0.80f, 0.18f);
    static readonly Color ColorIdle    = new Color(0.50f, 0.50f, 0.50f);
    static readonly Color ColorError   = new Color(0.90f, 0.15f, 0.15f);

    private OmmoHardwareMonitor.DeviceInfo _deviceInfo;
    private OmmoHardwareMonitor            _monitor;
    private bool                           _listenerAdded = false;

    public void SetMonitor(OmmoHardwareMonitor monitor) { _monitor = monitor; }

    public void Populate(OmmoHardwareMonitor.DeviceInfo d)
    {
        _deviceInfo = d;

        if (DeviceNameText) DeviceNameText.text = d.Name;
        if (DeviceUUIDText) DeviceUUIDText.text  = d.UUID;
        if (PPSText)        PPSText.text         = d.PPS > 0 ? $"{d.PPS} pps" : "";

        string status = d.Status ?? "Idle";
        if (StatusText) StatusText.text = status;
        if (StatusDot)  StatusDot.color = status == "Running" ? ColorRunning
                                        : status == "Error"   ? ColorError
                                        : ColorIdle;

        if (ChannelInfoText)
            ChannelInfoText.text = d.IsBaseStation
                ? $"Sync Ch: {d.SyncChannel ?? "—"}"
                : $"Data Ch: {d.DataChannel ?? "Not Set"}   Sync Ch: {d.SyncChannel ?? "—"}";

        if (ActionButton && ActionButtonText)
        {
            if (d.IsBaseStation)
            {
                ActionButton.gameObject.SetActive(true);
                ActionButtonText.text     = d.IsRunning ? "Stop Motor" : "Start Motor";
                ActionButton.interactable = true;
                if (!_listenerAdded)
                {
                    ActionButton.onClick.AddListener(OnMotorButtonClicked);
                    _listenerAdded = true;
                }
            }
            else if (d.IsSIU)
            {
                ActionButton.gameObject.SetActive(true);
                bool isBlocked = d.Section == OmmoHardwareMonitor.DeviceSection.Blocked;
                ActionButtonText.text     = isBlocked ? "Unblock" : "Block";
                ActionButton.interactable = true;
                if (!_listenerAdded)
                {
                    ActionButton.onClick.AddListener(OnBlockButtonClicked);
                    _listenerAdded = true;
                }
            }
            else
            {
                ActionButton.gameObject.SetActive(false);
            }
        }
    }

    // ── Motor ─────────────────────────────────────────────────────────
    void OnMotorButtonClicked()
    {
        if (_deviceInfo == null) return;
        bool enable = !_deviceInfo.IsRunning;
        ActionButton.interactable = false;
        ActionButtonText.text     = "A aguardar...";

        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var handler  = new YetAnotherHttpHandler { Http2Only = true };
                var channel  = GrpcChannel.ForAddress("http://localhost:50051",
                                   new GrpcChannelOptions { HttpHandler = handler });
                var client   = new Ommo.CoreService.CoreServiceClient(channel);
                bool success = client.SetBaseStationMotorRunning(enable);
                channel.ShutdownAsync().Wait();

                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    ActionButton.interactable = true;
                    if (success)
                    {
                        _deviceInfo.IsRunning = enable;
                        _monitor?.SetMotorRunningState(_deviceInfo.UUID, enable);
                        ActionButtonText.text = enable ? "Stop Motor" : "Start Motor";
                    }
                    else
                    {
                        ActionButtonText.text = _deviceInfo.IsRunning ? "Stop Motor" : "Start Motor";
                        Debug.LogWarning("[OmmoDeviceRow] Motor command falhou.");
                    }
                });
            }
            catch (System.Exception e)
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    ActionButton.interactable = true;
                    ActionButtonText.text     = _deviceInfo.IsRunning ? "Stop Motor" : "Start Motor";
                    Debug.LogError("[OmmoDeviceRow] Erro motor: " + e.Message);
                });
            }
        });
    }

    // ── Block / Unblock SIU ───────────────────────────────────────────
    void OnBlockButtonClicked()
    {
        if (_deviceInfo == null || _monitor == null) return;
        bool isBlocked = _deviceInfo.Section == OmmoHardwareMonitor.DeviceSection.Blocked;
        bool block     = !isBlocked;

        _monitor.SetBlocked(_deviceInfo.UUID, block);
        Debug.Log($"[OmmoDeviceRow] {(block ? "Blocked" : "Unblocked")} SIU: {_deviceInfo.UUID}");
    }
}
