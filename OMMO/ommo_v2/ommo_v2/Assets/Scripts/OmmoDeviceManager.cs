using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

public class OmmoDeviceManager : MonoBehaviour
{
    [Serializable]
    public struct DeviceTypePrefab
    {
        public uint DeviceType;
        public GameObject Prefab;
    }

    [Header("Device Type and Prefabs")]
    public DeviceTypePrefab[] DeviceTypePrefabs;

    [Tooltip("GameObject representing the base station.")]
    public GameObject BaseStation;

    [Tooltip("Scale for 1 Unity value in centimeters.")]
    public float UnityScaleInCM = 10;

    private Dictionary<uint, GameObject> _deviceTypePrefabDict = new Dictionary<uint, GameObject>();
    private Vector3 _basePosition;
    private Ommo.Client _client;

    private SortedSet<Ommo.DeviceDescriptor> _devices =
        new SortedSet<Ommo.DeviceDescriptor>(new Ommo.DeviceComparer());
    private Dictionary<Ommo.DeviceDescriptor, OmmoDevice> _deviceToInstance =
        new Dictionary<Ommo.DeviceDescriptor, OmmoDevice>();
    private Queue<Ommo.DeviceDescriptor> _instantiateQueue = new Queue<Ommo.DeviceDescriptor>();
    private Queue<Ommo.DeviceDescriptor> _destroyQueue     = new Queue<Ommo.DeviceDescriptor>();
    private bool _subscribed = false;

    void Start()
    {
        if (BaseStation == null)
        {
            BaseStation = new GameObject("Base Station");
            BaseStation.transform.position = Vector3.zero;
        }
        _basePosition = BaseStation.transform.position;

        if (DeviceTypePrefabs == null || DeviceTypePrefabs.Length == 0)
        {
            Debug.LogError("[OmmoDeviceManager] Nenhum Device prefab definido!");
            return;
        }

        foreach (var dtp in DeviceTypePrefabs)
        {
            if (!_deviceTypePrefabDict.ContainsKey(dtp.DeviceType))
                _deviceTypePrefabDict.Add(dtp.DeviceType, dtp.Prefab);
        }

        // Não inicia automaticamente — aguarda chamada explícita de StartTracking()
        Debug.Log("[OmmoDeviceManager] Aguarda chamada de StartTracking()");
    }

    public void StartTracking()
    {
        if (_subscribed)
        {
            // Re-bootstrap para recriar GameObjects destruídos pelo StopTracking
            StartCoroutine(BootstrapConnectedDevices());
            return;
        }
        if (OmmoServiceLauncher.ServiceReady)
            InitClient();
        else
            OmmoServiceLauncher.OnServiceReady += InitClient;
    }

    public void StopTracking()
    {
        // Não dessubscreve o evento — o Ommo.Client singleton continua ativo
        // Apenas destroi os GameObjects visuais
        foreach (var kv in _deviceToInstance)
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        _deviceToInstance.Clear();
        _devices.Clear();
        _instantiateQueue.Clear();
        _destroyQueue.Clear();
        StopAllCoroutines();
        Debug.Log("[OmmoDeviceManager] Tracking parado — GameObjects destruídos.");
    }

    void InitClient()
    {
        OmmoServiceLauncher.OnServiceReady -= InitClient;
        _client = Ommo.Client.GetClient();
        _client.AvailableDeviceChangceEvent += HandleAvailableDeviceChangeEvent;
        _subscribed = true;

        // Garante que existe um prefab para type=255 (tipo genérico)
        // Usa o primeiro prefab disponível como fallback
        if (!_deviceTypePrefabDict.ContainsKey(255) && _deviceTypePrefabDict.Count > 0)
        {
            var first = new List<KeyValuePair<uint,GameObject>>(_deviceTypePrefabDict)[0];
            _deviceTypePrefabDict[255] = first.Value;
            Debug.Log("[OmmoDeviceManager] Registado prefab fallback para type=255");
        }

        Debug.Log("[OmmoDeviceManager] À escuta de dispositivos.");

        // Injeta dispositivos já conectados via GetTrackingDevices
        StartCoroutine(BootstrapConnectedDevices());
    }

