/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#include "data_manager.h"
#include "logger_base.h"
#include "protobuf_converters.h"
#include "sdk_utils.h"

namespace ommo
{
    DataManager::DataManager(const api::DataRequest& request, api::DataStreamType stream_type)
        // make a deep copy of request
        : stream_type_(stream_type)
    {
        api::MoveAndDeletePtr(request_, api::CopyDataRequest(request));
    }


    const api::DataRequest& DataManager::GetDataRequest() const
    {
        return request_;
    }

    api::DataStreamType DataManager::GetDataStreamType() const
    {
        return stream_type_;
    }

    bool DataManager::IsDeviceDataRequested(const api::DeviceID& device_id)
    {
        // An empty request list means all devices are requested
        if (request_.requested_devices == nullptr || request_.requested_device_count == 0)
        {
            return true;
        }

        // If the user provided a request list, check if the specified device was included
        for (int i = 0; i < request_.requested_device_count; i++)
        {
            if (device_id.siu_uuid == request_.requested_devices[i].siu_uuid && device_id.port_id == request_.requested_devices[i].port_id)
            {
                return true;
            }
        }

        // The device was not found in the request list
        return false;
    }

    api::DeviceIDListUPtr DataManager::GetDeviceStorageList()
    {
        api::DeviceIDListUPtr result(new api::DeviceIDList);

        std::shared_lock<std::shared_mutex> lk(device_data_map_mtx_);
        result->device_count = device_data_map_.size();
        result->devices = new api::DeviceID[result->device_count];
        int i = 0;
        for (auto& ele : device_data_map_)
        {
            result->devices[i] = api::DeviceID{ ele.second->GetUUID(), ele.second->GetPortId() };
            i++;
        }
        return result;
    }

    void DataManager::AddDeviceStorage(const api::DeviceDescriptor& device, int32_t buffer_size)
    {
        uint64_t hash = api::Hash(device);

        std::unique_lock<std::shared_mutex> lk(device_data_map_mtx_);
        // Check if the storage already exists before creating a new one
        if (device_data_map_.find(hash) == device_data_map_.end())
        {
            device_data_map_.emplace(hash, std::make_unique<DeviceDataStorage>(device, buffer_size));
            OMMOLOG_INFO("Adding data storage for device. Siu: {}, Port Id: {}", device.siu_uuid, device.port_id);
        }
    }

    void DataManager::RemoveDeviceStorage(uint64_t hash)
    {
        std::unique_lock<std::shared_mutex> lk(device_data_map_mtx_);

        if (device_data_map_.find(hash) != device_data_map_.end())
        {
            device_data_map_.erase(hash);
            OMMOLOG_INFO("Erasing data storage for {}", hash);
        }
    }

    void DataManager::RemoveDeviceStorage(const api::DeviceDescriptor& device)
    {
        RemoveDeviceStorage(Hash(device));
    }

    void DataManager::RemoveDeviceStorage(const api::DeviceID& device_id)
    {
        RemoveDeviceStorage(Hash(device_id));
    }

    bool DataManager::IsStorageAvailable(uint64_t hash)
    {
        std::shared_lock<std::shared_mutex> lk(device_data_map_mtx_);
        return device_data_map_.find(hash) != device_data_map_.end();
    }

    bool DataManager::IsStorageAvailable(const api::DeviceDescriptor& device)
    {
        return IsStorageAvailable(Hash(device));
    }

    bool DataManager::IsStorageAvailable(const api::DeviceID& device_id)
    {
        return IsStorageAvailable(Hash(device_id));
    }

    void DataManager::UpdateDeviceData(const ommo::TrackingDeviceData& packet)
    {
        // Add SDK receive timestamp to the protobuf data before conversion
        ommo::TrackingDeviceData packet_copy(packet);
        auto* ts = packet_copy.add_latency_timestamps();
        ts->set_timestamp_type(ommo::LATENCY_TIMESTAMP_TYPE_SDK_RECEIVED);
        ts->set_steady_timestamp_milliseconds(std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now().time_since_epoch()).count());
        ts->set_system_timestamp_milliseconds(std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count());

        std::shared_lock<std::shared_mutex> lk(device_data_map_mtx_);

        uint64_t device_hash = api::Hash(packet_copy.siu_uuid(), packet_copy.port_id());

        auto storage = device_data_map_.find(device_hash);
        if (storage != device_data_map_.end())
        {
            storage->second->PushData(packet_copy);
        }

