/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#include <iomanip>
#include <filesystem>
#include "client_manager.h"
#include "logger_base.h"
#include "rpcOpenTrackingDeviceDataStreamClientCallData.h"
#include "rpcOpenDataFrameStreamClientCallData.h"
#include "rpc_base_station_data_stream_client_call_data.h"
#include "rpc_reference_device_state_stream_client_read_reactor.h"
#include "rpc_tracking_devices_event_stream_client_read_reactor.h"
#include "rpc_tracking_group_data_stream_client_call_data.h"
#include "rpc_tracking_groups_event_stream_client_call_data.h"
#include "rpc_wireless_management_stream_client_bidi_reactor.h"
#include "protobuf_converters.h"
#include "sdk_utils.h"
#include "wireless_manager.h"
#include "wireless_manager_impl.h"
#include "wireless_manager_wrapper.h"

namespace
{
    // Check the gRPC channel state every <interval> seconds
    int check_channel_interval = 1;
}

namespace ommo
{

    ClientManager::ClientManager(std::string server_address) : server_address_(server_address)
    {
        // Initialize the grpc channel.
        channel_ = grpc::CreateChannel(server_address_, grpc::InsecureChannelCredentials());
    }

    ClientManager::~ClientManager()
    {
        Shutdown();
    }

    rpcClientCallData* ClientManager::OpenTrackingDeviceDataStream(const ommo::TrackingDeviceDataStreamRequest& request, const std::function<void(const ommo::TrackingDeviceData&)> listener_function, std::weak_ptr<ommo::CallDataAssociation> association)
    {
        return new rpcOpenTrackingDeviceDataStreamClientCallData(channel_, &completion_queue_, request, listener_function, association);
    }

    rpcClientCallData* ClientManager::OpenDataFrameStream(const ommo::DataFrameStreamRequest& request, const std::function<void(const ommo::DataFrame&)> listener_function, std::weak_ptr<ommo::CallDataAssociation> association)
    {
        return new rpcOpenDataFrameStreamClientCallData(channel_, &completion_queue_, request, listener_function, association);
    }


    rpcClientCallData* ClientManager::OpenBaseStationDataStream(const ommo::BaseStationDataStreamRequest &request, const std::function<void(const ommo::BaseStationData&)> cb_handler, std::weak_ptr<ommo::CallDataAssociation> association)
    {
        return new RpcBaseStationDataStreamClientCallData(channel_, &completion_queue_, request, cb_handler, association);
    }

    rpcClientCallData* ClientManager::OpenTrackingGroupDataStream(const ommo::TrackingGroupDataStreamRequest &request, const std::function<void(const ommo::DataFrame&)> cb_handler, std::weak_ptr<ommo::CallDataAssociation> association)
    {
        return new RpcTrackingGroupDataStreamClientCallData(channel_, &completion_queue_, request, cb_handler, association);
    }

    rpcClientCallData* ClientManager::OpenTrackingGroupsEventStream(const ommo::TrackingGroupsEventStreamRequest &request, const std::function<void(const ommo::TrackingGroupEvent&)> cb_handler)
    {
        return new RpcTrackingGroupsEventStreamClientCallData(channel_, &completion_queue_, request, cb_handler);
    }

    api::TrackingDevicesUPtr ClientManager::GetTrackingDevices()
    {
        ommo::TrackingDevicesRequest blank_request;
        ommo::TrackingDevices reply;

        std::unique_ptr<ommo::CoreService::Stub> stub = ommo::CoreService::NewStub(channel_);
        ClientContext grpc_client_context;
        Status status = stub->GetTrackingDevices(&grpc_client_context, blank_request, &reply);

        if (status.ok())
        {
            return ommo::ProtoToTrackingDevices(reply);
        }
        else // RPC failure
        {
            // TODO: Consider returning a nullptr instead of a default struct?
            reply.Clear();
            return ommo::ProtoToTrackingDevices(reply);
        }
    }

