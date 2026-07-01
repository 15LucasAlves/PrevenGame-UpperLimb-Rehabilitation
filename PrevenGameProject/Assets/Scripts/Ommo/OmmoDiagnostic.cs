using UnityEngine;
using System.Threading.Tasks;
using Cysharp.Net.Http;
using Grpc.Net.Client;

public class OmmoDiagnostic : MonoBehaviour
{
    async void Start()
    {
        await Task.Delay(3000);

        Debug.Log("=== OMMO DIAGNOSTIC START ===");

        // 1. GetTrackingDevices
        try
        {
            var handler = new YetAnotherHttpHandler { Http2Only = true };
            var channel = GrpcChannel.ForAddress("http://localhost:50051",
                               new GrpcChannelOptions { HttpHandler = handler });
            var client = new Ommo.CoreService.CoreServiceClient(channel);
            var response = client.GetTrackingDevices(new Ommo.TrackingDevicesRequest());
            await channel.ShutdownAsync();

            Debug.Log("[Diagnostic] Device count: " + response.Devices.Count);
            foreach (var d in response.Devices)
                Debug.Log("[Diagnostic] UUID=" + d.SiuUuid + " Port=" + d.PortId + " Type=" + d.UserDeviceType + " Sensors=" + d.SensorUnitDescriptors.Count);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Diagnostic] GetTrackingDevices FAILED: " + e.Message);
        }

        // 2. OmmoDeviceManager
        var mgr = FindObjectOfType<OmmoDeviceManager>();
        if (mgr == null)
        {
            Debug.LogError("[Diagnostic] OmmoDeviceManager NOT FOUND!");
        }
        else
        {
            Debug.Log("[Diagnostic] OmmoDeviceManager found. Prefab count: " + mgr.DeviceTypePrefabs.Length);
            foreach (var p in mgr.DeviceTypePrefabs)
            {
                string prefabName = p.Prefab != null ? p.Prefab.name : "NULL";
                Debug.Log("[Diagnostic] Prefab type=" + p.DeviceType + " name=" + prefabName);
            }
        }

        // 3. OmmoDevice instances
        var devices = FindObjectsOfType<OmmoDevice>();
        Debug.Log("[Diagnostic] OmmoDevice instances: " + devices.Length);
        foreach (var d in devices)
            Debug.Log("[Diagnostic] OmmoDevice: " + d.name + " children=" + d.transform.childCount);

        Debug.Log("=== OMMO DIAGNOSTIC END ===");
    }
}