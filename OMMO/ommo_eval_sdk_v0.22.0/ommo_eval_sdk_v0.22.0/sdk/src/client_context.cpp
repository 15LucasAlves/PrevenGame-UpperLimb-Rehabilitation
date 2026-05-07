/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/
#include "client_context.h"
#include "client_context_impl.h"

#include <string>
#include <memory>
#include <unordered_map>
#include "client_manager.h"
#include "spdlog_logger.h"

namespace ommo::api
{
    ClientContext::ClientContext(const char* server_address) : p_impl_(new ClientContext::impl(server_address)) {}

    ClientContext::~ClientContext()
    {
        p_impl_->Shutdown();
        delete p_impl_;
    }

    void ClientContext::Start()
    {
        p_impl_->Start();
    }

    void ClientContext::Shutdown()
    {
        p_impl_->Shutdown();
    }

    void ClientContext::SetupLogging(const char* file_name)
    {
        SetLogger(std::make_unique<SpdLogLogger>());

        SpdLogConfig cfg;
        if (file_name && *file_name)
        {
            cfg.file_name = file_name;
        }
        ConfigureSpdLog(cfg);
    }

    api::TrackingDevices* ClientContext::GetTrackingDevices()
    {
        return p_impl_->GetTrackingDevices();
    }

    api::HardwareStates* ClientContext::GetHardwareStates()
    {
        return p_impl_->GetHardwareStates();
    }

    bool ClientContext::SetBaseStationMotorRunning(bool active)
    {
        return p_impl_->SetBaseStationMotorRunning(active);
    }

    void ClientContext::RegisterDeviceEventCallback(std::function<void(const api::TrackingDeviceEvent&)> callback_function)
    {
        p_impl_->RegisterDeviceEventCallback(callback_function);
    }

    void ClientContext::ResetDeviceEventCallback()
    {
        p_impl_->ResetDeviceEventCallback();
    }

    void ClientContext::RegisterChannelStateCallback(std::function<void(int)> callback_function)
    {
        p_impl_->RegisterChannelStateCallback(callback_function);
    }

    void ClientContext::ResetChannelStateCallback()
    {
        p_impl_->ResetChannelStateCallback();
    }

    void ClientContext::RegisterReferenceDeviceStateEventCallback(std::function<void(const api::ReferenceDeviceState& event)> callback_function)
    {
        p_impl_->RegisterReferenceDeviceStateEventCallback(callback_function);
    }

    void ClientContext::ResetReferenceDeviceStateEventCallback()
    {
        p_impl_->ResetReferenceDeviceStateEventCallback();
    }

    uint32_t ClientContext::RequestDeviceData(api::DataRequest& request)
    {
        return p_impl_->RequestDeviceData(request);
    }

    uint32_t ClientContext::RequestDataFrame(api::DataRequest& request)
    {
        return p_impl_->RequestDataFrame(request);
    }

    uint32_t ClientContext::RequestBaseStationData()
    {
        return p_impl_->RequestBaseStationData();
    }
    void ClientContext::CloseBaseStationDataRequest(uint32_t request_tag)
    {
        return p_impl_->CloseBaseStationDataRequest(request_tag);
    }

    api::BaseStationDataResponse* ClientContext::GetLatestBaseStationData(uint32_t request_tag)
    {
        return p_impl_->GetLatestBaseStationData(request_tag);
    }

    api::BaseStationDataResponse* ClientContext::GetLatestBaseStationData(uint32_t request_tag, int32_t num_packets)
    {
        return p_impl_->GetLatestBaseStationData(request_tag, num_packets);
    }

    api::BaseStationDataResponse* ClientContext::GetBaseStationDataSinceIndex(uint32_t request_tag, int32_t start_index)
    {
        return p_impl_->GetBaseStationDataSinceIndex(request_tag, start_index);
    }

    void ClientContext::CloseRequest(uint32_t request_tag)
    {
        p_impl_->CloseRequest(request_tag);
    }

    api::DeviceIDList* ClientContext::GetAvailableDeviceList(uint32_t request_tag)
    {
        return p_impl_->GetAvailableDeviceList(request_tag);
    }

    api::DataResponse* ClientContext::GetLatestData(uint32_t request_tag, const api::DeviceID& device_id)
    {
        return p_impl_->GetLatestData(request_tag, device_id);
    }