    api::HardwareStatesUPtr ClientManager::GetHardwareStates()
    {
        ommo::HardwareStatesRequest blank_request;
        ommo::HardwareStates reply;

        std::unique_ptr<ommo::CoreService::Stub> stub = ommo::CoreService::NewStub(channel_);
        ClientContext grpc_client_context;
        Status status = stub->GetHardwareStates(&grpc_client_context, blank_request, &reply);

        if (status.ok())
        {
            return ommo::ProtoToHardwareStates(reply);
        }
        else // RPC failure
        {
            // TODO: Consider returning a nullptr instead of a default struct?
            reply.Clear();
            return ommo::ProtoToHardwareStates(reply);
        }
    }

    bool ClientManager::SetBaseStationMotorRunning(bool active)
    {
        ommo::BaseStationMotorRunningRequest request;
        request.set_active(active);
        ommo::BaseStationMotorRunningResponse reply;

        std::unique_ptr<ommo::CoreService::Stub> stub = ommo::CoreService::NewStub(channel_);
        ClientContext grpc_client_context;
        Status status = stub->SetBaseStationMotorRunning(&grpc_client_context, request, &reply);

        if (status.ok())
        {
            return reply.success();
        }
        else
        {
            OMMOLOG_ERROR("SetBaseStationMotorRunning RPC failed. code={} message={}",
                          static_cast<int>(status.error_code()), status.error_message());
            reply.Clear();
            return false;
        }
    }

    void ClientManager::ChannelMonitor()
    {
        while (!stop_channel_monitor_)
        {
            if (channel_ == nullptr)
            {
                OMMOLOG_ERROR("Channel is null. Exiting ChannelMonitor");
                return;
            }

            int state = channel_->GetState(true);

            // -1 is an invalid channel state. It's only used to detect initial startup condition
            if (-1 == previous_channel_state_ || state != previous_channel_state_)
            {
                if (state == GRPC_CHANNEL_READY)
                {
                    // if channel is ready, open a device event stream
                    ommo::TrackingDevicesEventStreamRequest req;
                    req.set_buffer_depth(100);

                    req.set_include_all_connected_devices(true);

                    OMMOLOG_INFO("Channel is ready. Opening device event stream");
                    device_event_stream_ptr_ = std::make_unique<RpcTrackingDevicesEventStreamClientReadReactor>(channel_, req, std::bind(&ClientManager::DeviceEventProcessor, this, std::placeholders::_1));

                    ommo::ReferenceDeviceStateStreamRequest ref_req;
                    ref_req.set_include_initial_state(true);
                    OMMOLOG_INFO("Opening reference device state event stream");
                    reference_device_state_stream_ptr_ = std::make_unique<RpcReferenceDeviceStateStreamClientReadReactor>(
                        channel_, ref_req, std::bind(&ClientManager::ReferenceDeviceStateEventProcessor, this, std::placeholders::_1));

                    // Re-open base station stream if they are previously requested.
                    std::unique_lock<std::mutex> lk(base_station_data_storage_list_mutex_);
                    ommo::BaseStationDataStreamRequest request;
                    for (auto& storage_ptr : base_station_data_storage_list_)
                    {
                        if (!storage_ptr->DataStreamExists())
                        {
                            rpcClientCallData* call_data_ptr = OpenBaseStationDataStream(request, std::bind(&BaseStationDataStorage::PushData, storage_ptr.get(), std::placeholders::_1), storage_ptr);
                            storage_ptr->SetDataStream(call_data_ptr);
                        }
                    }
                    lk.unlock();

                    // Re-open wireless management stream if it is previously requested.
                    OMMOLOG_INFO("Opening wireless management stream");
                    std::unique_lock<std::mutex> wl(wireless_manager_list_mutex_);
                    for (auto& manager_wrapper : wireless_manager_wrapper_list_)
                    {
                        if (!manager_wrapper->wireless_manager_ptr->IsStreamActive())
                        {
                            auto reactor = RpcWirelessManagementStreamClientBidiReactor::Create(channel_,
                                [impl = manager_wrapper->wireless_manager_ptr->p_impl_](const ommo::WirelessManagementEvent& event)
                                {
                                    impl->HandleEvent(event);
                                },
                                manager_wrapper
                            );
                            manager_wrapper->wireless_manager_ptr->p_impl_->SetClientReactor(reactor);
                        }
                    }
                    wl.unlock();
                }
                else
                {
                    OMMOLOG_INFO("gRPC channel is not ready");
                    if (device_event_stream_ptr_)
                    {
                        OMMOLOG_INFO("Stopping device event stream");
                        device_event_stream_ptr_->CancelCall();
                        device_event_stream_ptr_->WaitForDone();
                        device_event_stream_ptr_.reset();
                    }

                    if (reference_device_state_stream_ptr_)
                    {
                        OMMOLOG_INFO("Stopping reference device state stream");
                        reference_device_state_stream_ptr_->CancelCall();
                        reference_device_state_stream_ptr_->WaitForDone();
                        reference_device_state_stream_ptr_.reset();
                    }

                    // If the channel changes from ready to not-ready, the service is assumed to be offline and all devices are considered disconnected.
                    if (previous_channel_state_ == GRPC_CHANNEL_READY)
                    {
                        std::unique_lock<std::mutex> lock(connected_devices_mtx_);
                        connected_devices_.clear();
                    }
                }

                // Save the state.
                previous_channel_state_ = state;

                // call user callback if set
                if (channel_state_user_callback_)
                {
                    channel_state_user_callback_(state);
                }
            }

            // Sleep for <interval> before checking the gRPC channel state again
            std::this_thread::sleep_for(std::chrono::seconds(check_channel_interval));
        }
        OMMOLOG_INFO("Channel monitor stopped");
        if (device_event_stream_ptr_)
        {
            OMMOLOG_INFO("Stopping device event stream");
            device_event_stream_ptr_->CancelCall();
            device_event_stream_ptr_->WaitForDone();
            device_event_stream_ptr_.reset();
        }

        if (reference_device_state_stream_ptr_)
        {
            OMMOLOG_INFO("Stopping reference device state stream");
            reference_device_state_stream_ptr_->CancelCall();
            reference_device_state_stream_ptr_->WaitForDone();
            reference_device_state_stream_ptr_.reset();
        }
    }

