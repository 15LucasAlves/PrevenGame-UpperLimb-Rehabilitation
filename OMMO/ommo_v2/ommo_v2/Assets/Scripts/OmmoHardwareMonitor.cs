using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Net.Http;
using Grpc.Net.Client;

public class OmmoHardwareMonitor : MonoBehaviour
{
    public enum DeviceSection { Connected, Disconnected, Blocked }

    public class DeviceInfo
    {
        public string Name;
        public string UUID;
        public string Status;
        public bool IsRunning;
        public int PPS;
        public string DataChannel;
        public string SyncChannel;
        public bool IsBaseStation;
        public bool IsSIU;
        public DeviceSection Section;
    }

    public class ServiceInfo
    {
        public bool IsConnected;
        public int ErrorCount;
        public List<DeviceInfo> Connected = new List<DeviceInfo>();
        public List<DeviceInfo> Disconnected = new List<DeviceInfo>();
        public List<DeviceInfo> Blocked = new List<DeviceInfo>();
    }

    public event Action<ServiceInfo> OnHardwareUpdated;
    public float PollInterval = 1.5f;

    private ServiceInfo _currentInfo = new ServiceInfo();
    private bool _polling = false;

    // Estado local do motor — não deixa o servidor sobrescrever após comando
    private Dictionary<string, bool> _motorRunning = new Dictionary<string, bool>();
    // UUIDs bloqueados localmente (via botão Block)
    private HashSet<string> _blockedUUIDs = new HashSet<string>();

    void Start()
    {
        _currentInfo.IsConnected = false;
        NotifyUpdate();

        if (OmmoServiceLauncher.ServiceReady)
            StartPolling();
        else
            OmmoServiceLauncher.OnServiceReady += StartPolling;
    }

    void OnDestroy()
    {
        OmmoServiceLauncher.OnServiceReady -= StartPolling;
        _polling = false;
    }

    void StartPolling()
    {
        OmmoServiceLauncher.OnServiceReady -= StartPolling;
        _polling = true;
        StartCoroutine(PollLoop());
        Debug.Log("[OmmoMonitor] A iniciar polling...");
    }

    IEnumerator PollLoop()
    {
        while (_polling)
        {
            yield return PollHardware();
            yield return new WaitForSeconds(PollInterval);
        }
    }

