/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once


#include <mutex>
#include <thread>
#include <atomic>
#include <iomanip>

#include "basestation_data_storage.h"
#include "data_manager.h"
#include "grpcpp/grpcpp.h"
#include "ommo_service_api.grpc.pb.h"
#include "rpcClientCallData.h"

class RpcReferenceDeviceStateStreamClientReadReactor;
class RpcTrackingDevicesEventStreamClientReadReactor;
class RpcWirelessManagementStreamClientCallData;

namespace ommo::api
{
    class WirelessManager;
}

namespace ommo
{
    class WirelessManagerWrapper;
}

using grpc::Channel;
using grpc::ClientAsyncResponseReader;
using grpc::ClientContext;
using grpc::CompletionQueue;
using grpc::Status;

namespace ommo
{
    class ClientManager
    {
    public:
        explicit ClientManager(std::string server_address);
        ~ClientManager();

        /*
         * Start the client context and attempt to establish connection to ommo service via a gRPC channel
         * on the provided server address.
         */
        void Start();

        /*
         * Shutdown the client context. This will close the connection to ommo service, cancel all data
         * requests, and delete all available data.
         */
        void Shutdown();

        /*
         * Get a list of tracking devices that are currently connected to and available from ommo service
         */
        api::TrackingDevicesUPtr GetTrackingDevices();

        /*
         * Get a list of basestation, siu, and wireless receiver states for all hardware that ommo service
         * has encountered since start up.
         */
        api::HardwareStatesUPtr GetHardwareStates();

        /*
         * Set the base station motor running to on/off on the server.
         * Returns true if the RPC completed successfully and the server replied success.
         */
        bool SetBaseStationMotorRunning(bool active);

        /*
         * Register a call back to be called whenever a TrackingDeviceEvent is received from service
         * Only one call back can be registered at a time. Registering another callback will overwrite the existing one.
         */
        void RegisterDeviceEventCallback(std::function<void(const api::TrackingDeviceEvent&)> callback_function);

        /*
         * Reset the currently registered callback for TrackingDeviceEvent so it'll no longer be called
         */
        void ResetDeviceEventCallback();

        /*
         * Register a call back to be called whenever channel connection state to service changes
         * Only one call back can be registered at a time. Registering another callback will overwrite the existing one.
         */
        void RegisterChannelStateCallback(std::function<void(int)> callback_function);

        /*
         * Reset the currently registered callback for channel connection state so it'll no longer be called
         */
        void ResetChannelStateCallback();

        /*
         * Register a call back to be called whenever a ReferenceDeviceState is received from service
         * Only one call back can be registered at a time. Registering another callback will overwrite the existing one.
         */
        void RegisterReferenceDeviceStateEventCallback(std::function<void(const api::ReferenceDeviceState& event)> callback_function);

        /*
         * Reset the currently registered callback for ReferenceDeviceState so it'll no longer be called
         */
        void ResetReferenceDeviceStateEventCallback();

        /*
         * Request real-time data from one or more devices. Data is returned individually for each device as
         * soon as it is ready. This request is suitable for scenarios where having the most recent data
         * possible is preferred or required.
         *
         * Individual devices will return data at an interval of ~1000/second.
         * @return Shared pointer to DataManager created for request
         */
        std::shared_ptr<DataManager> RequestDeviceData(api::DataRequest& request);

        /*
         * Request a grouping of data from one or more devices. Data for all specified devices is returned collectively
         * in a single data frame. This request is suitable for scenarios where having a more synchronized snapshot for
         * all devices is preferred.
         *
         * The minimum interval for a data frame is 20ms (~50 data frames/second).
         * @return Shared pointer to DataManager created for request
         */
        std::shared_ptr<DataManager> RequestDataFrame(api::DataRequest& request);

        /*
         * Terminate the open request stream associated with the request tag. This function is used for both DeviceData and
         * DataFrame requests. Once called, the tag can no longer be used with any functions requiring a request tag.
         */
        void CloseRequest(std::shared_ptr<DataManager> data_manager);

        /*
         * Request data from the base station, with the data packets stored in a BaseStationDataStorage.
         * The base station send data packet at an interval of ~4/second.
         * @return Shared pointer to the BaseStationDataStorage created for this request.
         */
        std::shared_ptr<BaseStationDataStorage> RequestBaseStationData();