    void ClientManager::CompletionQueueProcessor()
    {
        void* tag;
        bool ok;
        while (!stop_handling_cq_)
        {
            // Block waiting to read the next event from the completion queue. The
            // event is uniquely identified by its tag, which in this case is the
            // memory address of a CallData instance.
            // The return value of Next should always be checked. This return value
            // tells us whether there is any kind of event or cq_ is shutting down.
            if (!completion_queue_.Next(&tag, &ok))
            {
                // Server shutting down
                OMMOLOG_INFO("Completion Queue is fully drained or is shutting down. Stopping the handling of Completion Queue events");
                return;
            }

            CallDataInfo* call_data_info = static_cast<CallDataInfo*>(tag);
            rpcClientCallData* returned_call_data = static_cast<rpcClientCallData*>(call_data_info->call_data);
            if (!ok)
            {
                OMMOLOG_INFO("Call data disconnected. Stopping call data");
                // Change call data status to FINISHED
                
                returned_call_data->Stop();
            }

            ok = returned_call_data->Proceed(call_data_info->op_type);
            if (!ok)
            {
                // have to delete call_data here when it's done
                // call_data_info will be automatically deleted since they are members of the CallData object
                delete returned_call_data;
                OMMOLOG_INFO("Call data has been deleted");
            }
        }
    }

