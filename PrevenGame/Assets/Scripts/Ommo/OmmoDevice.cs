using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

public class OmmoDevice : MonoBehaviour
{
    [Tooltip("Sensor Prefab.")]
    public GameObject SensorPrefab;

    private List<GameObject> _sensors = new List<GameObject>();

    private Task _dataTask;
    private CancellationTokenSource _source;
    private CancellationToken _token;

    private Vector3 _basePosition;
    private Vector3[] _sensorPositions;
    private Quaternion[] _sensorOrientations;

    [Tooltip("Scale for 1 Unity value in centimeters.")]
    private float _unityScaleInCM;
    // [Header("Device Settings")]
    // public uint DeviceType = 0xFF;
    private uint _siuUuid = 0;
    private uint _portId = 0;
    public Ommo.DeviceFusionMode RequestedMode = Ommo.DeviceFusionMode.Default;

    // TODO: make this a singleton
    private Ommo.Client _client;


    private Ommo.DeviceDescriptor _device = null;

    // Use this for initialization
    void Start()
    {
        _source = new CancellationTokenSource();
        _token = _source.Token;
        Debug.Log("Device started");
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < _sensors.Count; i++)
        {
            if (_sensors[i] != null)
            {
                //Debug.Log("Update - for " + i + " position " + _sensorPositions[i]);
                _sensors[i].transform.position = _basePosition + _sensorPositions[i];
                _sensors[i].transform.rotation = _sensorOrientations[i];
            }
        }
    }

    public void SetUnityScaleInCM(float unityScaleInCM)
    {
        _unityScaleInCM = unityScaleInCM;
    }

    public void SetClient(Ommo.Client client)
    {
        _client = client;
    }

    public void SetBasePosition(Vector3 basePosition)
    {
        _basePosition = basePosition;
    }

    public void SetDeviceDescriptor(Ommo.DeviceDescriptor device)
    {
        _device = device;

        int sensorCount = _device.SensorUnitDescriptors.Count;
        Debug.Log("SetDeviceDescriptor - sensorCount " + sensorCount);
        for (int i = 0; i < sensorCount; i++)
        {
            _sensors.Add(Instantiate(SensorPrefab, _basePosition, Quaternion.identity, gameObject.transform));
        }

        _sensorPositions = new Vector3[sensorCount];
        _sensorOrientations = new Quaternion[sensorCount];

        _siuUuid = _device.SiuUuid;
        _portId = _device.PortId;
        StartDataStream(_device.SiuUuid, _device.PortId);
        Debug.Log("Starting Data Stream" + _device.SiuUuid + ":" + _device.PortId);

    }

    private void OnDestroy()
    {
        Debug.Log("Device destroyed");
        _source.Cancel();
        try 
        {
            _dataTask?.Wait(1000); // Wait up to 1 second for clean shutdown
        }
        catch (OperationCanceledException) { }
        _source.Dispose();
    }

    private void StartDataStream(uint uuid, uint portId)
    {
        Debug.Log("Starting Data Stream" + _siuUuid + ":" + _portId);
        // Create a request message to get the data we want
        Ommo.TrackingDeviceDataStreamRequest request = new Ommo.TrackingDeviceDataStreamRequest
        {
            // Header field to include UUID, PortId and DeviceType to identify the device
            FieldMask = Ommo.RawDataFieldMask.OMMO_SIU_UUID | Ommo.RawDataFieldMask.OMMO_PORT_ID | Ommo.RawDataFieldMask.OMMO_DEVICE_TYPE,
            SiuUuid = uuid,
            PortId = portId,
            // Report as soon as data becomes available
            ReportInterval = 0,
            // Buffer 10 samples before overwriting if client is too slow to process
            BufferDepth = 10,
            // Use Default fusion mode (highest available level)
            RequestedFusionMode = RequestedMode
        };

        _dataTask = Task.Factory.StartNew(async () => {
            try 
            {
                await _client.StreamTrackingDeviceData(request, _token, ProcessTrackingDeviceData);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Data stream cancelled");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in data stream: {ex.Message}");
            }
            //_client.StreamTrackingDeviceData(request, _token, ProcessTrackingDeviceData).Wait(_token);
        }, TaskCreationOptions.LongRunning);
    }


    public void ProcessTrackingDeviceData(Ommo.TrackingDeviceData mes)
    {
        //Debug.Log("ProcessTrackingDeviceData " + mes.Timestamp);
        lock (this)
        {
            //Debug.Log("ProcessTrackingDeviceData - lock");
            // Check both sensor positions and ensure we don't exceed the # of sensors on the device
            for (int i = 0; i < _sensorPositions.Length && i < mes.Positions.Count; i++)
            {
                //Debug.Log("ProcessTrackingDeviceData - for " + i);
                // Swap Z and Y axes to match Ommo Base station coordinate frame to Unity coordinate frame
                _sensorPositions[i] = new Vector3(mes.Positions[i].X, mes.Positions[i].Z, mes.Positions[i].Y);
                
                // Divide by scale so position shows up correctly in Unity
                _sensorPositions[i] /= _unityScaleInCM;

                // Simple conversion into Unity rotation
                // Quaternion from service is to rotate from sensor frame into base station frame
                // So we take the inverse as Unity frame is the Base station frame (with Y and Z axis swapped)
                // We swap Y and Z values again to match Ommo Base station coordinate frame to Unity coordinate frame
                _sensorOrientations[i] = Quaternion.Inverse(new Quaternion(mes.Quaternions[i].X, mes.Quaternions[i].Z, mes.Quaternions[i].Y, mes.Quaternions[i].W));
            }
        }
    }
}