        if (device_data_user_callback_)
        {
            // convert to api UPtr type to be automaitcally destroyed after callback
            api::TrackingDeviceDataUPtr cb_packet = ProtoToTrackingDeviceData(packet_copy);
            device_data_user_callback_(*cb_packet);
        }
    }

    void DataManager::UpdateDataFrame(const ommo::DataFrame& packet)
    {
        // Add SDK receive timestamp to each device data before conversion
        ommo::DataFrame packet_copy(packet);
        const uint64_t steady_ts = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now().time_since_epoch()).count();
        const uint64_t system_ts = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count();
        for (int i = 0; i < packet_copy.device_data_size(); i++)
        {
            auto* ts = packet_copy.mutable_device_data(i)->add_latency_timestamps();
            ts->set_timestamp_type(ommo::LATENCY_TIMESTAMP_TYPE_SDK_RECEIVED);
            ts->set_steady_timestamp_milliseconds(steady_ts);
            ts->set_system_timestamp_milliseconds(system_ts);
        }

        std::shared_lock<std::shared_mutex> lk(device_data_map_mtx_);

        for (int i = 0; i < packet_copy.device_data_size(); i++)
        {
            uint64_t device_hash = api::Hash(packet_copy.device_data(i).siu_uuid(), packet_copy.device_data(i).port_id());
            auto it = device_data_map_.find(device_hash);
            if (it != device_data_map_.end())
            {
                it->second->PushData(packet_copy.device_data(i));
            }
        }

        if (data_frame_user_callback_)
        {
            // convert to api UPtr type to be automaitcally destroyed after callback
            api::DataFrameUPtr cb_packet = ProtoToDataFrame(packet_copy);
            data_frame_user_callback_(*cb_packet);
        }
    }

    void DataManager::RegisterTrackingDeviceDataCallback(std::function<void(const api::TrackingDeviceData&)> callback_function)
    {
        if (stream_type_ != api::DataStreamType::kDeviceData)
        {
            OMMOLOG_WARN("Cannot register TrackingDeviceData callback for a stream type that's not DeviceData.");
            return;
        }

        device_data_user_callback_ = callback_function;
    }

    void DataManager::ResetTrackingDeviceDataCallback()
    {
        device_data_user_callback_ = nullptr;
    }

    void DataManager::RegisterDataFrameCallback(std::function<void(const api::DataFrame&)> callback_function)
    {
        if (stream_type_ != api::DataStreamType::kDataFrame)
        {
            OMMOLOG_WARN("Cannot register DataFrame callback for a stream type that's not DataFrame.");
            return;
        }

        data_frame_user_callback_ = callback_function;
    }

    void DataManager::ResetDataFrameCallback()
    {
        data_frame_user_callback_ = nullptr;
    }

    api::DataResponseUPtr DataManager::GetLatestData(const api::DeviceID& device_id, std::chrono::milliseconds timeout_threshold)
    {
        api::DataResponseUPtr result(new api::DataResponse{ api::DataResponseState::kNoData, nullptr, 0 });
        uint64_t hash = api::Hash(device_id);
        // Lock the data map and find the storage for this device
        std::shared_lock<std::shared_mutex> lk(device_data_map_mtx_);
        auto storage = device_data_map_.find(hash);
        if (storage != device_data_map_.end())
        {
            result = storage->second->GetLatestData(timeout_threshold);
        }
        return result;
    }

    api::DataResponseUPtr DataManager::GetDataWithMaxAge(const api::DeviceID& device_id, std::chrono::milliseconds max_age)
    {
        api::DataResponseUPtr result(new api::DataResponse{ api::DataResponseState::kNoData, nullptr, 0 });
        uint64_t hash = api::Hash(device_id);
        // Lock the data map and find the storage for this device
        std::shared_lock<std::shared_mutex> lk(device_data_map_mtx_);
        auto storage = device_data_map_.find(hash);
        if (storage != device_data_map_.end())
        {
            result = storage->second->GetDataWithMaxAge(max_age);
        }
        return result;
    }

    api::DataResponseUPtr DataManager::GetLatestData(const api::DeviceID& device_id, uint32_t count)
    {
        api::DataResponseUPtr result(new api::DataResponse{ api::DataResponseState::kNoData, nullptr, 0 });
        // Lock the data map and find the storage for this device
        std::shared_lock<std::shared_mutex> lk(device_data_map_mtx_);
        uint64_t hash = api::Hash(device_id);
        auto storage = device_data_map_.find(hash);
        if (storage != device_data_map_.end())
        {
            result = storage->second->GetLatestData(count);
        }
        return result;
    }

    api::DataResponseUPtr DataManager::GetDataSinceIndex(const api::DeviceID& device_id, uint32_t start_idx)
    {
        api::DataResponseUPtr result(new api::DataResponse{ api::DataResponseState::kNoData, nullptr, 0 });
        uint64_t hash = api::Hash(device_id);
        // Lock the data map and find the storage for this device
        std::shared_lock<std::shared_mutex> lk(device_data_map_mtx_);
        auto storage = device_data_map_.find(hash);
        if (storage != device_data_map_.end())
        {
            result = storage->second->GetDataSinceIndex(start_idx);
        }
        return result;
    }

    bool DataManager::AddDataStream(const api::DeviceID& device_id, rpcClientCallData* call_data)
    {
        std::unique_lock<std::mutex> lk(data_stream_map_mtx_);
        uint64_t hash = api::Hash(device_id);
        if (device_data_streams_.find(hash) == device_data_streams_.end())
        {
            device_data_streams_[hash] = call_data;
            return true;
        }
        OMMOLOG_WARN("Failed to add device stream for device siu_uuid={} port_id={}. Call data already exists", device_id.siu_uuid, device_id.port_id);
        return false;
    }

    bool DataManager::RemoveDataStream(const api::DeviceID& device_id)
    {
        std::unique_lock<std::mutex> lk(data_stream_map_mtx_);
        uint64_t hash = api::Hash(device_id);
        if (device_data_streams_.find(hash) != device_data_streams_.end())
        {
            device_data_streams_.erase(hash);
            return true;
        }
        OMMOLOG_WARN("Failed to remove data stream for device siu_uuid={} port_id={}. Data stream pointer does not exist", device_id.siu_uuid, device_id.port_id);
        return false;
    }

    bool DataManager::DataStreamExists(const api::DeviceID& device_id)
    {
        std::unique_lock<std::mutex> lk(data_stream_map_mtx_);
        uint64_t hash = api::Hash(device_id);
        if (device_data_streams_.find(hash) != device_data_streams_.end())
        {
            return true;
        }
        return false;
    }

    bool DataManager::CancelDataStream(const api::DeviceID& device_id)
    {
        std::unique_lock<std::mutex> lk(data_stream_map_mtx_);
        uint64_t hash = api::Hash(device_id);
        if (device_data_streams_.find(hash) != device_data_streams_.end())
        {
            device_data_streams_[hash]->CancelCall();
            return true;
        }
        OMMOLOG_WARN("Failed to cancel data stream for device siu_uuid={} port_id={}. Data stream pointer does not exist.", device_id.siu_uuid, device_id.port_id);
        return false;
    }

    void DataManager::CancelAllDataStreams()
    {
        std::unique_lock<std::mutex> lk(data_stream_map_mtx_);
        for (auto& [device_hash, stream]: device_data_streams_)
        {
            stream->CancelCall();
        }
    }

    void DataManager::ClearDataStreams()
    {
        std::unique_lock<std::mutex> lk(data_stream_map_mtx_);
        device_data_streams_.clear();
    }

    bool DataManager::CancelDataFrameStream()
    {
        std::lock_guard<std::mutex> lock(dataframe_stream_mtx_);
        if (dataframe_stream_)
        {
            dataframe_stream_->CancelCall();
            return true;
        }
        return false;
    }

    void DataManager::RemoveDataFrameStream()
    {
        std::lock_guard<std::mutex> lock(dataframe_stream_mtx_);
        dataframe_stream_ = nullptr;
    }

    void DataManager::SetDataFrameStream(rpcClientCallData* call_data)
    {
        std::lock_guard<std::mutex> lock(dataframe_stream_mtx_);
        dataframe_stream_ = call_data;
    }

    bool DataManager::RemoveStream(rpcClientCallData* call_data)
    {
        std::unique_lock<std::mutex> dataframe_stream_lock(dataframe_stream_mtx_);
        if (dataframe_stream_ == call_data)
        {
            dataframe_stream_ = nullptr;
            return true;
        }
        dataframe_stream_lock.unlock();

        std::unique_lock<std::mutex> data_stream_map_lock(data_stream_map_mtx_);
        auto it = std::find_if(device_data_streams_.begin(), device_data_streams_.end(),
                            [&call_data](const auto& item) {return item.second == call_data;});
        if (it != device_data_streams_.end())
        {
            device_data_streams_.erase(it);
            return true;
        }
        data_stream_map_lock.unlock();

        OMMOLOG_WARN("Failed to remove device stream. stream pointer does not exists");
        return false;
    }

    bool DataManager::ClearAssociation(void* call_data_ptr)
    {
        return RemoveStream(static_cast<rpcClientCallData*>(call_data_ptr));
    }
}  // namespace ommo