    void ClientManager::DeviceEventProcessor(const ommo::TrackingDeviceEvent& device_event)
    {
        // Convert to api type for processing. We own the memory for the duration of this function
        api::TrackingDeviceEventUPtr event_ptr = ommo::ProtoToTrackingDeviceEvent(device_event);
        uint64_t device_hash = api::Hash(event_ptr->device);
        bool device_connected = device_event.connected();

        std::unique_lock<std::mutex> lock(connected_devices_mtx_);
        if (connected_devices_.find(device_hash) != connected_devices_.end())
        {
            if (device_connected)
            {
                // Update any device descriptor changes. Create a separate copy to store in connected_devices
                connected_devices_[device_hash] = ommo::ProtoToDeviceDescriptor(device_event.device());
            }
            else
            {
                OMMOLOG_INFO("Device removal detected. Removing from connected devices. siu_uuid={} port_id={}", device_event.device().siu_uuid(), device_event.device().port_id());
                connected_devices_.erase(device_hash);
            }
        }
        else if (device_connected)
        {
            OMMOLOG_INFO("Adding connected device. siu_uuid={} port_id={}", device_event.device().siu_uuid(), device_event.device().port_id());
            // Create a separate copy to store in connected_devices
            connected_devices_[device_hash] = ommo::ProtoToDeviceDescriptor(device_event.device());
        }
        lock.unlock();

        // Update all data managers according to the device event.
        std::unique_lock<std::mutex> lk(data_manager_list_mutex_);
        for (auto& data_manager_ptr : data_manager_list_)
        {
            if (data_manager_ptr->GetDataStreamType() == api::DataStreamType::kDeviceData)
            {
                UpdateDeviceDataStream(data_manager_ptr, event_ptr->device, device_connected);
            }
            else if (data_manager_ptr->GetDataStreamType() == api::DataStreamType::kDataFrame)
            {
                UpdateDataFrameStream(data_manager_ptr, event_ptr->device, device_connected);
            }
        }
        lk.unlock();

        // Run the user-defined event callback function.
        if (device_event_user_callback_)
        {
            device_event_user_callback_(*event_ptr);
        }
    }

    void ClientManager::ReferenceDeviceStateEventProcessor(const ommo::ReferenceDeviceState& event)
    {
        api::ReferenceDeviceState ref_device_state_event = ommo::ProtoToReferenceDeviceStateEvent(event);

        // Update current reference device state
        {
            std::lock_guard<std::mutex> lock(reference_device_state_mutex_);
            current_reference_device_state_ = ref_device_state_event;
        }

        // Run the user-defined event callback function.
        if (reference_device_state_event_user_callback_)
        {
            reference_device_state_event_user_callback_(ref_device_state_event);
        }
    }

    void ClientManager::OpenDeviceDataStream(std::shared_ptr<DataManager> data_manager_ptr)
    {
        if (nullptr == data_manager_ptr || data_manager_ptr->GetDataStreamType() != api::DataStreamType::kDeviceData)
        {
            OMMOLOG_WARN("Data Manager is nullptr or is not for device data. Data stream will not be opened.");
            return;
        }

        std::unique_lock<std::mutex> lock(connected_devices_mtx_);
        for (auto& device : connected_devices_)
        {
            UpdateDeviceDataStream(data_manager_ptr, *device.second, true);
        }
    }

    void ClientManager::UpdateDeviceDataStream(std::shared_ptr<DataManager> data_manager_ptr, api::DeviceDescriptor& device, bool device_connected)
    {
        api::DeviceID device_id{ device.siu_uuid, device.port_id };
        // if invalid data manager or device is not requested by the data manager, do nothing
        if (nullptr == data_manager_ptr || !data_manager_ptr->IsDeviceDataRequested(device_id))
        {
            OMMOLOG_WARN("Data Manager is nullptr or is not for the specified device. Data stream will not be updated.");
            return;
        }

        // if device is disconnected and it is on the request list, remove its listener and its storage.
        if (!device_connected)
        {
            OMMOLOG_INFO("Cancelling data stream for device siu_uuid={} port_id={}.", device.siu_uuid, device.port_id);
            data_manager_ptr->CancelDataStream(device_id);
            data_manager_ptr->RemoveDataStream(device_id);

            // Remove the related storage for the unplugged device.
            data_manager_ptr->RemoveDeviceStorage(device_id);
        }
        // if device is connected and it is on the request list, create storage and open streams if needed
        else
        {
            // If the device storage does not exist, create storage.
            if (!data_manager_ptr->IsStorageAvailable(device_id))
            {
                OMMOLOG_INFO("Creating data storage for device siu_uuid={} port_id={}.", device.siu_uuid, device.port_id);
                data_manager_ptr->AddDeviceStorage(device);
            }

            // if listener not exist. open data stream.
            if (!data_manager_ptr->DataStreamExists(device_id))
            {
                OMMOLOG_INFO("Opening DataStream for device siu_uuid={} port_id={}.", device.siu_uuid, device.port_id);

                const api::DataRequest& data_request = data_manager_ptr->GetDataRequest();
                // Set request for this device.
                ommo::TrackingDeviceDataStreamRequest req;
                req.set_siu_uuid(device.siu_uuid);
                req.set_port_id(device.port_id);
                req.set_field_mask(data_request.data_field_mask);
                req.set_include_raw_sensor_data(data_request.include_raw_sensor_data);
                req.set_report_interval(data_request.report_interval);
                req.set_buffer_depth(data_request.buffer_depth);
                req.set_requested_fusion_mode(ommo::DeviceFusionModeToProto(data_request.requested_fusion_mode));
                // Open data stream.
                rpcClientCallData* call_data_ptr = OpenTrackingDeviceDataStream(req, std::bind(&DataManager::UpdateDeviceData, data_manager_ptr.get(), std::placeholders::_1), data_manager_ptr);
                // Save data stream pointer in data manager.
                data_manager_ptr->AddDataStream(device_id, call_data_ptr);
            }
        }
    }

