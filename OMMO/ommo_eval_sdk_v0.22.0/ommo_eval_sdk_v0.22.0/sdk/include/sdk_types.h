/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

// Define DLL exports
#ifdef _WIN32
#ifdef OMMO_SDK_STATIC
#define OMMO_SDK_API
#elif defined(OMMO_SDK_EXPORTS)
#define OMMO_SDK_API __declspec(dllexport)
#else
#define OMMO_SDK_API __declspec(dllimport)
#endif
#else
#ifdef OMMO_SDK_STATIC
#define OMMO_SDK_API
#else
#define OMMO_SDK_API __attribute__((visibility("default")))
#endif
#endif

#include <cstdint>
#include <memory>

namespace ommo::api
{
    // Extern "C" for unmangled linkage across library interface
    // Inside the namespace, C++ application code can still use the namespace
    extern "C"
    {
        typedef enum DeviceFusionMode
        {
            kDeviceFusionModeDefault = 0,
            kDeviceFusionModeNoFusion = 1,
            kDeviceFusionModeMagOnlyFusion = 2,
            kDeviceFusionModeIMUOnlyFusion = 3,
            kDeviceFusionModeFullFusion = 4
        } DeviceFusionMode;

        typedef struct Vector3i
        {
            int32_t x;
            int32_t y;
            int32_t z;
        } Vector3i;

        typedef struct Vector3f
        {
            float x;
            float y;
            float z;
        } Vector3f;

        typedef struct Vector4f
        {
            float w;
            float x;
            float y;
            float z;
        } Vector4f;

        typedef struct SensorUnitDescriptor
        {
            Vector3i timestamp_offset;
            bool mag_present;
            float mag_scale;
            bool imu_present;
            float accel_scale;
            float gyro_scale;
            uint64_t uuid;
        } SensorUnitDescriptor;

        typedef struct DeviceDescriptor
        {
            uint32_t siu_uuid;
            uint32_t port_id;
            uint32_t user_device_type;
            uint32_t button_count;
            SensorUnitDescriptor* sensor_unit_descriptors;
            uint32_t sensor_unit_descriptor_count;
            DeviceFusionMode* supported_fusion_modes;
            uint32_t supported_fusion_modes_count;
            uint32_t device_part_number;
            bool secure_device_info;
        } DeviceDescriptor;

        typedef struct TrackingDeviceEvent
        {
            bool connected;
            DeviceDescriptor device;
        } TrackingDeviceEvent;

        typedef struct TrackingDevices
        {
            DeviceDescriptor* devices;
            uint32_t device_count;
        } TrackingDevices;

        typedef enum HardwareStatus
        {
            kHardwareStateUnknown = 0,
            kHardwareStateIdle = 1,
            kHardwareStateSettingUp = 2,
            kHardwareStateWaitingOnCommand = 3,
            kHardwareStateRunning = 4,
            kHardwareStateError = 5
        } HardwareStatus;

        typedef struct CommonHardwareState
        {
            bool connected;
            char* serial_number;
            uint32_t uuid;
            char* usb_port_name;
            HardwareStatus hardware_status;
        } CommonHardwareState;

        typedef enum DirectCommStatus
        {
            kDirectCommNone = 0,
            kDirectCommIdle = 1,
            kDirectCommConnected = 2,
            kDirectCommDescriptorRequest = 3,
            kDirectCommChannelSearch = 4
        } DirectCommStatus;

        typedef struct SensorDeviceState
        {
            uint32_t port_number;
            uint32_t mag_sensor_count;
        } SensorDeviceState;

        typedef struct ReceiverConnection
        {
            uint32_t uuid;
            uint32_t time_slot;
        } ReceiverConnection;

        typedef struct BasestationHardwareState
        {
            CommonHardwareState common_state;
            uint32_t sync_channel;
            DirectCommStatus direct_comm_status;
            uint32_t direct_comm_uuid;
            bool motor_running;
        } BasestationHardwareState;

        typedef struct SIUHardwareState
        {
            CommonHardwareState common_state;
            bool wireless;
            uint32_t sync_channel;
            uint32_t data_channel;
            SensorDeviceState* sensor_device_states;
            uint32_t sensor_device_state_count;
        } SIUHardwareState;

        typedef struct WirelessReceiverHardwareState
        {
            CommonHardwareState common_state;
            uint32_t data_channel;
            ReceiverConnection* connected_sius;
            uint32_t connected_siu_count;
        } WirelessReceiverHardwareState;

        typedef struct HardwareStates
        {
            BasestationHardwareState* basestation_states;
            uint32_t basestation_state_count;
            SIUHardwareState* siu_states;
            uint32_t siu_state_count;
            WirelessReceiverHardwareState* wireless_receiver_states;
            uint32_t wireless_receiver_state_count;
        } HardwareStates;

        typedef struct RawSensorData
        {
            Vector3i mag;
            Vector3i gyro;
            Vector3i accel;
        } RawSensorData;

        typedef struct PoseData
        {
            Vector3f position;
            Vector4f quaternion;
            float indicator_value;
            float motion_indicator;
            float bad_data_indicator;
        } PoseData;

        typedef enum ButtonState
        {
            kButtonStateUnknown = 0,
            kButtonStateIdle = 1,
            kButtonStateUp = 2,
            kButtonStateDown = 3,
            kButtonStateLeft = 4,
            kButtonStateRight = 5,
            kButtonStatePressed = 6
        } ButtonState;

        typedef enum TimestampType
        {
            kTimestampTypeUnknown = 0,
            kTimestampTypeSample = 1,
            kTimestampTypeServiceReceived = 2,
            kTimestampTypeServiceSent = 3,
            kTimestampTypeSdkReceived = 4
        } TimestampType;

        typedef struct TimestampData
        {
            TimestampType timestamp_type;
            uint64_t steady_timestamp_milliseconds;
            uint64_t system_timestamp_milliseconds;
        } TimestampData;

        typedef struct BatteryState
        {
            int32_t state_of_charge;
            int32_t current;
            int32_t remaining_capacity;
        } BatteryState;

        typedef struct TrackingDeviceData
        {
            uint32_t siu_uuid;
            uint32_t port_id;
            uint32_t basestation_angle;
            uint32_t basestation_speed;
            uint32_t timestamp;
            RawSensorData* raw_sensor_data;
            uint32_t raw_sensor_data_count;
            PoseData* poses;
            uint32_t pose_count;
            ButtonState* buttons;
            uint32_t button_count;
            TimestampData* latency_timestamps;
            uint32_t latency_timestamp_count;
            BatteryState battery_state;
        } TrackingDeviceData;

        typedef struct DataFrame
        {
            TrackingDeviceData* device_data;
            uint32_t device_data_count;
        } DataFrame;

        typedef struct DevicePacket
        {
            // The original data received from server has no index.
            // When saving data in storage, this index will be added.
            uint32_t packet_idx;
            TrackingDeviceData device_data;
        } DevicePacket;

        typedef enum DataResponseState
        {
            kNoData,
            kPartialData,
            kSuccess
        } DataResponseState;

        typedef struct DataResponse
        {
            DataResponseState state;
            DevicePacket* packets;
            uint32_t packet_count;
        } DataResponse;

        typedef struct DeviceID
        {
            uint32_t siu_uuid;
            uint32_t port_id;
        } DeviceID;

        typedef struct DeviceIDList
        {
            DeviceID* devices;
            uint32_t device_count;
        } DeviceIDList;

        typedef enum DataStreamType
        {
            kDeviceData,
            kDataFrame
        } DataStreamType;

        typedef struct DataRequest
        {
            uint32_t data_field_mask;
            uint32_t report_interval;
            uint32_t buffer_depth;
            DeviceFusionMode requested_fusion_mode;
            bool include_raw_sensor_data;
            DeviceID* requested_devices;
            uint32_t requested_device_count;
        } DataRequest;

        typedef enum DataFieldMask
        {
            kSiuUuid = (1 << 0),
            kPortId = (1 << 1),
            kBasestationAngle = (1 << 2),
            kBasestationSpeed = (1 << 3),
            kTimestamp = (1 << 4),
            kButtonStatus = (1 << 5),
            kBatteryStatus = (1 << 6)
        } DataFieldMask;

        typedef struct TrackingGroup
        {
            /*
             * The siu uuid and port id combination will be used to uniquely identify
             * the tracking group. The port_id is the port_id of the device with the
             * lowest port_id in the group.
             *
             * This uuid/port_id combination is used to identify the tracking group
             * to request data from in TrackingGroupDataStreamRequest
             */
            uint32_t siu_uuid;
            uint32_t port_id;
            DeviceDescriptor* devices;
            uint32_t device_count;
        } TrackingGroup;

        typedef struct TrackingGroupEvent
        {
            TrackingGroup tracking_group;
            bool connected;
        } TrackingGroupEvent;

        typedef struct SpecParamf
        {
            float value;
            bool out_of_spec;
        } SpecParamf;

        typedef struct BaseStationData
        {
            SpecParamf accel_figure_of_merit;
            SpecParamf max_phase_std;
            SpecParamf max_phase_drift;
            SpecParamf temp_diff_from_calib_c;
            SpecParamf mean_rotation_rate_hz;
            SpecParamf tilt_angle_deg;
            SpecParamf max_mag_rms_res;
            SpecParamf accel_dc_magnitude_g;
        } BaseStationData;

        typedef struct BaseStationPacket
        {
            // The original data received from server has no index.
            // When saving data in storage, this index will be added.
            uint32_t packet_idx;
            BaseStationData base_station_data;
        } BaseStationPacket;

        typedef struct BaseStationDataResponse
        {
            DataResponseState state;
            BaseStationPacket* packets;
            uint32_t packet_count;
        } BaseStationDataResponse;

        typedef enum WirelessManagementRequestType
        {
            kWirelessManagementRequestNone = 0,
            kWirelessManagementRequestEnablePairingMode = 1,
            kWirelessManagementRequestDisablePairingMode = 2,
            kWirelessManagementRequestGetPairingApprovedList = 3,
            kWirelessManagementRequestApprovePairing = 4,
            kWirelessManagementRequestDenyPairing = 5,
            kWirelessManagementRequestUnpair = 6,
            kWirelessManagementRequestGetPairingBlockedList = 7,
            kWirelessManagementRequestBlockPairing = 8,
            kWirelessManagementRequestUnblockPairing = 9,
            kWirelessManagementRequestClearBlockedList = 10,
            kWirelessManagementRequestClearApprovedList = 11,
            kWirelessManagementRequestResetWirelessConfig = 12,
            kWirelessManagementRequestSetIntervalLength = 13,
            kWirelessManagementRequestApproveIntervalPairing = 14,
            kWirelessManagementRequestSleepDevice = 15,
            kWirelessManagementRequestWakeDevice = 16,
            kWirelessManagementRequestGetPairingApprovedIntervalList = 17
        } WirelessManagementRequestType;

        typedef struct WirelessManagementRequest
        {
            WirelessManagementRequestType request_type;
            uint32_t siu_uuid;
            uint32_t interval_length;
        } WirelessManagementRequest;

        typedef enum WirelessManagementEventType
        {
            kWirelessManagementEventNone = 0,
            kWirelessManagementEventPairingRequest = 1,
            kWirelessManagementEventPairingTimeout = 2,
            kWirelessManagementEventPairingApprovedList = 3,
            kWirelessManagementEventPairingBlockedList = 4,
            kWirelessManagementEventRequestAck = 5,
            kWirelessManagementEventRequestError = 6,
            kWirelessManagementEventPairingApprovedIntervalList = 7
        } WirelessManagementEventType;

        typedef enum WirelessManagementError
        {
            kWirelessManagementErrorNone = 0,
            kWirelessManagementErrorUuidNotFound = 1,
            kWirelessManagementErrorSettingsSaveFailed = 2,
            kWirelessManagementErrorCouldNotRemoveFromPreviousList = 3,
            kWirelessManagementErrorUuidAlreadyExists = 4,
            kWirelessManagementErrorFailedToClearLists = 5,
            kWirelessManagementErrorSleepNotSupportedInCurrentMode = 6,
            kWirelessManagementErrorDeviceAlreadySleeping = 7,
            kWirelessManagementErrorDeviceAlreadyAwake = 8
        } WirelessManagementError;

        typedef struct DevicePairingInformation
        {
            uint32_t device_part_num_count;
            uint32_t* device_part_nums;
        } DevicePairingInformation;

        typedef struct PairingInformation
        {
            uint32_t siu_uuid;
            uint32_t device_pairing_information_count;
            DevicePairingInformation* device_pairing_information;
        } PairingInformation;

        typedef struct WirelessManagementEvent
        {
            WirelessManagementEventType event_type;
            WirelessManagementRequestType client_request_type;
            uint32_t* siu_uuids;
            uint32_t siu_uuid_count;
            WirelessManagementError request_error;
            PairingInformation pairing_information;
        } WirelessManagementEvent;

        typedef enum DataLogState
        {
            kUnknown = 0,
            kEnabled = 1,
            kDisabled = 2,
            kError = 3,
            kRpcFail = 4,
        } DataLogState;

        typedef struct SelectReferenceDeviceRequest
        {
            bool enabled;
            uint32_t siu_uuid;
            uint32_t port_num;
        } SelectReferenceDeviceRequest;

        typedef struct SelectReferenceDeviceResponse
        {
            bool success;
        } SelectReferenceDeviceResponse;

        typedef struct ReferenceDeviceState
        {
            bool is_enabled;
            uint32_t siu_uuid;
            uint32_t port_num;
        } ReferenceDeviceState;

        OMMO_SDK_API DataRequest* CreateDefaultDataRequest();

        /*
         * Copy functions will allocate new memory and perform a deep copy
         * The returned pointer needs to be deleted when done
         * Allocation uses new and new[] to match the destruction functions.
         */
        OMMO_SDK_API DeviceDescriptor* CopyDeviceDescriptor(const DeviceDescriptor& source);
        OMMO_SDK_API DevicePacket* CopyDevicePacket(const DevicePacket& source);
        OMMO_SDK_API TrackingDeviceData* CopyTrackingDeviceData(const TrackingDeviceData& source);
        OMMO_SDK_API DataRequest* CopyDataRequest(const DataRequest& source);
        OMMO_SDK_API TrackingGroup* CopyTrackingGroup(const TrackingGroup& source);
        OMMO_SDK_API TrackingGroupEvent* CopyTrackingGroupEvent(const TrackingGroupEvent& source);
        OMMO_SDK_API WirelessManagementEvent* CopyWirelessManagementEvent(const WirelessManagementEvent& source);


        /*
         * ------------------------------------------------------------------------
         * Member Destruction Functions
         * ------------------------------------------------------------------------
         *
         * Functions that handle deletion of dynamically allocated members internal to the structs
         * These functions have 3 purposes
         * 1. Allow clean up of statically allocated objects that contain dynamic members allocated with 'new' or 'new[]'
         * 2. Keep internal member deletion logic of each struct in one place
         * 3. Allow nested structs to call these destruction functions to delete internal states
         *
         * NOTE: These functions should NOT be used unless you are managing statically allocated structs with internal dynamic allocation.
         * WARNING: These functions expect a reference which cannot be null. If used with C interop, ensure pointers are not NULL.
         */
        OMMO_SDK_API void DestroyDeviceDescriptorMembers(DeviceDescriptor& descriptor);
        OMMO_SDK_API void DestroyCommonHardwareStateMembers(CommonHardwareState& state);
        OMMO_SDK_API void DestroyBasestationHardwareStateMembers(BasestationHardwareState& state);
        OMMO_SDK_API void DestroySIUHardwareStateMembers(SIUHardwareState& state);
        OMMO_SDK_API void DestroyWirelessReceiverHardwareStateMembers(WirelessReceiverHardwareState& state);
        OMMO_SDK_API void DestroyTrackingDeviceDataMembers(TrackingDeviceData& data);
        OMMO_SDK_API void DestroyDevicePacketMembers(DevicePacket& packet);
        OMMO_SDK_API void DestroyTrackingGroupMembers(TrackingGroup& tracking_group);
        OMMO_SDK_API void DestroyTrackingGroupEventMembers(TrackingGroupEvent& event);
        OMMO_SDK_API void DestroyWirelessManagementEventMembers(WirelessManagementEvent& event);

        /*
         * All destruction functions below assume that standard allocators new and new[] were used.
         * The objects should be ones created and returned by the library to ensure proper allocation and deletion
         *
         * The pointers types do not support static allocation or other allocators.
         */
        OMMO_SDK_API void DestroyDeviceDescriptor(DeviceDescriptor* descriptor);
        OMMO_SDK_API void DestroyTrackingDeviceEvent(TrackingDeviceEvent* event);
        OMMO_SDK_API void DestroyTrackingDevices(TrackingDevices* tracking_devices);
        OMMO_SDK_API void DestroyCommonHardwareState(CommonHardwareState* state);
        OMMO_SDK_API void DestroyBasestationHardwareState(BasestationHardwareState* state);
        OMMO_SDK_API void DestroySIUHardwareState(SIUHardwareState* state);
        OMMO_SDK_API void DestroyWirelessReceiverHardwareState(WirelessReceiverHardwareState* state);
        OMMO_SDK_API void DestroyHardwareStates(HardwareStates* states);
        OMMO_SDK_API void DestroyTrackingDeviceData(TrackingDeviceData* data);
        OMMO_SDK_API void DestroyDataFrame(DataFrame* data_frame);
        OMMO_SDK_API void DestroyDevicePacket(DevicePacket* packet);
        OMMO_SDK_API void DestroyDataResponse(DataResponse* response);
        OMMO_SDK_API void DestroyBaseStationDataResponse(BaseStationDataResponse* response);
        OMMO_SDK_API void DestroyDeviceIDList(DeviceIDList* list);
        OMMO_SDK_API void DestroyDataRequest(DataRequest* request);
        OMMO_SDK_API void DestroyTrackingGroup(TrackingGroup* group);
        OMMO_SDK_API void DestroyTrackingGroupEvent(TrackingGroupEvent* event);
        OMMO_SDK_API void DestroyWirelessManagementEvent(WirelessManagementEvent* event);

        /*
         * Helper functions that convert enum values to string
         */
        OMMO_SDK_API const char* GetHardwareStatusName(HardwareStatus status);
        OMMO_SDK_API const char* GetDirectCommStatusName(DirectCommStatus status);
        OMMO_SDK_API const char* GetTimestampTypeName(TimestampType type);
    }