    System.Collections.IEnumerator BootstrapConnectedDevices()
    {
        // Aguarda um frame para garantir que o event stream está subscrito
        yield return new UnityEngine.WaitForSeconds(1f);

        var task = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var handler  = new Cysharp.Net.Http.YetAnotherHttpHandler { Http2Only = true };
                var channel  = Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost:50051",
                                   new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = handler });
                var client   = new Ommo.CoreService.CoreServiceClient(channel);
                var response = client.GetTrackingDevices(new Ommo.TrackingDevicesRequest());
                channel.ShutdownAsync().Wait();
                return response;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[OmmoDeviceManager] Bootstrap error: " + e.Message);
                return null;
            }
        });

        yield return new UnityEngine.WaitUntil(() => task.IsCompleted);

        if (task.Result == null) yield break;

        foreach (var device in task.Result.Devices)
        {
            Debug.Log($"[OmmoDeviceManager] Bootstrap device: {device.SiuUuid}:{device.PortId}");
            // Simula o evento de conexão
            var args = new Ommo.AvailableDeviceChangeEventArgs(device, true);
            HandleAvailableDeviceChangeEvent(this, args);
        }
    }

    void Update()
    {
        while (_instantiateQueue.Count > 0)
        {
            var deviceInfo = _instantiateQueue.Dequeue();

            // Evita duplicados — verifica se já existe instância para este UUID+Port
            bool alreadyExists = false;
            foreach (var kv in _deviceToInstance)
                if (kv.Key.SiuUuid == deviceInfo.SiuUuid && kv.Key.PortId == deviceInfo.PortId)
                { alreadyExists = true; break; }
            if (alreadyExists) { Debug.Log("[OmmoDeviceManager] Duplicado ignorado: " + deviceInfo.SiuUuid); continue; }


            // Usa o tipo exato ou fallback 255 ou o primeiro disponível
            uint key = _deviceTypePrefabDict.ContainsKey(deviceInfo.UserDeviceType) ? deviceInfo.UserDeviceType
                     : _deviceTypePrefabDict.ContainsKey(255) ? (uint)255
                     : 0;
            if (!_deviceTypePrefabDict.ContainsKey(key) || _deviceTypePrefabDict[key] == null)
            {
                Debug.LogWarning("[OmmoDeviceManager] Prefab null para type=" + deviceInfo.UserDeviceType + " — a criar prefab básico");
                _deviceTypePrefabDict[key] = CreateDefaultDevicePrefab();
            }

            var device = Instantiate(
                _deviceTypePrefabDict[key],
                _basePosition, Quaternion.identity).GetComponent<OmmoDevice>();

            device.gameObject.name = $"OmmoDevice_{deviceInfo.SiuUuid}_{deviceInfo.PortId}";
            device.SetBasePosition(_basePosition);
            device.SetUnityScaleInCM(UnityScaleInCM);
            device.SetClient(_client);
            device.SetDeviceDescriptor(deviceInfo);
            _deviceToInstance[deviceInfo] = device;
        }

        while (_destroyQueue.Count > 0)
        {
            var deviceInfo = _destroyQueue.Dequeue();
            if (_deviceToInstance.TryGetValue(deviceInfo, out var instance))
            {
                Destroy(instance.gameObject);
                _deviceToInstance.Remove(deviceInfo);
            }
        }
    }

    GameObject CreateDefaultDevicePrefab()
    {
        var go = new GameObject("TrackedDevice");
        go.AddComponent<OmmoDevice>();
        // Sensor visual — esfera vermelha
        var sensor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sensor.name = "Sensor";
        sensor.transform.SetParent(go.transform, false);
        sensor.transform.localScale = Vector3.one * 0.15f;
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.9f, 0.1f, 0.1f);
        sensor.GetComponent<Renderer>().material = mat;
        var ommoDevice = go.GetComponent<OmmoDevice>();
        ommoDevice.SensorPrefab = sensor;
        go.SetActive(false);
        return go;
    }

    void OnDestroy()
    {
        OmmoServiceLauncher.OnServiceReady -= InitClient;
        if (_client != null && _subscribed)
        {
            _client.AvailableDeviceChangceEvent -= HandleAvailableDeviceChangeEvent;
            // NÃO chamar _client.Shutdown() aqui — o cliente é partilhado com outros scripts
        }
    }

    void HandleAvailableDeviceChangeEvent(object sender, Ommo.AvailableDeviceChangeEventArgs a)
    {
        lock (this)
        {
            if (!_deviceTypePrefabDict.ContainsKey(a.DeviceInfo.UserDeviceType)) return;

            if (a.Connected)
            {
                _devices.Add(a.DeviceInfo);
                _instantiateQueue.Enqueue(a.DeviceInfo);
                Debug.Log($"[OmmoDeviceManager] Dispositivo conectado: {a.DeviceInfo.SiuUuid}:{a.DeviceInfo.PortId}");
            }
            else
            {
                _devices.Remove(a.DeviceInfo);
                _destroyQueue.Enqueue(a.DeviceInfo);
                Debug.Log($"[OmmoDeviceManager] Dispositivo desconectado: {a.DeviceInfo.SiuUuid}");
            }
        }
    }
}