    void ClientManager::OpenDataFrame(std::shared_ptr<DataManager> data_manager_ptr)
    {
        if (nullptr == data_manager_ptr || data_manager_ptr->GetDataStreamType() != api::DataStreamType::kDataFrame)
        {
            OMMOLOG_WARN("Data Manager is nullptr or is not for data frames. Data frame stream will not be opened.");
            return;
        }

        const api::DataRequest& data_request = data_manager_ptr->GetDataRequest();

        // Set data frame request. Prepare the device data storage.
        ommo::DataFrameStreamRequest req;
        req.set_report_interval(data_request.report_interval);
        req.set_buffer_depth(data_request.buffer_depth);

        std::unique_lock<std::mutex> lock(connected_devices_mtx_);
        for (auto& device_item : connected_devices_)
        {
            // only add devices that have been requested
            if (data_manager_ptr->IsDeviceDataRequested(api::DeviceID{ device_item.second->siu_uuid, device_item.second->port_id }))
            {
                ommo::DataFrameTrackingDevice* device = req.add_tracking_devices();
                device->set_siu_uuid(device_item.second->siu_uuid);
                device->set_port_id(device_item.second->port_id);
                device->set_field_mask(data_request.data_field_mask);
                device->set_include_raw_sensor_data(data_request.include_raw_sensor_data);
                device->set_requested_fusion_mode(ommo::DeviceFusionModeToProto(data_request.requested_fusion_mode));

                if (!data_manager_ptr->IsStorageAvailable(*device_item.second))
                {
                    OMMOLOG_INFO("Creating data storage for device siu_uuid={} port_id={}.", device_item.second->siu_uuid, device_item.second->port_id);
                    data_manager_ptr->AddDeviceStorage(*device_item.second);
                }
            }
        }
        lock.unlock();

        // Make request for data frame.
        rpcClientCallData* call_data_ptr = OpenDataFrameStream(req, std::bind(&DataManager::UpdateDataFrame, data_manager_ptr.get(), std::placeholders::_1), data_manager_ptr);
        data_manager_ptr->SetDataFrameStream(call_data_ptr);
    }

    void ClientManager::UpdateDataFrameStream(std::shared_ptr<DataManager> data_manager_ptr, api::DeviceDescriptor& device, bool device_connected)
    {
        api::DeviceID device_id{ device.siu_uuid, device.port_id };
        // if invalid data manager or device is not requested by the data manager, do nothing
        if (nullptr == data_manager_ptr || !data_manager_ptr->IsDeviceDataRequested(device_id))
        {
            OMMOLOG_WARN("Data Manager is nullptr or is not for the specified device. Data frame stream will not be updated.");
            return;
        }

        // if device is disconnected, remove related storage, use new dataframe listener to replace old one.
        if (!device_connected)
        {
            OMMOLOG_INFO("Removing DataStream for device siu_uuid={} port_id={}.", device.siu_uuid, device.port_id);
            data_manager_ptr->RemoveDeviceStorage(device_id);
        }

        // Always replace the old data stream with the new one, regardless of whether the device is connected or removed.
        data_manager_ptr->CancelDataFrameStream();
        data_manager_ptr->RemoveDataFrameStream();

        // Start new data frame listener.
        OpenDataFrame(data_manager_ptr);
    }