    api::DataResponse* ClientContext::GetLatestData(uint32_t request_tag, const api::DeviceID& device_id, std::chrono::milliseconds timeout_threshold)
    {
        return p_impl_->GetLatestData(request_tag, device_id, timeout_threshold);
    }

    api::DataResponse* ClientContext::GetLatestData(uint32_t request_tag, const api::DeviceID& device_id, int32_t num_packets)
    {
        return p_impl_->GetLatestData(request_tag, device_id, num_packets);
    }

    api::DataResponse* ClientContext::GetDataSinceIndex(uint32_t request_tag, const api::DeviceID& device_id, int32_t start_index)
    {
        return p_impl_->GetDataSinceIndex(request_tag, device_id, start_index);
    }

    api::DataResponse* ClientContext::GetDataWithMaxAge(uint32_t request_tag, const api::DeviceID& device_id, std::chrono::milliseconds max_age)
    {
        return p_impl_->GetDataWithMaxAge(request_tag, device_id, max_age);
    }

    void ClientContext::RegisterTrackingDeviceDataCallback(uint32_t request_tag, std::function<void(const api::TrackingDeviceData&)> callback_function)
    {
        p_impl_->RegisterTrackingDeviceDataCallback(request_tag, callback_function);
    }

    void ClientContext::ResetTrackingDeviceDataCallback(uint32_t request_tag)
    {
        p_impl_->ResetTrackingDeviceDataCallback(request_tag);
    }

    void ClientContext::RegisterDataFrameCallback(uint32_t request_tag, std::function<void(const api::DataFrame&)> callback_function)
    {
        p_impl_->RegisterDataFrameCallback(request_tag, callback_function);
    }

    void ClientContext::ResetDataFrameCallback(uint32_t request_tag)
    {
        p_impl_->ResetDataFrameCallback(request_tag);
    }

    api::WirelessManager* ClientContext::CreateWirelessManager()
    {
        return p_impl_->CreateWirelessManager();
    }

    void ClientContext::DeleteWirelessManager(api::WirelessManager* wireless_manager)
    {
        p_impl_->DeleteWirelessManager(wireless_manager);
    }

    api::DataLogState ClientContext::EnableDataLogging(const char* directory, const char* file_name, bool overwrite)
    {
        std::string directory_str = std::string(directory);
        std::string file_name_str = std::string(file_name);
        return p_impl_->EnableDataLogging(directory_str, file_name_str, overwrite);
    }

    api::DataLogState ClientContext::DisableDataLogging()
    {
        return p_impl_->DisableDataLogging();
    }

    bool ClientContext::SelectReferenceDevice(bool enabled, uint32_t siu_uuid, uint32_t port_num)
    {
        return p_impl_->SelectReferenceDevice(enabled, siu_uuid, port_num);
    }

    ClientContext::impl::impl(const char* server_address)
    {
        std::string address = "localhost:50051";
        if (server_address != nullptr && server_address[0] != '\0')
        {
            address = std::string(server_address);
        }
        client_manager_ = std::make_unique<ommo::ClientManager>(address);
    }

    void ClientContext::impl::Start()
    {
        client_manager_->Start();
    }

    void ClientContext::impl::Shutdown()
    {
        client_manager_->Shutdown();
    }

    api::TrackingDevices* ClientContext::impl::GetTrackingDevices()
    {
        return client_manager_->GetTrackingDevices().release();
    }

    api::HardwareStates* ClientContext::impl::GetHardwareStates()
    {
        return client_manager_->GetHardwareStates().release();
    }

    bool ClientContext::impl::SetBaseStationMotorRunning(bool active)
    {
        return client_manager_->SetBaseStationMotorRunning(active);
    }

    void ClientContext::impl::RegisterDeviceEventCallback(std::function<void(const api::TrackingDeviceEvent&)> callback_function)
    {
        client_manager_->RegisterDeviceEventCallback(callback_function);
    }

    void ClientContext::impl::ResetDeviceEventCallback()
    {
        client_manager_->ResetDeviceEventCallback();
    }

    void ClientContext::impl::RegisterChannelStateCallback(std::function<void(int)> callback_function)
    {
        client_manager_->RegisterChannelStateCallback(callback_function);
    }

