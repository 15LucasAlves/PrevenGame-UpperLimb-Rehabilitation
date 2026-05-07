using UnityEngine;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Cysharp.Net.Http;
using Grpc.Net.Client;
using Grpc.Core;
using grpcChannel = Grpc.Net.Client.GrpcChannel;
using ServiceClient = Ommo.CoreService.CoreServiceClient;

namespace Ommo {
    public class DeviceComparer : IComparer<DeviceDescriptor>
    {
        /*
         * Compare two devices based on their DeviceDescriptor
         * Descriptors are uniquely identified by their type, siu_uuid and port_id
         */
        public int Compare(DeviceDescriptor x, DeviceDescriptor y)
        {
            int diff = (int)(x.UserDeviceType - y.UserDeviceType);
            if (diff != 0) return diff;
            diff = (int)(x.SiuUuid - y.SiuUuid);
            if (diff != 0) return diff;
            return (int)(x.PortId - y.PortId);
        }
    }

    public class AvailableDeviceChangeEventArgs : EventArgs
    {
        public AvailableDeviceChangeEventArgs(DeviceDescriptor device, bool connected)
        {
            DeviceInfo = device;
            Connected = connected;
        }

        public DeviceDescriptor DeviceInfo { get; }
        public bool Connected { get; }
    }

    // Field mask for raw data requests
    class RawDataFieldMask
    {
        public const int OMMO_SIU_UUID = (1 << 0);
        public const int OMMO_PORT_ID = (1 << 1);
        public const int OMMO_THETA = (1 << 2);
        public const int OMMO_SPEED = (1 << 3);
        public const int OMMO_TIMESTAMP = (1 << 4);
        public const int OMMO_DEVICE_TYPE = (1 << 5);
        public const int OMMO_BUTTON_STATUS = (1 << 6);
    }

    public delegate void TrackingDeviceDataHandler(TrackingDeviceData data);
    public delegate void DataFrameHandler(DataFrame dataFrame);

    public class Client
    {
        // TODO: only support local host for now
        private string ServerAddress = "http://localhost:50051";

        private GrpcChannel channel;

        private Thread _deviceListThread;
        private CancellationTokenSource _source;
        private CancellationToken _token;

        private bool _initialized = false;


        public event EventHandler<AvailableDeviceChangeEventArgs> AvailableDeviceChangceEvent;

        private Client()
        {
            Initialize();
        }

        ~Client()
        {
            Shutdown();
        }

        public void Shutdown()
        {
            if (_initialized)
            {
                Debug.Log("Shutting down Ommo Client");
                _initialized = false;
                // NOTE: Follow items must be done in order to properly shut down
                Debug.Log("Shutting down Ommo Client - cancel");
                _source.Cancel();
                
                Debug.Log("Shutting down Ommo Client - join");
                _deviceListThread.Join();
                Debug.Log("Shutting down Ommo Client - shutdown");
                channel.ShutdownAsync().Wait();
                Debug.Log("Shutting down Ommo Client - dispose");  
                _source.Dispose();
            }
        }

        public void Initialize()
        {
            if (!_initialized)
            {
                Debug.Log("Initializing Ommo Client");
                _initialized = true;
                //channel = new grpc::Channel(ServerAddress, grpc.ChannelCredentials.Insecure);
                YetAnotherHttpHandler handler = new YetAnotherHttpHandler() { Http2Only = true };
                channel = grpcChannel.ForAddress(ServerAddress, new GrpcChannelOptions() { HttpHandler = handler });

                _source = new CancellationTokenSource();
                _token = _source.Token;

                _deviceListThread = new Thread(new ThreadStart(TrackingDevicesEventStreamThreadFunction));
                _deviceListThread.Start();
            }
        }