    void ClientManager::Start()
    {
        if (channel_monitor_thread_.get() == nullptr)
        {
            stop_channel_monitor_ = false;
            OMMOLOG_INFO("Starting connection monitor thread");
            channel_monitor_thread_ = std::make_unique<std::thread>(std::bind(&ClientManager::ChannelMonitor, this));
        }

        if (handle_cq_thread_.get() == nullptr)
        {
            stop_handling_cq_ = false;
            OMMOLOG_INFO("Starting completion queue processor thread");
            handle_cq_thread_ = std::make_unique<std::thread>(std::bind(&ClientManager::CompletionQueueProcessor, this));
        }
    }

    void ClientManager::Shutdown()
    {
        OMMOLOG_INFO("Cancelling all call data of tracking devices");
        std::unique_lock<std::mutex> lk(data_manager_list_mutex_);
        for (auto &data_manager_ptr : data_manager_list_)
        {
            if (data_manager_ptr->GetDataStreamType() ==  api::DataStreamType::kDeviceData)
            {
                data_manager_ptr->CancelAllDataStreams();
                data_manager_ptr->ClearDataStreams();
            }
            else
            {
                data_manager_ptr->CancelDataFrameStream();
                data_manager_ptr->RemoveDataFrameStream();
            }
        }
        lk.unlock();

        OMMOLOG_INFO("Cancelling all call data of base station data");
        std::unique_lock<std::mutex> lk2(base_station_data_storage_list_mutex_);
        for (auto &storage_ptr : base_station_data_storage_list_)
        {
            storage_ptr->CancelDataStream();
            storage_ptr->RemoveDataStream();
        }

        OMMOLOG_INFO("Stopping channel monitor");
        stop_channel_monitor_ = true;
        if (channel_monitor_thread_.get() != nullptr)
        {
            channel_monitor_thread_->join();
            channel_monitor_thread_.reset();
        }

        OMMOLOG_INFO("Shutting down completion queue");
        completion_queue_.Shutdown();

        OMMOLOG_INFO("Stopping completion queue processor");
        stop_handling_cq_ = true;
        if (handle_cq_thread_.get() != nullptr)
        {
            if (handle_cq_thread_->joinable())
            {
                handle_cq_thread_->join();
                handle_cq_thread_.reset();
            }
        }

        // Remove all created DataManagers to release our hold on the shared pointers
        // This is so they can be deleted if no one else is using them
        data_manager_list_.clear();
    }

    void ClientManager::RegisterDeviceEventCallback(std::function<void(const api::TrackingDeviceEvent&)> callback_function)
    {
        device_event_user_callback_ = callback_function;
    }

    void ClientManager::ResetDeviceEventCallback()
    {
        device_event_user_callback_ = nullptr;
    }

    void ClientManager::RegisterChannelStateCallback(std::function<void(int)> callback_function)
    {
        channel_state_user_callback_ = callback_function;
    }

    void ClientManager::ResetChannelStateCallback()
    {
        channel_state_user_callback_ = nullptr;
    }

    void ClientManager::RegisterReferenceDeviceStateEventCallback(std::function<void(const api::ReferenceDeviceState&)> callback_function)
    {
        reference_device_state_event_user_callback_ = callback_function;
    }

    void ClientManager::ResetReferenceDeviceStateEventCallback()
    {
        reference_device_state_event_user_callback_ = nullptr;
    }

