/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#include <chrono>
#include <map>
#include <shared_mutex>
#include <vector>

#include "device_data_storage.h"
#include "ommo_service_api.pb.h"
#include "rpcClientCallData.h"
#include "sdk_types.h"


namespace ommo
{
    class DataManager : public CallDataAssociation
    {
    public:
        DataManager(const api::DataRequest& request, api::DataStreamType stream_type);
        ~DataManager() = default;

        // Get the DataRequest assigned to this DataManager
        const api::DataRequest& GetDataRequest() const;
        api::DataStreamType GetDataStreamType() const;

        bool IsDeviceDataRequested(const api::DeviceID& device_id);

        // Return the list of devices with created storage
        api::DeviceIDListUPtr GetDeviceStorageList();

        // Storage related functions
        void AddDeviceStorage(const api::DeviceDescriptor& device, int32_t buffer_size = 500);
        void RemoveDeviceStorage(uint64_t hash);
        void RemoveDeviceStorage(const api::DeviceDescriptor& device);
        void RemoveDeviceStorage(const api::DeviceID& device_id);

        bool IsStorageAvailable(uint64_t hash);
        bool IsStorageAvailable(const api::DeviceDescriptor& device);
        bool IsStorageAvailable(const api::DeviceID& device_id);

        void UpdateDeviceData(const ommo::TrackingDeviceData& packet);
        void UpdateDataFrame(const ommo::DataFrame& packet);

        // Register a call back to be called whenever a TrackingDeviceData is received via UpdateDeviceData
        // Register function will do nothing unless stream_type of the DataManager is kDeviceData
        // Only one call back can be registered at a time. Registering another callback will overwrite the existing one.
        void RegisterTrackingDeviceDataCallback(std::function<void(const api::TrackingDeviceData&)> callback_function);
        // Reset the currently registered callback for TrackingDeviceData so it'll no longer be called
        void ResetTrackingDeviceDataCallback();

        // Register a call back to be called whenever a DataFrame is received via UpdateDataFrame
        // Register function will do nothing unless stream_type of the DataManager is kDataFrame
        // Only one call back can be registered at a time. Registering another callback will overwrite the existing one.
        void RegisterDataFrameCallback(std::function<void(const api::DataFrame&)> callback_function);
        // Reset the currently registered callback for DataFrame so it'll no longer be called
        void ResetDataFrameCallback();

        // Get the latest data for the requested device.
        // If timeout_threshold_milliseconds is zero, return the latest data available.
        // If timeout_threshold_milliseconds is non-zero, return the latest data received within the timeout threshold.
        // If no data is received within the timeout threshold, return an empty DataResponse.
        api::DataResponseUPtr GetLatestData(const api::DeviceID& device_id, std::chrono::milliseconds timeout_threshold = std::chrono::milliseconds(0));
        // Get the latest <num_packets> of data for the requested device
        api::DataResponseUPtr GetLatestData(const api::DeviceID& device_id, uint32_t num_packets);
        // Get all data since <start_idx> for the requested device
        api::DataResponseUPtr GetDataSinceIndex(const api::DeviceID& device_id, uint32_t start_idx);
        // Get all data received within the specified max_age
        api::DataResponseUPtr GetDataWithMaxAge(const api::DeviceID& device_id, std::chrono::milliseconds max_age);

        // Store the data stream pointer of a tracking device to this DataManager.
        bool AddDataStream(const api::DeviceID& device_id, rpcClientCallData* call_data);
        // Remove the data stream of the given tracking device from this DataManager.
        bool RemoveDataStream(const api::DeviceID& device_id);
        // Check if the data stream pointer of the given devices exists.
        bool DataStreamExists(const api::DeviceID& device_id);
        // Cancel the data stream associated with the given tracking device. The call data will be deleted by completion queue.
        bool CancelDataStream(const api::DeviceID& device_id); 

        // Cancel all data streams associated with this DataManager.
        void CancelAllDataStreams();
        // Remove all data stream pointer saved in this DataManager.
        void ClearDataStreams();
        // Store the DataFrame stream pointer in this DataManager.
        void SetDataFrameStream(rpcClientCallData* call_data);
        // Remove the DataFrame stream pointer from this DataManager.
        void RemoveDataFrameStream();
        // Cancel the DataFrame stream associated with this DataManager. The call data will be deleted by completion queue.
        bool CancelDataFrameStream();

        // Remove the stream pointer from this DataManager if it is stored.
        bool RemoveStream(rpcClientCallData* call_data);

        virtual bool ClearAssociation(void* call_data_ptr) override;

    private:
        // Lock to protect access to the device data map
        std::shared_mutex device_data_map_mtx_;
        // Storage for device data storage
        std::map<uint64_t, std::unique_ptr<DeviceDataStorage>> device_data_map_;

        // Lock to protect access to the data stream map
        std::mutex data_stream_map_mtx_;
        // Storage for the mapping between tracking devices' hash and their data stream pointer.
        std::unordered_map<uint64_t, rpcClientCallData*> device_data_streams_;

        // The associated data frame stream pointer. There can only be one DataFrame request associated with a DataManager
        std::mutex dataframe_stream_mtx_;
        rpcClientCallData* dataframe_stream_ = nullptr;

        // The device data callback function provided by user.
        std::function<void(const api::TrackingDeviceData& packet)> device_data_user_callback_;
        // The data frame callback function provided by user.
        std::function<void(const api::DataFrame& packet)> data_frame_user_callback_;

        // request_ and stream_typs_ are initialized when DataManager is created.
        api::DataRequest request_;
        const api::DataStreamType stream_type_;
    };

}  // namespace ommo
