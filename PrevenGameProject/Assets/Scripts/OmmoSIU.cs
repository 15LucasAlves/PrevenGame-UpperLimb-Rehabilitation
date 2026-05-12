using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

public class OmmoSIU : MonoBehaviour
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


    protected Vector3 _basePosition;
    protected Ommo.Client _client;

    protected SortedSet<Ommo.DeviceDescriptor> _devices = new SortedSet<Ommo.DeviceDescriptor>(new Ommo.DeviceComparer());

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected Queue<Ommo.DeviceDescriptor> _instantiateQueue = new Queue<Ommo.DeviceDescriptor>();

    protected Queue<Ommo.DeviceDescriptor> _destroyQueue = new Queue<Ommo.DeviceDescriptor>();

    private Dictionary<uint, GameObject> _deviceTypePrefabDict = new Dictionary<uint, GameObject>();

    private uint _siuUuid = 0;
    
    private CancellationTokenSource _source;
    private CancellationToken _token;
    private Task _dataTask;

    private List<GameObject> _sensors = new List<GameObject>();

    private Dictionary<uint, GameObject[]> _portIdToSensors = new Dictionary<uint, GameObject[]>();
    private Dictionary<uint, Vector3[]> _portIdToSensorPosition = new Dictionary<uint, Vector3[]>();
    private Dictionary<uint, Quaternion[]> _portIdToSensorOrientation = new Dictionary<uint, Quaternion[]>();

    void Start()
    {
        _client = Ommo.Client.GetClient();

        if (BaseStation == null)
        {
            BaseStation = new GameObject("Base Station");
            BaseStation.transform.position = new Vector3(0, 0, 0);
        }
        _basePosition = BaseStation.transform.position;

        if (DeviceTypePrefabs == null || DeviceTypePrefabs.Length == 0)
        {
            Debug.LogError("No Device type prefabs defined!");
        }
        else
        {
            foreach (DeviceTypePrefab deviceTypePrefab in DeviceTypePrefabs)
            {
                if (_deviceTypePrefabDict.ContainsKey(deviceTypePrefab.DeviceType))
                {
                    Debug.LogError("Duplicate device type defined, the latest prefab will override the previous ones.");
                }
                _deviceTypePrefabDict.Add(deviceTypePrefab.DeviceType, deviceTypePrefab.Prefab);
            }
        }

        _client.AvailableDeviceChangceEvent += HandleAvailableDeviceChangeEvent;
        Debug.Log("Started listening for devices");
    }

    // Update is called once per frame
    void Update()
    {
        while (_instantiateQueue.Count > 0)
        {
            Ommo.DeviceDescriptor deviceInfo = _instantiateQueue.Dequeue();
            int sensorCount = deviceInfo.SensorUnitDescriptors.Count;
            Debug.Log("Sensor count: " + sensorCount);
            _portIdToSensorPosition[deviceInfo.PortId] = new Vector3[sensorCount];
            _portIdToSensorOrientation[deviceInfo.PortId] = new Quaternion[sensorCount];

            GameObject[] sensors = new GameObject[sensorCount];
            for (int i = 0; i < sensorCount; i++)
            {
                sensors[i] = Instantiate(_deviceTypePrefabDict[deviceInfo.UserDeviceType], _basePosition, Quaternion.identity, gameObject.transform);
            }
            Debug.Log("deviceInfo.PortId " + deviceInfo.PortId);
            _portIdToSensors[deviceInfo.PortId] = sensors;
        }
        while (_destroyQueue.Count > 0)
        {
            Ommo.DeviceDescriptor deviceInfo = _destroyQueue.Dequeue();
            int sensorCount = deviceInfo.SensorUnitDescriptors.Count;            
            for (int i = 0; i < sensorCount; i++)
            {
                Destroy(_portIdToSensors[deviceInfo.PortId][i]);
            }
            _portIdToSensors.Remove(deviceInfo.PortId);
            _portIdToSensorPosition.Remove(deviceInfo.PortId);
            _portIdToSensorOrientation.Remove(deviceInfo.PortId);
        }

        foreach (KeyValuePair<uint, GameObject[]> kvp in _portIdToSensors)
        {
            Vector3[] sensorPositions = _portIdToSensorPosition[kvp.Key];
            Quaternion[] sensorOrientations = _portIdToSensorOrientation[kvp.Key];
            for (int i = 0; i < kvp.Value.Length; i++)
            {
                if (kvp.Value[i] != null)
                {
                    kvp.Value[i].transform.position = _basePosition + sensorPositions[i];
                    kvp.Value[i].transform.rotation = sensorOrientations[i];
                }
            }
        }
    }

    
    private void OnDestroy()
    {
        _client.AvailableDeviceChangceEvent -= HandleAvailableDeviceChangeEvent;
        Debug.Log("Device destroyed");
        _client.Shutdown();
    }


    void HandleAvailableDeviceChangeEvent(object sender, Ommo.AvailableDeviceChangeEventArgs a)
    {
        Debug.Log("[OmmoDevice:HandleDeviceChange] " + ":" + a.DeviceInfo + ":" + a.Connected);
        lock (this)
        {
            if (_deviceTypePrefabDict.ContainsKey(a.DeviceInfo.UserDeviceType))
            {
                if (_devices.Count == 0 && a.Connected)
                {
                    // This is the first device we have received, we will just stick to it
                    _siuUuid = a.DeviceInfo.SiuUuid;
                }

                if (a.DeviceInfo.SiuUuid == _siuUuid)
                {
                    if (a.Connected) // Device is now connected
                    {
                        _devices.Add(a.DeviceInfo);
                        _instantiateQueue.Enqueue(a.DeviceInfo);
                    }
                    else // Device is now disconnected
                    {
                        _devices.Remove(a.DeviceInfo);
                        _destroyQueue.Enqueue(a.DeviceInfo);
                        Debug.Log("Device disconnected: " + a.DeviceInfo);
                    }

                    // matches our existing SIU, add the device
                    Debug.Log("Removing old data frame stream");
                    // remove our old data frame stream
                    _source?.Cancel();
                    _dataTask?.Wait();
                    _source?.Dispose();

                    if (_devices.Count > 0)
                    {
                        // request new one with updated list
                        _source = new CancellationTokenSource();
                        _token = _source.Token;

                        Debug.Log("Starting Data Frame Stream");
                        StartDataFrameStream(_devices);
                    }
                }
            }
        }
    }

    private void StartDataFrameStream(SortedSet<Ommo.DeviceDescriptor> devices)
    {
        // Create a request message to get the data we want
        Ommo.DataFrameStreamRequest request = new Ommo.DataFrameStreamRequest
        {
            // Report as soon as data becomes available
            ReportInterval = 0,
            // Buffer 10 samples before overwriting if client is too slow to process
            BufferDepth = 10
        };
        Debug.Log("Requesting Data Frame");
        foreach(Ommo.DeviceDescriptor device in devices)
        {
            Debug.Log(device.SiuUuid + ":" + device.PortId);
            request.TrackingDevices.Add(new Ommo.DataFrameTrackingDevice 
            {
                // Header field to include UUID, PortId and DeviceType to identify the device
                FieldMask = Ommo.RawDataFieldMask.OMMO_SIU_UUID | Ommo.RawDataFieldMask.OMMO_PORT_ID | Ommo.RawDataFieldMask.OMMO_DEVICE_TYPE,
                SiuUuid = device.SiuUuid,
                PortId = device.PortId,
                // Use default fusion mode (highest available for device)
                RequestedFusionMode = Ommo.DeviceFusionMode.Default
            });
        }

        _dataTask = Task.Factory.StartNew((System.Action)(() => {
            _client.StreamDataFrame(request, _token, ProceessDataFrame).Wait();
        }), TaskCreationOptions.LongRunning);
    }

    public void ProceessDataFrame(Ommo.DataFrame dataFrame)
    {
        lock (this)
        {
            foreach(Ommo.TrackingDeviceData data in dataFrame.DeviceData)
            {
                //Debug.Log("ProceessDataFrame");
                if (!_portIdToSensors.ContainsKey(data.PortId))
                {
                    continue;
                }

                Vector3[] sensorPositions = _portIdToSensorPosition[data.PortId];
                Quaternion[] sensorOrientations = _portIdToSensorOrientation[data.PortId];

                uint sensorIndex = 0;
                // Check both sensor positions and ensure we don't exceed the # of sensors on the device
                for (int i = 0; i < data.Positions.Count && sensorIndex < sensorPositions.Length; i++)
                {
                    // Swap Z and Y axes to match Ommo Base station coordinate frame to Unity coordinate frame
                    sensorPositions[sensorIndex] = new Vector3(data.Positions[i].X, data.Positions[i].Z, data.Positions[i].Y);

                    // Divide by scale so position shows up correctly in Unity
                    sensorPositions[sensorIndex] /= UnityScaleInCM;

                    // Simple conversion into Unity rotation
                    // Quaternion from service is to rotate from sensor frame into base station frame
                    // So we take the inverse as Unity frame is the Base station frame (with Y and Z axis swapped)
                    // We swap Y and Z values again to match Ommo Base station coordinate frame to Unity coordinate frame
                    sensorOrientations[sensorIndex] = Quaternion.Inverse(new Quaternion(data.Quaternions[i].X, data.Quaternions[i].Z, data.Quaternions[i].Y, data.Quaternions[i].W));

                    // increment sensorIndex
                    sensorIndex++;
                }
            }
        }
    }
}