    std::shared_ptr<DataManager> ClientManager::RequestDeviceData(api::DataRequest& request)
    {
        // Create data manager for request.
        std::shared_ptr<DataManager> data_manager_ptr = std::make_shared<DataManager>(request, api::DataStreamType::kDeviceData);

        std::unique_lock<std::mutex> lk(data_manager_list_mutex_);
        data_manager_list_.emplace_back(data_manager_ptr);
        lk.unlock();

        // Check current device state and open device data stream for data manager.
        OpenDeviceDataStream(data_manager_ptr);

        return data_manager_ptr;
    }

    std::shared_ptr<DataManager> ClientManager::RequestDataFrame(api::DataRequest& request)
    {
        // Create data manager for request.
        std::shared_ptr<DataManager> data_manager_ptr = std::make_shared<DataManager>(request, api::DataStreamType::kDataFrame);

        std::unique_lock<std::mutex> lk(data_manager_list_mutex_);
        data_manager_list_.emplace_back(data_manager_ptr);
        lk.unlock();

        // Check current device state and open data frame stream for data manager.
        OpenDataFrame(data_manager_ptr);

        return data_manager_ptr;
    }
    /*
     * Unlike ClientContext, if users request base station data through ClientManager, 
     * a storage and backend stream are created for each request.
     */
    std::shared_ptr<BaseStationDataStorage> ClientManager::RequestBaseStationData()
    {
        std::shared_ptr<BaseStationDataStorage> storage_ptr = std::make_shared<BaseStationDataStorage>();

        ommo::BaseStationDataStreamRequest request;
        rpcClientCallData* call_data_ptr = OpenBaseStationDataStream(request, std::bind(&BaseStationDataStorage::PushData, storage_ptr.get(), std::placeholders::_1), storage_ptr);
        storage_ptr->SetDataStream(call_data_ptr);

        std::unique_lock<std::mutex> lk(base_station_data_storage_list_mutex_);
        base_station_data_storage_list_.push_back(storage_ptr);

        return storage_ptr;
    }

    void ClientManager::CloseBaseStationDataRequest(std::shared_ptr<BaseStationDataStorage> storage_ptr)
    {
        std::unique_lock<std::mutex> lk(base_station_data_storage_list_mutex_);
        if (nullptr != storage_ptr)
        {
            for (auto it = base_station_data_storage_list_.begin(); it != base_station_data_storage_list_.end();)
            {
                if ((*it) == storage_ptr)
                {
                    // Cancel data stream. The Completion Queue will delete the call data.
                    (*it)->CancelDataStream();

                    it = base_station_data_storage_list_.erase(it);
                    break;
                }
            }
        }
    }

    void ClientManager::CloseRequest(std::shared_ptr<DataManager> data_manager_ptr)
    {
        if (nullptr == data_manager_ptr)
        {
            OMMOLOG_WARN("Data Manager is nullptr. Request cannot be closed.");
            return;
        }

        if (data_manager_ptr->GetDataStreamType() ==  api::DataStreamType::kDeviceData)
        {
            // Remove all its related data stream. call data will be deleted.
            OMMOLOG_INFO("Cancelling all device stream call data from Data Manager");
            data_manager_ptr->CancelAllDataStreams();

            // Clear all its stream pointer.
            OMMOLOG_INFO("Clearing device stream call data from Data Manager");
            data_manager_ptr->ClearDataStreams();
        }
        else
        {
            OMMOLOG_INFO("Cancelling data frame stream and removing its pointer from Data Manager");
            data_manager_ptr->CancelDataFrameStream();
            data_manager_ptr->RemoveDataFrameStream();
        }

        // Remove from client's data manager list.
        OMMOLOG_INFO("Removing data manager");
        std::unique_lock<std::mutex> lk(data_manager_list_mutex_);
        for (auto it = data_manager_list_.begin(); it != data_manager_list_.end(); it++)
        {
            if (*it == data_manager_ptr)
            {
                data_manager_list_.erase(it);
                break;
            }
        }
        lk.unlock();
    }