        /*
         * Close the base station request associated with the provided shared pointer to BaseStationDataStorage.
         * This function first removes the data stream, then deletes the associated shared pointer from the client manager.
         * After this call, the BaseStationDataStorage will no longer receive new data packets.
         */
        void CloseBaseStationDataRequest(std::shared_ptr<BaseStationDataStorage>);

        /*
         * Enable raw data logging. The data will be saved using the specified directory and file name.
         * If the overwrite flag is set to true, the existing file will be overwritten.
         * If the overwrite flag is set to false and file already exists, logging will not be enabled. 
         * @return api::DataLogState::kEnabled only if logging is successfully enabled.
         */
        api::DataLogState EnableDataLogging(std::string directory, std::string file_name, bool overwrite);

        /*
         * Disable raw data logging.
         * @return api::DataLogState::kDisabled only if logging is successfully disabled.
         */
        api::DataLogState DisableDataLogging();

        /*
         * Create a WirelessManager that can be used to manage wireless devices via the ommo service.
         * @return Shared pointer to the created WirelessManager.
         */
        std::shared_ptr<api::WirelessManager> CreateWirelessManager();

        /*
         * Delete the WirelessManager.
         * After this call, the WirelessManager will no longer be functional.
         */
        void DeleteWirelessManager(api::WirelessManager* wireless_manager);

        /*
         * Select a specific device as the reference.
         * @param enabled Whether to enable or disable the reference selection
         * @param siu_uuid The uuid of the device
         * @param port_num The port number of the device
         * @return true if the request was successful, false otherwise
         */
        bool SelectReferenceDevice(bool enabled, uint32_t siu_uuid, uint32_t port_num);

        /*
         * Get the current reference device state.
         */
        api::ReferenceDeviceState GetCurrentReferenceDeviceState();

        /*
         * The following are low level access functions for more advance gRPC API usage. They are direct replacements for
         * legacy rpcOmmoClientManager functions.
         */

        /*
         * Manually open a device data stream. Data is returned individually as soon as it is ready to the provided listener function.
         * This is suitable for scenarios where low level control over the individual data and gRPC API is required.
         */
        rpcClientCallData* OpenTrackingDeviceDataStream(const ommo::TrackingDeviceDataStreamRequest& request, const std::function<void(const ommo::TrackingDeviceData&)> listener_function, std::weak_ptr<ommo::CallDataAssociation> association = std::weak_ptr<ommo::CallDataAssociation>{});

        /*
         * Manually open a data frame stream. Data for all specified devices is returned collectively in a single
         * data frame to the listener function. This is suitable for scenarios where low level control over grouped
         * data and gRPC API is required.
         */
        rpcClientCallData* OpenDataFrameStream(const ommo::DataFrameStreamRequest& request, const std::function<void(const ommo::DataFrame&)> listener_function, std::weak_ptr<ommo::CallDataAssociation> association = std::weak_ptr<ommo::CallDataAssociation>{});

        /*
         * Manually open a base station data stream. Base station data packets are returned to the provided listener function when received from ommo service.
         */
        rpcClientCallData* OpenBaseStationDataStream(const ommo::BaseStationDataStreamRequest &request, const std::function<void(const ommo::BaseStationData&)> cb_handler, std::weak_ptr<ommo::CallDataAssociation> association = std::weak_ptr<ommo::CallDataAssociation>{});

        /*
         * Manually open a tracking group data stream. Data for all specified groups is returned collectively in a single
         * data frame to the provided callback function when received from ommo service.
         */
        rpcClientCallData* OpenTrackingGroupDataStream(const ommo::TrackingGroupDataStreamRequest &request, const std::function<void(const ommo::DataFrame&)> cb_handler, std::weak_ptr<ommo::CallDataAssociation> association = std::weak_ptr<ommo::CallDataAssociation>{});
        
        /*
         * Manually open a tracking groups event stream. Tracking group events are returned to the provided callback function when received from ommo service.
         */
        rpcClientCallData* OpenTrackingGroupsEventStream(const ommo::TrackingGroupsEventStreamRequest &request, const std::function<void(const ommo::TrackingGroupEvent&)> cb_handler);

