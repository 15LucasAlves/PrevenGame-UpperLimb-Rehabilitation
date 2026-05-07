/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
 */

#include <iostream>
#include <iomanip>
#include <thread>
#include <string>
#include <chrono>
#include <unordered_map>
#include <mutex>
#include <deque>
#include <sstream>
#include <atomic>

#include "client_context.h"
#include "sdk_utils.h"


// Flag to signal the printing thread to terminate
std::atomic_bool stop_printing_data = false;

// Mutex for data print queue
std::mutex base_station_queue_mtx;
std::deque<ommo::api::BaseStationPacket> base_station_data_queue;

std::atomic_uint32_t base_station_packet_count = 0;

/*
 * This function prints out the base station packets received and demonstrates
 * the data fields available in each packet
 */
void PrintBaseStationData(uint32_t sleep_interval)
{
    // Run until signaled to stop
    while (!stop_printing_data)
    {
        std::unique_lock<std::mutex> lock(base_station_queue_mtx);
        while (base_station_data_queue.size() > 0)
        {
            // Get the first data to print
            ommo::api::BaseStationPacket result = std::move(base_station_data_queue.front());
            base_station_data_queue.pop_front();
            lock.unlock();

            std::stringstream output_ss;

            output_ss << " idx=" << result.packet_idx
                      << "  accel_figure_of_merit=" << result.base_station_data.accel_figure_of_merit.value << ":" << result.base_station_data.accel_figure_of_merit.out_of_spec
                      << "  max_phase_std=" << result.base_station_data.max_phase_std.value << ":" << result.base_station_data.max_phase_std.out_of_spec
                      << "  max_phase_drift=" << result.base_station_data.max_phase_drift.value << ":" << result.base_station_data.max_phase_drift.out_of_spec
                      << "  temp_diff_from_calib_c=" << result.base_station_data.temp_diff_from_calib_c.value << ":" << result.base_station_data.temp_diff_from_calib_c.out_of_spec
                      << "  mean_rotation_rate_hz=" << result.base_station_data.mean_rotation_rate_hz.value << ":" << result.base_station_data.mean_rotation_rate_hz.out_of_spec
                      << "  tilt_angle_deg=" << result.base_station_data.tilt_angle_deg.value << ":" << result.base_station_data.tilt_angle_deg.out_of_spec
                      << "  max_mag_rms_res=" << result.base_station_data.max_mag_rms_res.value << ":" << result.base_station_data.max_mag_rms_res.out_of_spec
                      << "  accel_dc_magnitude_g=" << result.base_station_data.accel_dc_magnitude_g.value << ":" << result.base_station_data.accel_dc_magnitude_g.out_of_spec
                      << std::endl;

            std::cout << output_ss.str();

            // lock before the next loop
            lock.lock();
        }
        lock.unlock();
        // Sleep for the specified interval to wait for more data
        std::this_thread::sleep_for(std::chrono::milliseconds(sleep_interval));
    }
}

int main(int argc, char *argv[])
{
    // Create a Client Context. This will establish the connection to ommo service.
    // localhost:50051 is the default gRPC address for ommo service.
    ommo::api::ClientContext client_context("localhost:50051");

    // Start the client context. It will begin monitoring device events and be ready to handle requests.
    client_context.Start();

    // Create a default tracking data request and request tracking device data to make the base station start running.
    ommo::api::DataRequestUPtr req_ptr(ommo::api::CreateDefaultDataRequest());
    uint32_t tracking_device_data_request_tag = client_context.RequestDeviceData(*req_ptr);

    // Request base station data.
    std::cout << "[Main] Requesting data for base station.\n";
    uint32_t base_station_data_request_tag = client_context.RequestBaseStationData();

    // Start a thread to print the most recently received base station data every 1000 ms.
    int sleep_interval = 1000;
    std::thread print_data_thread(&PrintBaseStationData, sleep_interval);

    // Retrieve the base station data every 1000 ms and save the data packet in a data_queue.
    std::chrono::steady_clock::time_point start_time = std::chrono::steady_clock::now();
    uint32_t start_idx = 0;
    for (int i = 0; i < 60; i++)
    {
        // Retrieve the recent packets.
        ommo::api::BaseStationDataResponseUPtr result(client_context.GetBaseStationDataSinceIndex(base_station_data_request_tag, start_idx));

        // Save the packets.
        if (result->state != ommo::api::DataResponseState::kNoData)
        {
            std::unique_lock<std::mutex> lock(base_station_queue_mtx);
            for (int i = 0; i < result->packet_count; i++)
            {
                base_station_data_queue.push_back(result->packets[i]);
            }
            start_idx += result->packet_count;
            lock.unlock();
            base_station_packet_count += result->packet_count;
        }

        // Calculate and print the packet rate every ~5s.
        if (i % 5 == 0)
        {
            std::chrono::steady_clock::time_point end_time = std::chrono::steady_clock::now();
            uint32_t count = base_station_packet_count.exchange(0);
            auto duration_ms = std::chrono::duration_cast<std::chrono::milliseconds>(end_time - start_time).count();
            start_time = end_time;

            std::stringstream msg;
            msg << "[Main] BaseStation Packet rate: " << count * 1000.0f / duration_ms << std::endl;
            std::cout << msg.str();
        }

        // Sleep for 1000 ms
        std::this_thread::sleep_for(std::chrono::milliseconds(sleep_interval));
    }

    // Close the tracking device data request.
    std::cout << "[Main] Closing tracking data request.\n";
    client_context.CloseRequest(tracking_device_data_request_tag);

    // Close the base station data request.
    std::cout << "[Main] Closing base station data request.\n";
    client_context.CloseBaseStationDataRequest(base_station_data_request_tag);

    // Stop the data printing thread.
    std::cout << "[Main] Stopping data printing thread.\n";
    stop_printing_data = true;
    print_data_thread.join();

    // Shut down the client context.
    std::cout << "[Main] Shutting down the client context.\n";
    client_context.Shutdown();
    return 0;
}