    IEnumerator PollHardware()
    {
        var task = Task.Run(async () =>
        {
            try
            {
                var handler = new YetAnotherHttpHandler { Http2Only = true };
                var channel = GrpcChannel.ForAddress("http://localhost:50051",
                                   new GrpcChannelOptions { HttpHandler = handler });
                var client = new Ommo.CoreService.CoreServiceClient(channel);
                var response = await client.GetHardwareStatesAsync(new Ommo.HardwareStatesRequest());
                await channel.ShutdownAsync();

                var info = new ServiceInfo { IsConnected = true };

                // Base Station
                foreach (var bs in response.BasestationStates)
                {
                    string uuid = bs.CommonState.Uuid.ToString();
                    bool connected = bs.CommonState.Connected;
                    bool hasLocalState = _motorRunning.ContainsKey(uuid);
                    bool serverRunning = bs.CommonState.HardwareStatus == Ommo.HardwareStatus.Running;

                    // Se temos override local false e o servidor confirma não-Running, limpa o override
                    if (hasLocalState && !_motorRunning[uuid] && !serverRunning)
                    {
                        _motorRunning.Remove(uuid);
                        hasLocalState = false;
                    }

                    bool motorOn = hasLocalState ? _motorRunning[uuid] : connected && serverRunning;
                    // Status vem SEMPRE do servidor — só IsRunning é sobrescrito localmente
                    string status = HardwareStatusToString(bs.CommonState.HardwareStatus);

                    var d = new DeviceInfo
                    {
                        Name = "Base Station",
                        UUID = uuid,
                        Status = status,
                        IsRunning = motorOn,
                        IsBaseStation = true,
                        SyncChannel = bs.SyncChannel.ToString(),
                        Section = connected ? DeviceSection.Connected : DeviceSection.Disconnected
                    };
                    AddToSection(info, d);
                }

                // Wireless SIU
                foreach (var siu in response.SiuStates)
                {
                    string uuid = siu.CommonState.Uuid.ToString();
                    bool connected = siu.CommonState.Connected;
                    bool blocked = _blockedUUIDs.Contains(uuid);

                    var d = new DeviceInfo
                    {
                        Name = "Wireless SIU",
                        UUID = uuid,
                        Status = HardwareStatusToString(siu.CommonState.HardwareStatus),
                        IsRunning = connected && siu.CommonState.HardwareStatus == Ommo.HardwareStatus.Running,
                        IsSIU = true,
                        SyncChannel = siu.SyncChannel.ToString(),
                        DataChannel = siu.DataChannel.ToString(),
                        Section = blocked ? DeviceSection.Blocked
                                    : connected ? DeviceSection.Connected
                                    : DeviceSection.Disconnected
                    };
                    AddToSection(info, d);
                }

                // Wireless Receivers
                foreach (var wr in response.WirelessReceiverStates)
                {
                    string uuid = wr.CommonState.Uuid.ToString();
                    bool connected = wr.CommonState.Connected;

                    var d = new DeviceInfo
                    {
                        Name = "Wireless Receiver",
                        UUID = uuid,
                        Status = HardwareStatusToString(wr.CommonState.HardwareStatus),
                        IsRunning = connected && wr.CommonState.HardwareStatus == Ommo.HardwareStatus.Running,
                        DataChannel = wr.DataChannel.ToString(),
                        Section = connected ? DeviceSection.Connected : DeviceSection.Disconnected
                    };
                    AddToSection(info, d);
                }

                return info;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OmmoMonitor] " + e.Message);
                return new ServiceInfo { IsConnected = false, ErrorCount = 1 };
            }
        });

        yield return new WaitUntil(() => task.IsCompleted);
        _currentInfo = task.Result;
        NotifyUpdate();
    }

    void AddToSection(ServiceInfo info, DeviceInfo d)
    {
        switch (d.Section)
        {
            case DeviceSection.Connected: info.Connected.Add(d); break;
            case DeviceSection.Disconnected: info.Disconnected.Add(d); break;
            case DeviceSection.Blocked: info.Blocked.Add(d); break;
        }
    }

    // Chamado pelo OmmoDeviceRow após comando motor bem-sucedido
    public void ClearMotorState(string uuid)
    {
        if (_motorRunning.ContainsKey(uuid))
            _motorRunning.Remove(uuid);
    }

    public void SetMotorRunningState(string uuid, bool running)
    {
        _motorRunning[uuid] = running;
        var device = _currentInfo.Connected.Find(d => d.UUID == uuid);
        if (device != null) device.IsRunning = running;
        NotifyUpdate();
    }

    // Chamado pelo OmmoDeviceRow ao bloquear/desbloquear SIU
    public void SetBlocked(string uuid, bool blocked)
    {
        if (blocked) _blockedUUIDs.Add(uuid);
        else _blockedUUIDs.Remove(uuid);
    }

    public ServiceInfo CurrentInfo => _currentInfo;
    void NotifyUpdate() => OnHardwareUpdated?.Invoke(_currentInfo);

    string HardwareStatusToString(Ommo.HardwareStatus s) => s switch
    {
        Ommo.HardwareStatus.Running => "Running",
        Ommo.HardwareStatus.Idle => "Idle",
        Ommo.HardwareStatus.SettingUp => "Setting up",
        Ommo.HardwareStatus.Error => "Error",
        Ommo.HardwareStatus.WaitingOnCommand => "Waiting",
        _ => "Unknown"
    };
}