    api::DataLogState ClientManager::EnableDataLogging(std::string directory, std::string file_name, bool overwrite)
    {
        // If overwrite is false, check if the file already exists.
        if (!overwrite)
        {
            auto full_path = std::filesystem::path(directory) / file_name;
            if (std::filesystem::exists(full_path))
            {
                SPDLOG_ERROR("File already exists. Overwrite is set to false. Logging request will not be sent.");
                return api::DataLogState::kError;
            }
        }

        ommo::DataLoggingRequest data_logging_request;
        data_logging_request.set_enable_logging(true);
        data_logging_request.set_directory(directory);
        data_logging_request.set_file_name(file_name);
        data_logging_request.set_overwrite(overwrite);
        ommo::DataLoggingResponse reply;

        std::unique_ptr<ommo::CoreService::Stub> stub = ommo::CoreService::NewStub(channel_);
        ClientContext grpc_client_context;
        Status status = stub->SendDataLoggingRequest(&grpc_client_context, data_logging_request, &reply);

        if (status.ok())
        {
            return ommo::ProtoToDataLogState(reply.log_state());
        }
        else // RPC failure
        {
            return api::DataLogState::kRpcFail;
        }
    }

    api::DataLogState ClientManager::DisableDataLogging()
    {
        ommo::DataLoggingRequest data_logging_request;
        data_logging_request.set_enable_logging(false);
        ommo::DataLoggingResponse reply;

        std::unique_ptr<ommo::CoreService::Stub> stub = ommo::CoreService::NewStub(channel_);
        ClientContext grpc_client_context;
        Status status = stub->SendDataLoggingRequest(&grpc_client_context, data_logging_request, &reply);

        if (status.ok())
        {
            return ommo::ProtoToDataLogState(reply.log_state());
        }
        else // RPC failure
        {
            return api::DataLogState::kRpcFail;
        }
    }

    std::shared_ptr<api::WirelessManager> ClientManager::CreateWirelessManager()
    {
        auto manager_wrapper = std::make_shared<WirelessManagerWrapper>(std::make_shared<api::WirelessManager>());

        auto reactor = RpcWirelessManagementStreamClientBidiReactor::Create(channel_,
            [impl = manager_wrapper->wireless_manager_ptr->p_impl_](const ommo::WirelessManagementEvent& event)
            {
                impl->HandleEvent(event);
            },
            manager_wrapper
        );
        manager_wrapper->wireless_manager_ptr->p_impl_->SetClientReactor(reactor);

        std::unique_lock<std::mutex> wl(wireless_manager_list_mutex_);
        wireless_manager_wrapper_list_.push_back(manager_wrapper);

        return manager_wrapper->wireless_manager_ptr;
    }

    void ClientManager::DeleteWirelessManager(api::WirelessManager* wireless_manager)
    {
        std::unique_lock<std::mutex> wl(wireless_manager_list_mutex_);
        for (auto it = wireless_manager_wrapper_list_.begin(); it != wireless_manager_wrapper_list_.end();)
        {
            if ((*it)->wireless_manager_ptr.get() == wireless_manager)
            {
                it = wireless_manager_wrapper_list_.erase(it);
                break;
            }
            else
            {
                ++it;
            }
        }
    }

    bool ClientManager::SelectReferenceDevice(bool enabled, uint32_t siu_uuid, uint32_t port_num)
    {
        api::SelectReferenceDeviceRequest request;
        request.enabled = enabled;
        request.siu_uuid = siu_uuid;
        request.port_num = port_num;

        ommo::SelectReferenceDeviceRequest proto_request = ommo::SelectReferenceDeviceRequestToProto(request);
        ommo::SelectReferenceDeviceResponse reply;

        std::unique_ptr<ommo::CoreService::Stub> stub = ommo::CoreService::NewStub(channel_);
        ClientContext grpc_client_context;
        Status status = stub->SelectReferenceDevice(&grpc_client_context, proto_request, &reply);

        if (status.ok())
        {
            api::SelectReferenceDeviceResponse api_response = ommo::ProtoToSelectReferenceDeviceResponse(reply);
            return api_response.success;
        }
        else // RPC failure
        {
            return false;
        }
    }

    api::ReferenceDeviceState ClientManager::GetCurrentReferenceDeviceState()
    {
        std::lock_guard<std::mutex> lock(reference_device_state_mutex_);
        return current_reference_device_state_;
    }
}  // namespace ommo
