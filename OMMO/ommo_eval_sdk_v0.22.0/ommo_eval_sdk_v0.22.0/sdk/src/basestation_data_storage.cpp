/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#include "basestation_data_storage.h"
#include "logger_base.h"
#include "protobuf_converters.h"

namespace ommo
{

    BaseStationDataStorage::BaseStationDataStorage(uint32_t buffer_size) : buffer_size_(buffer_size), packet_received_num_(0)
    {
        // Allocate memory for data buffer and info buffer.
        packet_buffer1_ = new api::BaseStationPacket[buffer_size_];
        packet_buffer2_ = new api::BaseStationPacket[buffer_size_];

        // Initialize buffer memory so they are not random values
        for (int i = 0; i < buffer_size_; i++)
        {
            // Initialize the buffers to the default value to ensure there is no bad
            // data in the buffers. Otherwise, the can be pointing to random locations
            packet_buffer1_[i] = {};
            packet_buffer2_[i] = {};
        }

        // Initialize the read and write buffer info.
        read_buffer_.packet_buffer_ptr = packet_buffer1_;
        read_buffer_.data_num.store(0);
        write_buffer_.packet_buffer_ptr = packet_buffer2_;
        write_buffer_.data_num.store(0);
    }

    BaseStationDataStorage::~BaseStationDataStorage()
    {
        delete[] packet_buffer1_;
        delete[] packet_buffer2_;
    }

    bool BaseStationDataStorage::PushData(const ommo::BaseStationData& packet)
    {
        // Save the data.
        const int32_t write_idx = write_buffer_.data_num;
        if (write_idx < buffer_size_)
        {
            // Save raw data.
            write_buffer_.packet_buffer_ptr[write_idx].packet_idx = packet_received_num_++;

            write_buffer_.packet_buffer_ptr[write_idx].base_station_data = ommo::ProtoToBaseStationData(packet);

            write_buffer_.data_num++;

            if (write_buffer_.data_num == buffer_size_)
            {
                // Switch write and read buffers when the write buffer is full.
                SwitchBufferPointer();
                // Reset the writer buffer (previous read buffer) to save new data.
                write_buffer_.data_num.store(0);
            }
        }
        else
        {
            SwitchBufferPointer();

            // Save raw data.
            write_buffer_.packet_buffer_ptr[0].packet_idx = packet_received_num_++;
    
            write_buffer_.packet_buffer_ptr[0].base_station_data = ommo::ProtoToBaseStationData(packet);

            write_buffer_.data_num.store(1);
        }
        return true;
    }

    api::BaseStationDataResponseUPtr BaseStationDataStorage::GetLatestData()
    {
        api::BaseStationDataResponseUPtr result(new api::BaseStationDataResponse{ api::DataResponseState::kNoData, nullptr, 0 });
        // The write and read buffers cannot switch while reading data.
        std::shared_lock<std::shared_mutex> lock(switch_mutex_);

        // If write_buffer already has data, get the latest data from it.
        int32_t idx = write_buffer_.data_num - 1;
        if (idx >= 0)
        {
            result->packets = new api::BaseStationPacket[1];
            result->packets[0] = write_buffer_.packet_buffer_ptr[idx];
            result->packet_count = 1;
            result->state = api::DataResponseState::kSuccess;
        }
        else
        {
            idx = read_buffer_.data_num - 1;
            if (idx >= 0)
            {
                result->packets = new api::BaseStationPacket[1];
                result->packets[0] = write_buffer_.packet_buffer_ptr[idx];
                result->packet_count = 1;
                result->state = api::DataResponseState::kSuccess;
            }
        }
        // If the read and write buffers are both empty, return default value.
        return result;
    }