    void ClientContext::impl::ResetChannelStateCallback()
    {
        client_manager_->ResetChannelStateCallback();
    }

    void ClientContext::impl::RegisterReferenceDeviceStateEventCallback(std::function<void(const api::ReferenceDeviceState& event)> callback_function)
    {
        client_manager_->RegisterReferenceDeviceStateEventCallback(callback_function);
    }

    void ClientContext::impl::ResetReferenceDeviceStateEventCallback()
    {
        client_manager_->ResetReferenceDeviceStateEventCallback();
    }

    uint32_t ClientContext::impl::RequestDeviceData(api::DataRequest& request)
    {
        std::shared_ptr<ommo::DataManager> manager = client_manager_->RequestDeviceData(request);
        std::unique_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        uint32_t current_tag = data_managers_tag_source_++;
        data_managers_[current_tag] = manager;
        return current_tag;
    }

    uint32_t ClientContext::impl::RequestDataFrame(api::DataRequest& request)
    {
        std::shared_ptr<ommo::DataManager> manager = client_manager_->RequestDataFrame(request);
        std::unique_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        uint32_t current_tag = data_managers_tag_source_++;
        data_managers_[current_tag] = manager;
        return current_tag;
    }

    void ClientContext::impl::CloseRequest(uint32_t request_tag)
    {
        std::unique_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        auto item = data_managers_.find(request_tag);
        if (item != data_managers_.end())
        {
            client_manager_->CloseRequest(item->second);
            data_managers_.erase(item);
        }
    }

    api::DeviceIDList* ClientContext::impl::GetAvailableDeviceList(uint32_t request_tag)
    {
        std::shared_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        auto item = data_managers_.find(request_tag);
        if (item != data_managers_.end())
        {
            return item->second->GetDeviceStorageList().release();
        }
        return new api::DeviceIDList{ nullptr, 0 };
    }

    // Get data functions
    api::DataResponse* ClientContext::impl::GetLatestData(uint32_t request_tag, const api::DeviceID& device_id)
    {
        std::shared_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        auto item = data_managers_.find(request_tag);
        if (item != data_managers_.end())
        {
            return item->second->GetLatestData(device_id).release();
        }
        return new api::DataResponse{ api::DataResponseState::kNoData, nullptr, 0 };
    }

    api::DataResponse* ClientContext::impl::GetLatestData(uint32_t request_tag, const api::DeviceID& device_id, std::chrono::milliseconds timeout_threshold)
    {
        std::shared_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        auto item = data_managers_.find(request_tag);
        if (item != data_managers_.end())
        {
            return item->second->GetLatestData(device_id, timeout_threshold).release();
        }
        return new api::DataResponse{ api::DataResponseState::kNoData, nullptr, 0 };
    }

    api::DataResponse* ClientContext::impl::GetLatestData(uint32_t request_tag, const api::DeviceID& device_id, int32_t num_packets)
    {
        std::shared_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        auto item = data_managers_.find(request_tag);
        if (item != data_managers_.end())
        {
            return item->second->GetLatestData(device_id, num_packets).release();
        }
        return new api::DataResponse{ api::DataResponseState::kNoData, nullptr, 0 };
    }

    api::DataResponse* ClientContext::impl::GetDataSinceIndex(uint32_t request_tag, const api::DeviceID& device_id, int32_t start_index)
    {
        std::shared_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        auto item = data_managers_.find(request_tag);
        if (item != data_managers_.end())
        {
            return item->second->GetDataSinceIndex(device_id, start_index).release();
        }
        return new api::DataResponse{ api::DataResponseState::kNoData, nullptr, 0 };
    }

    api::DataResponse* ClientContext::impl::GetDataWithMaxAge(uint32_t request_tag, const api::DeviceID& device_id, std::chrono::milliseconds max_age)
    {
        std::shared_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        auto item = data_managers_.find(request_tag);
        if (item != data_managers_.end())
        {
            return item->second->GetDataWithMaxAge(device_id, max_age).release();
        }
        return new api::DataResponse{ api::DataResponseState::kNoData, nullptr, 0 };
    }

