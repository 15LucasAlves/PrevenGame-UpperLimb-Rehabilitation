
## Service Latency Timestamps

A new repeated field "latency_timestamps" has been added to the TrackingDeviceData protobuf message. Each latency_timestamp is a LatencyTimestampData message as described below.

### Timestamp Events
Four separate timestamps are captured during the lifespan of a single data packet: Sample Time, Service Received Time, Service Sent Time, and SDK Received Time.

Service Receive Time and Service Sent Time are both captured directly using the std::chrono library.

The sample time is calculated manually using a sync algorithm to convert from base station clock to service clock.


### Timestamp Information Message

A new message type was added to the ommo_service_api.proto file. The message consists of a LatencyTimestampType field to describe the timestamp event and two timestamps expressed in milliseconds.

```
message LatencyTimestampData
{
    LatencyTimestampType timestamp_type = 1;
    uint64 steady_timestamp_milliseconds = 2;
    uint64 system_timestamp_milliseconds = 3;
}
```


### Latency Timestamp Type:

```
enum LatencyTimestampType
{
    LATENCY_TIMESTAMP_TYPE_UNKNOWN = 0;
    LATENCY_TIMESTAMP_TYPE_SAMPLE = 1;
    LATENCY_TIMESTAMP_TYPE_SERVICE_RECEIVED = 2;
    LATENCY_TIMESTAMP_TYPE_SERVICE_SENT = 3;
    LATENCY_TIMESTAMP_TYPE_SDK_RECEIVED = 4;
}
```

1. Unknown: A default value that does not represent a real timestamp
2. Sample: When the data was sampled from the sensors.
3. Service Received: When the data packet was received by service over USB
4. Service Sent: When the data packet is sent over the gRPC channel to the client
5. SDK Received: When the data packet is received from service in the sdk


### Timestamp Data

1. Steady Timestamp: Incrementing millisecond count since the computer was started. This timestamp is monotonic and can be used for accurate calculations between timestamps.
2. System Timestamp: Millisecond count since epoch that can be used to convert to local system time. This timestamp is provided for convenience in correlating data with real-world time.