    api::BaseStationDataResponseUPtr BaseStationDataStorage::GetLatestData(uint32_t request_count)
    {
        api::BaseStationDataResponseUPtr result(new api::BaseStationDataResponse{ api::DataResponseState::kNoData, nullptr, 0 });

        // The write and read buffers cannot switch while reading data.
        std::shared_lock<std::shared_mutex> lock(switch_mutex_);

        const int32_t write_packet_num = write_buffer_.data_num;
        const int32_t read_packet_num = read_buffer_.data_num;

        // Only get data if request_count is not 0
        if (request_count > 0)
        {
            if (request_count <= write_packet_num)
            {
                // Read from write buffer.
                uint32_t start_idx = write_packet_num - request_count;
                result->packets = new api::BaseStationPacket[request_count];

                for (int32_t i = 0; i < request_count; i++)
                {
                    result->packets[i] = write_buffer_.packet_buffer_ptr[start_idx + i];
                }
                result->packet_count = request_count;
                result->state = api::DataResponseState::kSuccess;
            }
            else if (request_count <= read_packet_num + write_packet_num)
            {
                int32_t idx = read_packet_num + write_packet_num - request_count;
                result->packets = new api::BaseStationPacket[request_count];

                // First read from the read buffer.
                // Note: variable i is used when getting data from read buffer and write buffer.
                int32_t i = 0;
                for (; idx < read_packet_num; idx++)
                {
                    result->packets[i] = read_buffer_.packet_buffer_ptr[idx];

                    i++;
                }
                // Then read from write buffer.
                for (idx = 0; idx < write_packet_num; idx++)
                {
                    result->packets[i] = write_buffer_.packet_buffer_ptr[idx];
                    i++;
                }
                result->packet_count = request_count;
                result->state = api::DataResponseState::kSuccess;
            }
            else if (read_packet_num + write_packet_num > 0)
            {
                OMMOLOG_WARN("Some requested packets are not available. request_count={} total_count={}", request_count, read_packet_num + write_packet_num);
                result->packets = new api::BaseStationPacket[read_packet_num + write_packet_num];
                // First read from read buffer.
                // Note: variable i is used when getting data from read buffer and write buffer.
                int32_t i = 0;
                for (int32_t idx = 0; idx < read_packet_num; idx++)
                {
                    result->packets[i] = read_buffer_.packet_buffer_ptr[idx];
                    i++;
                }
                // Then read from write buffer.
                for (int32_t idx = 0; idx < write_packet_num; idx++)
                {
                    result->packets[i] = write_buffer_.packet_buffer_ptr[idx];
                    i++;
                }
                result->packet_count = read_packet_num + write_packet_num;
                result->state = api::DataResponseState::kPartialData;
            }
        }
        return result;
    }