    uint32_t ClientContext::impl::RequestBaseStationData()
    {
        /*
         * When users request base station data through ClientContext, a single storage and backend stream are created.
         * Multiple requests share this storage and stream. Each request gets a unique tag for closing or retrieving data.
         */
        std::unique_lock<std::shared_mutex> lock(base_station_request_list_mutex_);
        if (nullptr == base_station_data_storage_)
        {
            base_station_data_storage_ = client_manager_->RequestBaseStationData();
        }
        uint32_t current_tag = data_managers_tag_source_++;
        base_station_request_list_.insert(current_tag);

        return current_tag;
    }

    void ClientContext::impl::CloseBaseStationDataRequest(uint32_t request_tag)
    {
        std::unique_lock<std::shared_mutex> lock(base_station_request_list_mutex_);
        auto it = base_station_request_list_.find(request_tag);
        if (it != base_station_request_list_.end())
        {
            base_station_request_list_.erase(it);
        }

        // Remove the stream and storage if all request is closed.
        if (base_station_request_list_.empty())
        {
            client_manager_->CloseBaseStationDataRequest(base_station_data_storage_);
        }
    }

    api::BaseStationDataResponse* ClientContext::impl::GetLatestBaseStationData(uint32_t request_tag)
    {
        std::unique_lock<std::shared_mutex> lock(base_station_request_list_mutex_);
        if (nullptr == base_station_data_storage_ || base_station_request_list_.find(request_tag) == base_station_request_list_.end())
        {
            return new api::BaseStationDataResponse{ api::DataResponseState::kNoData, nullptr, 0 };
        }
        lock.unlock();

        return base_station_data_storage_->GetLatestData().release();
    }

    api::BaseStationDataResponse* ClientContext::impl::GetLatestBaseStationData(uint32_t request_tag, int32_t num_packets)
    {
        std::unique_lock<std::shared_mutex> lock(base_station_request_list_mutex_);
        if (nullptr == base_station_data_storage_ || base_station_request_list_.find(request_tag) == base_station_request_list_.end())
        {
            return new api::BaseStationDataResponse{ api::DataResponseState::kNoData, nullptr, 0 };
        }
        lock.unlock();

        return base_station_data_storage_->GetLatestData(num_packets).release();
    }

    api::BaseStationDataResponse* ClientContext::impl::GetBaseStationDataSinceIndex(uint32_t request_tag, int32_t start_index)
    {
        std::unique_lock<std::shared_mutex> lock(base_station_request_list_mutex_);
        if (nullptr == base_station_data_storage_ || base_station_request_list_.find(request_tag) == base_station_request_list_.end())
        {
            return new api::BaseStationDataResponse{ api::DataResponseState::kNoData, nullptr, 0 };
        }
        lock.unlock();

        return base_station_data_storage_->GetDataSinceIndex(start_index).release();
    }

    void ClientContext::impl::RegisterTrackingDeviceDataCallback(uint32_t request_tag, std::function<void(const api::TrackingDeviceData&)> callback_function)
    {
        std::shared_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        auto item = data_managers_.find(request_tag);
        if (item != data_managers_.end())
        {
            item->second->RegisterTrackingDeviceDataCallback(callback_function);
        }
    }

    void ClientContext::impl::ResetTrackingDeviceDataCallback(uint32_t request_tag)
    {
        std::shared_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        auto item = data_managers_.find(request_tag);
        if (item != data_managers_.end())
        {
            item->second->ResetTrackingDeviceDataCallback();
        }
    }

    void ClientContext::impl::RegisterDataFrameCallback(uint32_t request_tag, std::function<void(const api::DataFrame&)> callback_function)
    {
        std::shared_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        auto item = data_managers_.find(request_tag);
        if (item != data_managers_.end())
        {
            item->second->RegisterDataFrameCallback(callback_function);
        }
    }

    void ClientContext::impl::ResetDataFrameCallback(uint32_t request_tag)
    {
        std::shared_lock<std::shared_mutex> lock(data_manager_map_mutex_);
        auto item = data_managers_.find(request_tag);
        if (item != data_managers_.end())
        {
            item->second->ResetDataFrameCallback();
        }
    }

    api::WirelessManager* ClientContext::impl::CreateWirelessManager()
    {
        return client_manager_->CreateWirelessManager().get();
    }

