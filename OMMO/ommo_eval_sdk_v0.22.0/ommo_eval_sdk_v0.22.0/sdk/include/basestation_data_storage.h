/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#include <atomic>
#include <shared_mutex>
#include "ommo_service_api.pb.h"
#include "rpcClientCallData.h"
#include "sdk_types.h"


namespace ommo
{
    class BaseStationDataStorage : public CallDataAssociation
    {
    private:
        struct BufferInfo
        {
            std::atomic_int32_t data_num;
            api::BaseStationPacket* packet_buffer_ptr;
        };

        void SwitchBufferPointer();

        uint32_t buffer_size_;

        // Record the packet index.
        uint32_t packet_received_num_;

        api::BaseStationPacket* packet_buffer1_;
        api::BaseStationPacket* packet_buffer2_;

        BufferInfo read_buffer_;
        BufferInfo write_buffer_;
        std::shared_mutex switch_mutex_;

        std::mutex base_station_stream_mtx_;
        rpcClientCallData* base_station_stream_ = nullptr;

    public:
        BaseStationDataStorage(uint32_t buffer_size = 500);
        ~BaseStationDataStorage();

        bool PushData(const ommo::BaseStationData& m);

        // Return the most recent packet.
        api::BaseStationDataResponseUPtr GetLatestData();

        // Return the most recent <count> packets.
        api::BaseStationDataResponseUPtr GetLatestData(uint32_t count);

        // TODO: This function currently does not handle when packet_idx wraps
        // TODO: Implement proper wrapping handling when packet_idx overflows uint32
        // Return all packets starting from start_idx;
        api::BaseStationDataResponseUPtr GetDataSinceIndex(uint32_t start_idx);

        void SetDataStream(rpcClientCallData* call_data);
        void RemoveDataStream();
        void CancelDataStream();
        bool DataStreamExists();

        virtual bool ClearAssociation(void* call_data_ptr) override;
    };
}  // namespace ommo