    private:
        /*
         * Handle device events received from ommo service. Updates the list of connected devices
         * and any DataManagers that are interested. It will also call the user provided callback function.
         */
        void DeviceEventProcessor(const ommo::TrackingDeviceEvent& device_event);

        /*
         * Handle reference device state events received from ommo service.
         */
        void ReferenceDeviceStateEventProcessor(const ommo::ReferenceDeviceState& event);

        // Monitor the gRPC channel status. Opens a DeviceEventStream when the channel is able to be used.
        void ChannelMonitor();

        // Handle the events put onto the gRPC completion queue
        void CompletionQueueProcessor();

        // Open a device data stream for a DataManager
        void OpenDeviceDataStream(std::shared_ptr<DataManager> data_manager_ptr);

        // Update the DataManager's device data stream based on the specified device's state
        void UpdateDeviceDataStream(std::shared_ptr<DataManager> data_manager_ptr, api::DeviceDescriptor& device, bool device_connected);

        // Open a data frame stream for a DataManager
        void OpenDataFrame(std::shared_ptr<DataManager> data_manager_ptr);

        // Update the DataManager's data frame stream based on the specified device's state
        void UpdateDataFrameStream(std::shared_ptr<DataManager> data_manager_ptr, api::DeviceDescriptor& device, bool device_connected);

        // gRPC completion queue
        CompletionQueue completion_queue_;

        // gRPC server location. Defaults to localhost:50051
        std::string server_address_;

        // gRPC channel
        std::shared_ptr<Channel> channel_;

        // Lockable object to protect the connected devices map
        std::mutex connected_devices_mtx_;

        // Store the connected devices by device hash
        std::unordered_map<uint64_t, api::DeviceDescriptorUPtr> connected_devices_;

        // Flag to signal channel monitor to stop
        std::atomic<bool> stop_channel_monitor_{ false };

        // Thread to monitor gRPC channel state
        std::unique_ptr<std::thread> channel_monitor_thread_;

        // Flag to signal completion queue handling to stop
        std::atomic<bool> stop_handling_cq_{ false };

        // Thread to handle completion queue
        std::unique_ptr<std::thread> handle_cq_thread_;

        /*
         * Store the most recent gRPC channel state.
         * -1 is an invalid channel state and is used before the initial status is received
         */
        int previous_channel_state_ = -1;

        // Stores the pointer to the gRPC async API client read reactor for handling device events
        std::unique_ptr<RpcTrackingDevicesEventStreamClientReadReactor> device_event_stream_ptr_;

        // Stores the pointer to the gRPC async API client read reactor for reference device state events
        std::unique_ptr<RpcReferenceDeviceStateStreamClientReadReactor> reference_device_state_stream_ptr_;

        // Store the user's device event callback function
        std::function<void(const api::TrackingDeviceEvent& device_event)> device_event_user_callback_;

        // Store the user's gRPC channel state callback function
        std::function<void(int channel_state)> channel_state_user_callback_;

        // Store the user's reference device state event callback function
        std::function<void(const api::ReferenceDeviceState& event)> reference_device_state_event_user_callback_;

        // Lockable object to protect the data manager list
        std::mutex data_manager_list_mutex_;

        // Store the DataManagers created when requests are opened
        std::vector<std::shared_ptr<DataManager>> data_manager_list_;

        // Mutex variable to protect the base station storage list.
        std::mutex base_station_data_storage_list_mutex_;
    
        // Vector of storage for base station data when requests are opened.
        std::vector<std::shared_ptr<BaseStationDataStorage>> base_station_data_storage_list_;

        // Mutex variable to protect the wireless manager list.
        std::mutex wireless_manager_list_mutex_;

        // Store the wireless manager created.
        std::vector<std::shared_ptr<WirelessManagerWrapper>> wireless_manager_wrapper_list_;

        // Mutex to protect access to current reference device state
        std::mutex reference_device_state_mutex_;

        // Store the current reference device state
        api::ReferenceDeviceState current_reference_device_state_{false, 0, 0};
    };
}  // namespace ommo