    /*
     * C++ convinence types for memory management
     * These are defined to be used in library code for automatic memory management
     * The application code can also use the definitions to automatically call library functions on deletion
     */
    template <auto fn>
    struct deleter_fn
    {
        template <typename T>
        constexpr void operator()(T* arg) const
        {
            fn(arg);
        }
    };

    using DeviceDescriptorUPtr = std::unique_ptr<DeviceDescriptor, deleter_fn<DestroyDeviceDescriptor>>;
    using TrackingDeviceEventUPtr = std::unique_ptr<TrackingDeviceEvent, deleter_fn<DestroyTrackingDeviceEvent>>;
    using TrackingDevicesUPtr = std::unique_ptr<TrackingDevices, deleter_fn<DestroyTrackingDevices>>;
    using CommonHardwareStateUPtr = std::unique_ptr<CommonHardwareState, deleter_fn<DestroyCommonHardwareState>>;
    using BasestationHardwareStateUPtr = std::unique_ptr<BasestationHardwareState, deleter_fn<DestroyBasestationHardwareState>>;
    using SIUHardwareStateUPtr = std::unique_ptr<SIUHardwareState, deleter_fn<DestroySIUHardwareState>>;
    using WirelessReceiverHardwareStateUPtr = std::unique_ptr<WirelessReceiverHardwareState, deleter_fn<DestroyWirelessReceiverHardwareState>>;
    using HardwareStatesUPtr = std::unique_ptr<HardwareStates, deleter_fn<DestroyHardwareStates>>;
    using TrackingDeviceDataUPtr = std::unique_ptr<TrackingDeviceData, deleter_fn<DestroyTrackingDeviceData>>;
    using DataFrameUPtr = std::unique_ptr<DataFrame, deleter_fn<DestroyDataFrame>>;
    using DevicePacketUPtr = std::unique_ptr<DevicePacket, deleter_fn<DestroyDevicePacket>>;
    using DataResponseUPtr = std::unique_ptr<DataResponse, deleter_fn<DestroyDataResponse>>;
    using BaseStationPacketUPtr = std::unique_ptr<BaseStationPacket>;
    using BaseStationDataResponseUPtr = std::unique_ptr<BaseStationDataResponse, deleter_fn<DestroyBaseStationDataResponse>>;
    using DeviceIDListUPtr = std::unique_ptr<DeviceIDList, deleter_fn<DestroyDeviceIDList>>;
    using DataRequestUPtr = std::unique_ptr<DataRequest, deleter_fn<DestroyDataRequest>>;
    using TrackingGroupUPtr = std::unique_ptr<TrackingGroup, deleter_fn<DestroyTrackingGroup>>;
    using TrackingGroupEventUPtr = std::unique_ptr<TrackingGroupEvent, deleter_fn<DestroyTrackingGroupEvent>>;
    using WirelessManagementEventUPtr = std::unique_ptr<WirelessManagementEvent, deleter_fn<DestroyWirelessManagementEvent>>;
    

    template <typename T, typename D>
    void MoveUniquePtr(T& destination, std::unique_ptr<T, D> u_ptr)
    {
        T* ptr = u_ptr.release();
        destination = *ptr;
        delete ptr;
    }

    template <typename T>
    void MoveAndDeletePtr(T& destination, T* ptr)
    {
        destination = *ptr;
        delete ptr;
    }
}  // namespace ommo::api
