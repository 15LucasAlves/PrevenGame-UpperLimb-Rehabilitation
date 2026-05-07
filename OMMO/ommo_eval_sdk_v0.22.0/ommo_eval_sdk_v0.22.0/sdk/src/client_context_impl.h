/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
 */
#pragma once

#include "client_manager.h"
#include "wireless_manager_impl.h"

namespace ommo::api
{
    class ClientContext::impl
    {
        public:
            explicit impl(const char* server_address);

            void Start();

            void Shutdown();

            api::TrackingDevices* GetTrackingDevices();

            api::HardwareStates* GetHardwareStates();

            bool SetBaseStationMotorRunning(bool active);

            void RegisterDeviceEventCallback(std::function<void(const api::TrackingDeviceEvent&)> callback_function);

            void ResetDeviceEventCallback();

            void RegisterChannelStateCallback(std::function<void(int)> callback_function);

            void ResetChannelStateCallback();

            void RegisterReferenceDeviceStateEventCallback(std::function<void(const api::ReferenceDeviceState& event)> callback_function);

            void ResetReferenceDeviceStateEventCallback();

            uint32_t RequestDeviceData(api::DataRequest& request);

            uint32_t RequestDataFrame(api::DataRequest& request);

            void CloseRequest(uint32_t request_tag);

            api::DeviceIDList* GetAvailableDeviceList(uint32_t request_tag);

            // Get data functions
            api::DataResponse* GetLatestData(uint32_t request_tag, const api::DeviceID& device_id);

            api::DataResponse* GetLatestData(uint32_t request_tag, const api::DeviceID& device_id, std::chrono::milliseconds timeout_threshold);

            api::DataResponse* GetLatestData(uint32_t request_tag, const api::DeviceID& device_id, int32_t num_packets);

            api::DataResponse* GetDataSinceIndex(uint32_t request_tag, const api::DeviceID& device_id, int32_t start_index);

            api::DataResponse* GetDataWithMaxAge(uint32_t request_tag, const api::DeviceID& device_id, std::chrono::milliseconds max_age);

            uint32_t RequestBaseStationData();

            void CloseBaseStationDataRequest(uint32_t request_tag);

            api::BaseStationDataResponse* GetLatestBaseStationData(uint32_t request_tag);

            api::BaseStationDataResponse* GetLatestBaseStationData(uint32_t request_tag, int32_t num_packets);

            api::BaseStationDataResponse* GetBaseStationDataSinceIndex(uint32_t request_tag, int32_t start_index);

            void RegisterTrackingDeviceDataCallback(uint32_t request_tag, std::function<void(const api::TrackingDeviceData&)> callback_function);

            void ResetTrackingDeviceDataCallback(uint32_t request_tag);

            void RegisterDataFrameCallback(uint32_t request_tag, std::function<void(const api::DataFrame&)> callback_function);

            void ResetDataFrameCallback(uint32_t request_tag);

            api::WirelessManager* CreateWirelessManager();

            void DeleteWirelessManager(api::WirelessManager* wireless_manager);

            api::DataLogState EnableDataLogging(std::string directory, std::string file_name, bool overwrite);

            api::DataLogState DisableDataLogging();

            bool SelectReferenceDevice(bool enabled, uint32_t siu_uuid, uint32_t port_num);

        private:
            std::unique_ptr<ommo::ClientManager> client_manager_;

            std::shared_mutex data_manager_map_mutex_;
            std::unordered_map<uint32_t, std::shared_ptr<ommo::DataManager>> data_managers_;
            uint32_t data_managers_tag_source_ = 1;

            std::shared_ptr<ommo::BaseStationDataStorage> base_station_data_storage_;

            std::shared_mutex base_station_request_list_mutex_;
            std::unordered_set<uint32_t> base_station_request_list_;
    };
}