        public async Task StreamTrackingDeviceData(TrackingDeviceDataStreamRequest request, CancellationToken token, TrackingDeviceDataHandler handler)
        {
            ServiceClient client = new ServiceClient(channel);
            using (var call = client.OpenTrackingDeviceDataStream(request))
            {
                while (!token.IsCancellationRequested && !this._token.IsCancellationRequested)
                {
                    try
                    {
                        if (await call.ResponseStream.MoveNext(token))
                        {
                            TrackingDeviceData result = call.ResponseStream.Current;
                            handler(result);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (RpcException e)
                    {
                        Debug.Log("RPC Exception:" + e.StatusCode);
                        break;
                    }
                    catch (AggregateException e)
                    {
                        Debug.Log(e);
                        e.Handle((x) =>
                        {
                            if (x is RpcException)
                            {
                                Debug.Log("Aggregate with RPC Exception:" + ((RpcException)x).StatusCode);
                            }
                            else
                            {
                                Debug.Log(x.GetType());
                            }
                            return true;
                        });
                        break;
                    }
                }
            }
        }

        public async Task StreamDataFrame(DataFrameStreamRequest request, CancellationToken token, DataFrameHandler handler)
        {
            ServiceClient client = new ServiceClient(channel);
            Debug.Log("Data Frame Start");
            using (var call = client.OpenDataFrameStream(request))
            {
                while (!token.IsCancellationRequested && !_token.IsCancellationRequested)
                {
                    try
                    {
                        if (await call.ResponseStream.MoveNext(token))
                        {
                            DataFrame result = call.ResponseStream.Current;
                            handler(result);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (RpcException e)
                    {
                        Debug.Log("RPC Exception:" + e.StatusCode);
                        break;
                    }
                    catch (AggregateException e)
                    {
                        e.Handle((x) =>
                        {
                            if (x is RpcException)
                            {
                                Debug.Log("Aggregate with RPC Exception:" + ((RpcException)x).StatusCode);
                            }
                            else
                            {
                                Debug.Log(x.GetType());
                            }
                            return true;
                        });
                        break;
                    }
                }
            }
            Debug.Log("Data Frame End");
        }

        private void HandleTrackingDeviceEvent(TrackingDeviceEvent e)
        {
            // Use Nullable type of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            AvailableDeviceChangceEvent?.Invoke(this, new AvailableDeviceChangeEventArgs(e.Device, e.Connected));
        }

        private void TrackingDevicesEventStreamThreadFunction()
        {
            Debug.Log("Listen for device list change - start");
            ServiceClient client = new ServiceClient(channel);
            TrackingDevicesEventStreamRequest request = new TrackingDevicesEventStreamRequest();
            request.BufferDepth = 20;
            request.IncludeAllConnectedDevices = true;
            using (var call = client.OpenTrackingDevicesEventStream(request))
            {
                // TODO: this token is global, not specific to the call
                while (!_token.IsCancellationRequested)
                {
                    try
                    {
                        Debug.Log("TrackingDevicesEventStreamThreadFunction - move next");                        
                        Task<bool> callTask = call.ResponseStream.MoveNext(_token);
                        Debug.Log("TrackingDevicesEventStreamThreadFunction - move next - wait");
                        callTask.Wait(_token);
                        Debug.Log("TrackingDevicesEventStreamThreadFunction - move next - wait done");

                        if (callTask.Result)
                        {
                            TrackingDeviceEvent result = call.ResponseStream.Current;
                            HandleTrackingDeviceEvent(result);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (RpcException e)
                    {
                        Debug.Log("RPC Exception:" + e.StatusCode);
                        break;
                    }
                    catch (AggregateException e)
                    {
                        Debug.Log(e);
                        e.Handle((x) =>
                        {
                            if (x is RpcException)
                            {
                                Debug.Log("Aggregate with RPC Exception:" + ((RpcException)x).StatusCode);
                            }
                            else
                            {
                                Debug.Log(x.GetType());
                            }
                            return true;
                        });
                        break;
                    }
                }
            }
            Debug.Log("Listen for device list change - end");
        }

        private static Client _instance;

        public static Client GetClient()
        {
            if (_instance == null)
            {
                _instance = new Client();
            }
            return _instance;
        }
    }
}