    api::BaseStationDataResponseUPtr BaseStationDataStorage::GetDataSinceIndex(uint32_t start_idx)
    {
        api::BaseStationDataResponseUPtr result(new api::BaseStationDataResponse{ api::DataResponseState::kNoData, nullptr, 0 });

        // The write and read buffers cannot switch while reading data.
        std::shared_lock<std::shared_mutex> lock(switch_mutex_);

        const int32_t write_packet_num = write_buffer_.data_num;
        const int32_t read_packet_num = read_buffer_.data_num;

        // First check the write buffer.
        if (write_packet_num > 0 && write_buffer_.packet_buffer_ptr[write_packet_num - 1].packet_idx < start_idx)
        {
            OMMOLOG_WARN("Requested packet is not available yet. request_idx={} latest_idx={}", start_idx, write_buffer_.packet_buffer_ptr[write_packet_num - 1].packet_idx);
        }
        // If the first packet in write buffer has index <= start_idx, it contains all of the required packets.
        else if (write_packet_num > 0 && write_buffer_.packet_buffer_ptr[0].packet_idx <= start_idx)
        {
            int32_t idx = start_idx - write_buffer_.packet_buffer_ptr[0].packet_idx;
            uint32_t packet_count = write_packet_num - idx;
            result->packets = new api::BaseStationPacket[packet_count];

            for (int32_t i = 0; i < packet_count; i++)
            {
                result->packets[i] = write_buffer_.packet_buffer_ptr[idx + i];
            }
            result->state = api::DataResponseState::kSuccess;
            result->packet_count = packet_count;
        }
        // If the first packet in read buffer has index <= start_idx, packets will be required from both the read and write buffers
        else if (read_packet_num > 0 && read_buffer_.packet_buffer_ptr[0].packet_idx <= start_idx)
        {
            int32_t idx = start_idx - read_buffer_.packet_buffer_ptr[0].packet_idx;
            result->packets = new api::BaseStationPacket[read_packet_num - idx + write_packet_num];

            // Note: variable i is used when getting data from read buffer and write buffer.
            int32_t i = 0;
            for (; idx < read_packet_num; idx++)
            {
                result->packets[i] = read_buffer_.packet_buffer_ptr[idx];
                i++;
            }
            for (idx = 0; idx < write_packet_num; idx++)
            {
                result->packets[i] = write_buffer_.packet_buffer_ptr[idx];
                i++;
            }
            result->state = api::DataResponseState::kSuccess;
            result->packet_count = read_packet_num + write_packet_num + read_buffer_.packet_buffer_ptr[0].packet_idx - start_idx;
        }
        else if (read_packet_num > 0 && read_buffer_.packet_buffer_ptr[0].packet_idx > start_idx)
        {
            OMMOLOG_WARN("Some requested packets are not available. request_idx={} earliest_idx={}", start_idx, read_buffer_.packet_buffer_ptr[0].packet_idx);
            result->packets = new api::BaseStationPacket[read_packet_num + write_packet_num];
            int32_t i = 0;
            // First read from read buffer.
            for (int32_t idx = 0; idx < read_packet_num; idx++)
            {
                result->packets[i] = read_buffer_.packet_buffer_ptr[idx];
                i++;
            }
            // Then read from write buffer.
            for (int32_t idx = 0; idx < write_packet_num; idx++)
            {
                result->packets[i] = write_buffer_.packet_buffer_ptr[idx];
                i++;
            }
            result->state = api::DataResponseState::kPartialData;
            result->packet_count = write_packet_num + read_packet_num;
        }
        return result;
    }

    void BaseStationDataStorage::SetDataStream(rpcClientCallData* call_data)
    {
        std::unique_lock<std::mutex> lock(base_station_stream_mtx_);
        base_station_stream_ = call_data;
    }

    void BaseStationDataStorage::RemoveDataStream()
    {
        std::unique_lock<std::mutex> lock(base_station_stream_mtx_);
        base_station_stream_ = nullptr;
    }

    void BaseStationDataStorage::CancelDataStream()
    {
        std::unique_lock<std::mutex> lock(base_station_stream_mtx_);
        if (base_station_stream_)
        {
            base_station_stream_->CancelCall();
        }
    }

    bool BaseStationDataStorage::DataStreamExists()
    {
        std::unique_lock<std::mutex> lock(base_station_stream_mtx_);
        return (base_station_stream_ != nullptr);
    }

    bool BaseStationDataStorage::ClearAssociation(void* call_data_ptr)
    {
        std::unique_lock<std::mutex> lock(base_station_stream_mtx_);
        if (base_station_stream_ == static_cast<rpcClientCallData*>(call_data_ptr))
        {
            base_station_stream_ = nullptr;
            return true;
        }
        return false;
    }

    void BaseStationDataStorage::SwitchBufferPointer()
    {
        std::unique_lock<std::shared_mutex> lock(switch_mutex_);

        api::BaseStationPacket* temp_ptr = write_buffer_.packet_buffer_ptr;
        write_buffer_.packet_buffer_ptr = read_buffer_.packet_buffer_ptr;
        read_buffer_.packet_buffer_ptr = temp_ptr;

        const int read_num = read_buffer_.data_num;
        const int write_num = write_buffer_.data_num.exchange(read_num);
        read_buffer_.data_num.store(write_num);
    }

}  // namespace ommo