    void ClientContext::impl::DeleteWirelessManager(api::WirelessManager* wireless_manager)
    {
        client_manager_->DeleteWirelessManager(wireless_manager);
    }

    api::DataLogState ClientContext::impl::EnableDataLogging(std::string directory, std::string file_name, bool overwrite)
    {
        return client_manager_->EnableDataLogging(directory, file_name, overwrite);
    }

    api::DataLogState ClientContext::impl::DisableDataLogging()
    {
        return client_manager_->DisableDataLogging();
    }

    bool ClientContext::impl::SelectReferenceDevice(bool enabled, uint32_t siu_uuid, uint32_t port_num)
    {
        return client_manager_->SelectReferenceDevice(enabled, siu_uuid, port_num);
    }
} // namespace ommo::api

// C API implementation
extern "C" {
    /*
     * ------------------------------------------------------------------------
     * ClientContext Creation and Management
     * ------------------------------------------------------------------------
     */

    OmmoClientContext* ClientContext_Create(const char* server_address)
    {
        try {
            return new ommo::api::ClientContext(server_address);
        }
        catch (...) {
            return nullptr;
        }
    }

    void ClientContext_Destroy(OmmoClientContext* context)
    {
        if (context) {
            delete static_cast<ommo::api::ClientContext*>(context);
        }
    }

    void ClientContext_Start(OmmoClientContext* context)
    {
        if (context) {
            static_cast<ommo::api::ClientContext*>(context)->Start();
        }
    }

    void ClientContext_Shutdown(OmmoClientContext* context)
    {
        if (context) {
            static_cast<ommo::api::ClientContext*>(context)->Shutdown();
        }
    }

    void ClientContext_SetupLogging(OmmoClientContext* context, const char* file_name)
    {
        if (context) {
            static_cast<ommo::api::ClientContext*>(context)->SetupLogging(file_name);
        }
    }

    /*
     * ------------------------------------------------------------------------
     * Device and Hardware Information
     * ------------------------------------------------------------------------
     */

    ommo::api::TrackingDevices* ClientContext_GetTrackingDevices(OmmoClientContext* context)
    {
        if (context) {
            return static_cast<ommo::api::ClientContext*>(context)->GetTrackingDevices();
        }
        return nullptr;
    }

    ommo::api::HardwareStates* ClientContext_GetHardwareStates(OmmoClientContext* context)
    {
        if (context) {
            return static_cast<ommo::api::ClientContext*>(context)->GetHardwareStates();
        }
        return nullptr;
    }

    bool ClientContext_SetBaseStationMotorRunning(OmmoClientContext* context, bool active)
    {
        if (context) {
            return static_cast<ommo::api::ClientContext*>(context)->SetBaseStationMotorRunning(active);
        }
        return false;
    }

    /*
     * ------------------------------------------------------------------------
     * Event Callbacks
     * ------------------------------------------------------------------------
     */

    void ClientContext_RegisterDeviceEventCallback(OmmoClientContext* context, DeviceEventCallback callback_function)
    {
        if (context && callback_function) {
            static_cast<ommo::api::ClientContext*>(context)->RegisterDeviceEventCallback([callback_function](const ommo::api::TrackingDeviceEvent& event) {
                callback_function(&event);
            });
        }
    }

    void ClientContext_ResetDeviceEventCallback(OmmoClientContext* context)
    {
        if (context) {
            static_cast<ommo::api::ClientContext*>(context)->ResetDeviceEventCallback();
        }
    }

    void ClientContext_RegisterChannelStateCallback(OmmoClientContext* context, ChannelStateCallback callback_function)
    {
        if (context && callback_function) {
            static_cast<ommo::api::ClientContext*>(context)->RegisterChannelStateCallback([callback_function](int state) {
                callback_function(state);
            });
        }
    }

    void ClientContext_ResetChannelStateCallback(OmmoClientContext* context)
    {
        if (context) {
            static_cast<ommo::api::ClientContext*>(context)->ResetChannelStateCallback();
        }
    }

    void ClientContext_RegisterReferenceDeviceStateEventCallback(OmmoClientContext* context, std::function<void(const ommo::api::ReferenceDeviceState&)> callback_function)
    {
        if (context && callback_function) {
            static_cast<ommo::api::ClientContext*>(context)->RegisterReferenceDeviceStateEventCallback(callback_function);
        }
    }

    void ClientContext_ResetReferenceDeviceStateEventCallback(OmmoClientContext* context)
    {
        if (context) {
            static_cast<ommo::api::ClientContext*>(context)->ResetReferenceDeviceStateEventCallback();
        }
    }

    /*
     * ------------------------------------------------------------------------
     * Data Requests
     * ------------------------------------------------------------------------
     */

    uint32_t ClientContext_RequestDeviceData(OmmoClientContext* context, const ommo::api::DataRequest* request)
    {
        if (context && request) {
            // Create a non-const copy since the C++ function takes a reference
            ommo::api::DataRequest mutable_request = *request;
            return static_cast<ommo::api::ClientContext*>(context)->RequestDeviceData(mutable_request);
        }
        return 0;
    }

    uint32_t ClientContext_RequestDataFrame(OmmoClientContext* context, const ommo::api::DataRequest* request)
    {
        if (context && request) {
            // Create a non-const copy since the C++ function takes a reference
            ommo::api::DataRequest mutable_request = *request;
            return static_cast<ommo::api::ClientContext*>(context)->RequestDataFrame(mutable_request);
        }
        return 0;
    }

    void ClientContext_CloseRequest(OmmoClientContext* context, uint32_t request_tag)
    {
        if (context) {
            static_cast<ommo::api::ClientContext*>(context)->CloseRequest(request_tag);
        }
    }

    uint32_t ClientContext_RequestBaseStationData(OmmoClientContext* context)
    {
        if (context) {
            return static_cast<ommo::api::ClientContext*>(context)->RequestBaseStationData();
        }
        return 0;
    }

    void ClientContext_CloseBaseStationDataRequest(OmmoClientContext* context, uint32_t request_tag)
    {
        if (context) {
            static_cast<ommo::api::ClientContext*>(context)->CloseBaseStationDataRequest(request_tag);
        }
    }

    /*
     * ------------------------------------------------------------------------
     * Data Retrieval
     * ------------------------------------------------------------------------
     */

    ommo::api::DeviceIDList* ClientContext_GetAvailableDeviceList(OmmoClientContext* context, uint32_t request_tag)
    {
        if (context) {
            return static_cast<ommo::api::ClientContext*>(context)->GetAvailableDeviceList(request_tag);
        }
        return nullptr;
    }

    ommo::api::DataResponse* ClientContext_GetLatestData(OmmoClientContext* context, uint32_t request_tag, const ommo::api::DeviceID* device_id)
    {
        if (context && device_id) {
            return static_cast<ommo::api::ClientContext*>(context)->GetLatestData(request_tag, *device_id);
        }
        return nullptr;
    }

    ommo::api::DataResponse* ClientContext_GetLatestDataWithTimeout(OmmoClientContext* context, uint32_t request_tag, const ommo::api::DeviceID* device_id, int64_t timeout_threshold_ms)
    {
        if (context && device_id) {
            return static_cast<ommo::api::ClientContext*>(context)->GetLatestData(request_tag, *device_id, std::chrono::milliseconds(timeout_threshold_ms));
        }
        return nullptr;
    }

    ommo::api::DataResponse* ClientContext_GetDataWithMaxAge(OmmoClientContext* context, uint32_t request_tag, const ommo::api::DeviceID* device_id, int64_t max_age_ms)
    {
        if (context && device_id) {
            return static_cast<ommo::api::ClientContext*>(context)->GetDataWithMaxAge(request_tag, *device_id, std::chrono::milliseconds(max_age_ms));
        }
        return nullptr;
    }

    ommo::api::DataResponse* ClientContext_GetLatestDataWithCount(OmmoClientContext* context, uint32_t request_tag, const ommo::api::DeviceID* device_id, int32_t num_packets)
    {
        if (context && device_id) {
            return static_cast<ommo::api::ClientContext*>(context)->GetLatestData(request_tag, *device_id, num_packets);
        }
        return nullptr;
    }

    ommo::api::DataResponse* ClientContext_GetDataSinceIndex(OmmoClientContext* context, uint32_t request_tag, const ommo::api::DeviceID* device_id, int32_t start_index)
    {
        if (context && device_id) {
            return static_cast<ommo::api::ClientContext*>(context)->GetDataSinceIndex(request_tag, *device_id, start_index);
        }
        return nullptr;
    }

    ommo::api::BaseStationDataResponse* ClientContext_GetLatestBaseStationData(OmmoClientContext* context, uint32_t request_tag)
    {
        if (context) {
            return static_cast<ommo::api::ClientContext*>(context)->GetLatestBaseStationData(request_tag);
        }
        return nullptr;
    }

    ommo::api::BaseStationDataResponse* ClientContext_GetLatestBaseStationDataWithCount(OmmoClientContext* context, uint32_t request_tag, int32_t num_packets)
    {
        if (context) {
            return static_cast<ommo::api::ClientContext*>(context)->GetLatestBaseStationData(request_tag, num_packets);
        }
        return nullptr;
    }

    ommo::api::BaseStationDataResponse* ClientContext_GetBaseStationDataSinceIndex(OmmoClientContext* context, uint32_t request_tag, int32_t start_index)
    {
        if (context) {
            return static_cast<ommo::api::ClientContext*>(context)->GetBaseStationDataSinceIndex(request_tag, start_index);
        }
        return nullptr;
    }

    /*
     * ------------------------------------------------------------------------
     * Data Stream Callbacks
     * ------------------------------------------------------------------------
     */

    void ClientContext_RegisterTrackingDeviceDataCallback(OmmoClientContext* context, uint32_t request_tag, TrackingDeviceDataCallback callback_function)
    {
        if (context && callback_function) {
            static_cast<ommo::api::ClientContext*>(context)->RegisterTrackingDeviceDataCallback(request_tag, [callback_function](const ommo::api::TrackingDeviceData& data) {
                callback_function(&data);
            });
        }
    }

    void ClientContext_ResetTrackingDeviceDataCallback(OmmoClientContext* context, uint32_t request_tag)
    {
        if (context) {
            static_cast<ommo::api::ClientContext*>(context)->ResetTrackingDeviceDataCallback(request_tag);
        }
    }

    void ClientContext_RegisterDataFrameCallback(OmmoClientContext* context, uint32_t request_tag, DataFrameCallback callback_function)
    {
        if (context && callback_function) {
            static_cast<ommo::api::ClientContext*>(context)->RegisterDataFrameCallback(request_tag, [callback_function](const ommo::api::DataFrame& frame) {
                callback_function(&frame);
            });
        }
    }

    void ClientContext_ResetDataFrameCallback(OmmoClientContext* context, uint32_t request_tag)
    {
        if (context) {
            static_cast<ommo::api::ClientContext*>(context)->ResetDataFrameCallback(request_tag);
        }
    }

    /*
     * ------------------------------------------------------------------------
     * Wireless Manager
     * ------------------------------------------------------------------------
     */

    OmmoWirelessManager* ClientContext_CreateWirelessManager(OmmoClientContext* context)
    {
        if (context) {
            return static_cast<OmmoWirelessManager*>(static_cast<ommo::api::ClientContext*>(context)->CreateWirelessManager());
        }
        return nullptr;
    }

    void ClientContext_DeleteWirelessManager(OmmoClientContext* context, OmmoWirelessManager* wireless_manager)
    {
        if (context && wireless_manager) {
            static_cast<ommo::api::ClientContext*>(context)->DeleteWirelessManager(static_cast<ommo::api::WirelessManager*>(wireless_manager));
        }
    }

    /*
     * ------------------------------------------------------------------------
     * Data Logging
     * ------------------------------------------------------------------------
     */

    ommo::api::DataLogState ClientContext_EnableDataLogging(OmmoClientContext* context, const char* directory, const char* file_name, bool overwrite)
    {
        if (context && directory && file_name) {
            return static_cast<ommo::api::ClientContext*>(context)->EnableDataLogging(directory, file_name, overwrite);
        }
        return ommo::api::DataLogState::kError;
    }

    ommo::api::DataLogState ClientContext_DisableDataLogging(OmmoClientContext* context)
    {
        if (context) {
            return static_cast<ommo::api::ClientContext*>(context)->DisableDataLogging();
        }
        return ommo::api::DataLogState::kError;
    }
}
