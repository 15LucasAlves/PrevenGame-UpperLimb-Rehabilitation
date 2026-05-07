/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
 */

#include <atomic>
#include <chrono>
#include <deque>
#include <filesystem>
#include <iomanip>
#include <iostream>
#include <mutex>
#include <sstream>
#include <string>
#include <thread>
#include <unordered_map>

// Required ommo::api classes
#include "client_context.h"
#include "sdk_utils.h"

// Flag to signal the printing thread to terminate
std::atomic_bool stop_printing_data = false;

// mutex for data print queue
std::mutex print_queue_mtx;
std::deque<ommo::api::DataResponseUPtr> print_queue;

std::string ToString(ommo::api::TimestampType timestamp_type)
{
    switch(timestamp_type)
    {
        case ommo::api::TimestampType::kTimestampTypeUnknown:
            return "Unknown";
        case ommo::api::TimestampType::kTimestampTypeSample:
            return "Sample";
        case ommo::api::TimestampType::kTimestampTypeServiceReceived:
            return "ServiceReceived";
        case ommo::api::TimestampType::kTimestampTypeServiceSent:
            return "ServiceSent";
        default:
            return "InvalidTimestampType";
    }
}

/*
 * This function prints out packets received in the print_queue and demonstrates
 * the data fields available in each packet received
 */
void PrintData(uint32_t sleep_interval)
{
    // Run until signaled to stop
    while (!stop_printing_data)
    {
        std::unique_lock<std::mutex> lock(print_queue_mtx);
        while (print_queue.size() > 0)
        {
            // Get the first data to print
            ommo::api::DataResponseUPtr result = std::move(print_queue.front());
            print_queue.pop_front();
            lock.unlock();

            if (result->state == ommo::api::DataResponseState::kNoData)
            {
                std::cout << "No data received.\n";
            }
            else
            {
                for (uint32_t i = 0; i < result->packet_count; i++)
                {
                    // Print out the information for each packet
                    auto& packet = result->packets[i].device_data;
                    // use stringstream to ensure thread safety during print
                    std::stringstream output_ss;
                    output_ss << "idx=" << std::setw(6) << result->packets[i].packet_idx
                        << "  siu=" << packet.siu_uuid
                        << "  port=" << packet.port_id
                        << "  timestamp=" << std::setw(11) << packet.timestamp
                        << "";

                    // Print out the pose information for each packet
                    for (int j = 0; j < packet.pose_count; j++)
                    {
                        output_ss << "\tposition-" << std::to_string(j)
                            << " ("
                            << packet.poses[j].position.x << ","
                            << packet.poses[j].position.y << ","
                            << packet.poses[j].position.z
                            << ")";

                        output_ss << "\tquaternion-" << std::to_string(j)
                            << " ("
                            << packet.poses[j].quaternion.w << ","
                            << packet.poses[j].quaternion.x << ","
                            << packet.poses[j].quaternion.y << ","
                            << packet.poses[j].quaternion.z
                            << ")";

                        output_ss << "\tindicators-" << std::to_string(j)
                            << " ("
                            << packet.poses[j].indicator_value << ","
                            << packet.poses[j].motion_indicator << ","
                            << packet.poses[j].bad_data_indicator
                            << ")";
                    }

                    // Print button information
                    for (int j = 0; j < packet.button_count; j++)
                    {
                        output_ss << "\tbutton-" << std::to_string(j)
                            << "="
                            << packet.buttons[j];
                    }

                    std::chrono::system_clock::time_point now = std::chrono::system_clock::now();
                    uint64_t sample_time = 0;
                    uint64_t sent_time = 0;
                    for (int j = 0; j < packet.latency_timestamp_count; j++)
                    {
                        if (packet.latency_timestamps[j].timestamp_type == ommo::api::TimestampType::kTimestampTypeSample)
                        {
                            sample_time = packet.latency_timestamps[j].system_timestamp_milliseconds;
                        }
                        else if (packet.latency_timestamps[j].timestamp_type == ommo::api::TimestampType::kTimestampTypeServiceSent)
                        {
                            sent_time = packet.latency_timestamps[j].system_timestamp_milliseconds;
                        }
                    }
                    if (sample_time != 0 && sent_time != 0)
                    {
                        output_ss << "\tsample_to_send_ms: " << sent_time - sample_time;
                        output_ss << "\tsample_to_processing_ms: " << std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()).count() - sample_time;

                         char sample_system_time[30]; // 29 characters + 1 null terminator
                         if (ommo::api::SystemTimeToString(sample_time, sample_system_time, sizeof(sample_system_time)))
                         {
                             output_ss << "\tsystem_sample_time: " << sample_system_time;
                         }
                         else
                         {
                             output_ss << "\tsystem_sample_time: Invalid timestamp";
                         }
                    }

                    output_ss << std::endl;
                    std::cout << output_ss.str();

                }
            }
            // lock before the next loop
            lock.lock();
        }
        lock.unlock();
        // Sleep for the specified interval to wait for more data
        std::this_thread::sleep_for(std::chrono::milliseconds(sleep_interval));
    }
}

void ChannelStateHandler(int channel_state)
{
    // Handle grpc channel connection state changes
    // In this example, we simply print the new state
    std::cout << "Service Channel Connection State: " << channel_state << std::endl;
}


void DeviceEventHandler(const ommo::api::TrackingDeviceEvent& device_event)
{
    // Handle any device events
    // In this example, we simply print the event information
    if (device_event.connected)
    {
        std::cout << "[INFO][DeviceEventHandler] device connected. siu: "
            << device_event.device.siu_uuid << "  port id: "
            << device_event.device.port_id << std::endl;
    }
    else
    {
        std::cout << "[INFO][DeviceEventHandler] device disconnected. siu: "
            << device_event.device.siu_uuid << "  port id: "
            << device_event.device.port_id << std::endl;
    }
}

std::atomic_uint32_t data_packet_count = 0;
void TrackingDeviceDataHandler(const ommo::api::TrackingDeviceData& device_data)
{
    // Handle device data
    // In this example, we just count how many packets we've received since we are demonstrating polling for data

    // Generally, you'd only want to use the callback for data if you must handle every packet as soon as they are available
    // If the callback is used, processing the data should be done as quickly as possible
    // This is due to the callback being a blocking call and will block additional data from being received while in the callback
    // If the receiving buffer is full while in the callback, data packets in transit will be lost!
    // As such, you should NOT be doing anything I/O intensive such as printing the data to std::out.
    data_packet_count++;
}


std::string ToString(ommo::api::HardwareStatus hardware_status)
{
    switch (hardware_status)
    {
        case ommo::api::HardwareStatus::kHardwareStateUnknown:
            return "Unknown";
        case ommo::api::HardwareStatus::kHardwareStateIdle:
            return "Idle";
        case ommo::api::HardwareStatus::kHardwareStateSettingUp:
            return "SettingUp";
        case ommo::api::HardwareStatus::kHardwareStateWaitingOnCommand:
            return "WaitingOnCommand";
        case ommo::api::HardwareStatus::kHardwareStateRunning:
            return "Running";
        case ommo::api::HardwareStatus::kHardwareStateError:
            return "Error";
        default:
            return "InvalidStatus";
    }
}

std::string ToString(ommo::api::DirectCommStatus direct_comm_status)
{
    switch (direct_comm_status)
    {
        case ommo::api::DirectCommStatus::kDirectCommNone:
            return "None";
        case ommo::api::DirectCommStatus::kDirectCommIdle:
            return "Idle";
        case ommo::api::DirectCommStatus::kDirectCommConnected:
            return "Connected";
        case ommo::api::DirectCommStatus::kDirectCommDescriptorRequest:
            return "DescriptorRequest";
        case ommo::api::DirectCommStatus::kDirectCommChannelSearch:
            return "ChannelSearch";
        default:
            return "InvalidStatus";
    }
}

std::string ToString(ommo::api::DeviceFusionMode fusion_mode)
{
    switch (fusion_mode)
    {
        case ommo::api::DeviceFusionMode::kDeviceFusionModeDefault:
            return "Default";
        case ommo::api::DeviceFusionMode::kDeviceFusionModeNoFusion:
            return "NoFusion";
        case ommo::api::DeviceFusionMode::kDeviceFusionModeMagOnlyFusion:
            return "MagOnlyFusion";
        case ommo::api::DeviceFusionMode::kDeviceFusionModeIMUOnlyFusion:
            return "IMUOnlyFusion";
        case ommo::api::DeviceFusionMode::kDeviceFusionModeFullFusion:
            return "FullFusion";
        default:
            return "InvalidFusionMode";
    }
}

std::ostream& operator<<(std::ostream& os, const ommo::api::HardwareStatus& status)
{
    os << ToString(status);
    return os;
}

std::ostream& operator<<(std::ostream& os, const ommo::api::DirectCommStatus& status)
{
    os << ToString(status);
    return os;
}

std::ostream& operator<<(std::ostream& os, const ommo::api::DeviceFusionMode& fusion_mode)
{
    os << ToString(fusion_mode);
    return os;
}

std::string ToString(ommo::api::DataLogState logging_state)
{
    switch (logging_state)
    {
        case ommo::api::DataLogState::kUnknown:
            return "Unknown";
        case ommo::api::DataLogState::kEnabled:
            return "Enabled";
        case ommo::api::DataLogState::kDisabled:
            return "Disabled";
        case ommo::api::DataLogState::kError:
            return "Error";
        case ommo::api::DataLogState::kRpcFail:
            return "RPCFail";
        default:
            return "InvalidState";
    }
}


// Print all available information for a single tracking device.
void PrintTrackingDeviceInformation(ommo::api::DeviceDescriptor &tracking_device)
{
    std::cout << "\tSIU UUID: " << std::left << std::setw(12) << tracking_device.siu_uuid
              << "  Port ID: " << tracking_device.port_id
              << "  Device Part Number: " << tracking_device.device_part_number
              << "  User Device Type: " << tracking_device.user_device_type
              << "  Button Count: " << tracking_device.button_count
              << "  Device Security Info: " << (tracking_device.secure_device_info ? "Secure" : "Insecure")
              << std::endl;

    std::cout << "\t\tSupported Fusion Mode(s):";
    for (int i = 0; i < tracking_device.supported_fusion_modes_count; i++)
    {
        std::cout << "  " << tracking_device.supported_fusion_modes[i];
    }
    std::cout << std::endl;

    for (int i = 0; i < tracking_device.sensor_unit_descriptor_count; i++)
    {
        ommo::api::SensorUnitDescriptor &descriptor = tracking_device.sensor_unit_descriptors[i];
        std::cout << "\t\tUUID: " << descriptor.uuid
                  << "  Mag Present: " << (descriptor.mag_present ? "YES" : "NO")
                  << "  IMU Preset: " << (descriptor.imu_present ? "YES" : "NO")
                  << "\n\t\tMag Scale: " << descriptor.mag_scale
                  << "  Accel Scale: " << descriptor.accel_scale
                  << "  Gyro Scale: " << descriptor.gyro_scale
                  << "\n\t\tTimestamp Offset: (" << descriptor.timestamp_offset.x 
                  << ", " << descriptor.timestamp_offset.y 
                  << ", " << descriptor.timestamp_offset.z << ")"
                  << std::endl;
    }
}

// Print all available hardware state information for a base station.
void PrintBaseStationState(ommo::api::BasestationHardwareState &base_station_state)
{
    std::cout << "\tUUID: " << std::setw(12) << base_station_state.common_state.uuid
              << "  Serial Number: " << std::setw(13) << base_station_state.common_state.serial_number
              << "  Connected: " << std::setw(5) << (base_station_state.common_state.connected? "YES" : "NO")
              << "  Status: " << base_station_state.common_state.hardware_status
              << "\n\tSync Channel: " << std::setw(4) << base_station_state.sync_channel
              << "  Direct Comm UUID: " << std::setw(10) << std::left << base_station_state.direct_comm_uuid
              << "  USB Port: " << std::setw(6) <<  base_station_state.common_state.usb_port_name
              << "  Direct Comm Status: " << base_station_state.direct_comm_status
              << std::endl;
}

// Print all available hardware state information for an SIU.
void PrintSIUState(ommo::api::SIUHardwareState &siu_state)
{
    std::cout << "\tUUID: " << std::setw(12) << siu_state.common_state.uuid
              << "  Serial Number: " << std::setw(13) << siu_state.common_state.serial_number
              << "  Connected: " << std::setw(5) << (siu_state.common_state.connected? "YES" : "NO")
              << "  Status: " << siu_state.common_state.hardware_status
              << "\n\tSync Channel: " << std::setw(4) << siu_state.sync_channel
              << "  Data Channel: " << std::setw(14) << std::left << siu_state.data_channel
              << "  Wireless: " << std::setw(6) << (siu_state.wireless? "YES" : "NO")
              << "  USB Port: " <<  siu_state.common_state.usb_port_name
              << std::endl;

    for (int j = 0; j < siu_state.sensor_device_state_count; j++)
    {
        std::cout << "\t\tPort Number: " << siu_state.sensor_device_states[j].port_number
                  << "  Magnetometer Count: " << siu_state.sensor_device_states[j].mag_sensor_count
                  << std::endl;
    }
}

// Print all available hardware state information for a wireless receiver.
void PrintWirelessReceiverState(ommo::api::WirelessReceiverHardwareState &wireless_receiver_state)
{
    std::cout << "\tUUID: " << std::setw(12) << wireless_receiver_state.common_state.uuid
              << "  Serial Number: " << std::setw(13) << wireless_receiver_state.common_state.serial_number
              << "  Connected: " << std::setw(5) << (wireless_receiver_state.common_state.connected? "YES" : "NO")
              << "  Status: " << wireless_receiver_state.common_state.hardware_status
              << "\n\tData Channel: " << std::setw(4) << wireless_receiver_state.data_channel
              << "  USB Port: " <<  wireless_receiver_state.common_state.usb_port_name
              << std::endl;

    for (int j = 0; j < wireless_receiver_state.connected_siu_count; j++)
    {
        std::cout << "\t\tConnected UUID: " << wireless_receiver_state.connected_sius[j].uuid
                  << " Time Slot: " << wireless_receiver_state.connected_sius[j].time_slot
                  << std::endl;
    }
}

/*
 * Main
 *
 * This example shows the basic flow of setting up the ommo client context to establish
 * a connection to and receive data from ommo service.
 *
 * Steps:
 * --------------------------------------------------------------------
 * 1. Create an ommo::api::ClientContext object
 * 2. Start the client context
 * 3. [OPTIONAL] register a callback handler for connection state changes to service
 * 4. [OPTIONAL] register a callback handler for device events
 * 5. Send a default data request (by default, all devices both currently connected and in the future will be automatically included)
 * 6. [OPTIONAL] register a callback handler for any data received related to the data request
 * 7. [OPTIONAL] Request ommo service to start raw data logging
 * 8. Start a thread to handle printing the incoming data
 * 9. Poll all available data at set interval, each time obtaining new data since our last poll
 * 10. Close the request when data is no longer needed
 * 11. Request ommo service to stop raw data logging
 * 12. Request and print tracking device information
 * 13. Request and print hardware state information
 * 14. Shutdown the client context
 * 15. Stop the data output thread
 */
int main(int argc, char* argv[])
{
    // Create a Client Context. This will establish the connection to ommo service.
    // localhost:50051 is the default gRPC address for ommo service.
    ommo::api::ClientContext client_context("localhost:50051");

    // Start the client context. This will start monitoring device events.
    client_context.Start();

    // Set a callback function to handle channel connection state changes
    // This is NOT necessary as ClientContext will automatically manage connection to the service.
    // But you may want to handle channel state changes for other purposes.
    client_context.RegisterChannelStateCallback(ChannelStateHandler);

    // Set a callback function to handle device events
    // This is NOT necessary as ClientContext will automatically handle device events for you for any data requests.
    // But you may want to handle device events for other purposes.
    client_context.RegisterDeviceEventCallback(DeviceEventHandler);

    // Create a default data request. All devices are included in default requests.
    std::cout << "[Main] Requesting data for all devices.\n";
    ommo::api::DataRequestUPtr req_ptr(ommo::api::CreateDefaultDataRequest());
    uint32_t request_tag = client_context.RequestDeviceData(*req_ptr);

    // Set a callback function to handle TrackingDeviceData
    // This is NOT necessary as ClientContext will automatically handle data packets for you for any data requests.
    // See comments in TrackingDeviceDataHandler on possible usage of the callback
    client_context.RegisterTrackingDeviceDataCallback(request_tag, TrackingDeviceDataHandler);

    // Request that service saves the raw data in an HDF5 file in the current directory
    ommo::api::DataLogState logging_state = client_context.EnableDataLogging(std::filesystem::current_path().generic_string().c_str(), "raw_data.hdf5", true);
    std::cout << "[Main] Enable raw data logging. Logging State: " << ToString(logging_state) << std::endl;

    // Start a thread to handle printing so we don't slow down our data polling in the main thread
    int sleep_interval = 50;
    std::thread print_data_thread(&PrintData, sleep_interval);

    // Create a map to store the packet index we should request from for each device
    // This is so we can retrieve all packets since the last received index
    std::unordered_map<uint64_t, uint32_t> packet_start_index;

    // Poll for data every 50ms, 600 times (~30s)
    std::chrono::steady_clock::time_point start_time = std::chrono::steady_clock::now();

    for (int i = 0; i < 600; i++)
    {
        /*
         * Get the list of devices contained within the request associated with the provided tag.
         * AvailableDeviceList will always contain all currently connected devices that were in the request.
         * We can use this to check which devices have data.
         * Use custom UPtr to automatically handle the memory of the pointer returned by the SDK
         */
        ommo::api::DeviceIDListUPtr device_list(client_context.GetAvailableDeviceList(request_tag));

        // Go through all available devices and request all data since we last checked
        for (int device_index = 0; device_index < device_list->device_count; device_index++)
        {
            uint64_t device_hash = ommo::api::Hash(device_list->devices[device_index]);
            // Use device hash to retrieve the last received packet index
            if (packet_start_index.find(device_hash) == packet_start_index.end())
            {
                // Start at 0 if we have not received anything from this device yet
                packet_start_index[device_hash] = 0;
            }

            // Request only the data that has been received since the last check for this specific device
            // This ensures that only new data is returned
            // Use custom UPtr to automatically handle the memory of the pointer returned by the SDK
            ommo::api::DataResponseUPtr result(client_context.GetDataSinceIndex(request_tag, device_list->devices[device_index], packet_start_index[device_hash]));

            // Update the index for next request based on what we just received
            if (result->state != ommo::api::DataResponseState::kNoData)
            {
                packet_start_index[device_hash] = result->packets[result->packet_count - 1].packet_idx + 1;
            }

            // Move the received data to the print queue to be printed
            // We don't print here in case printing takes a long time due to a lot of data and slows down our loop
            std::unique_lock<std::mutex> lock(print_queue_mtx);
            print_queue.push_back(std::move(result));
        }

        // Print some information every ~1s
        if (i % 20 == 0)
        {
            std::chrono::steady_clock::time_point end_time = std::chrono::steady_clock::now();
            uint32_t count = data_packet_count.exchange(0);
            auto duration_ms = std::chrono::duration_cast<std::chrono::milliseconds>(end_time - start_time).count();
            start_time = end_time;

            std::stringstream msg;
            msg << "[Main] Number of devices: " << device_list->device_count << std::endl;
            msg << "[Main] Packet rate: " << count * 1000.0f / duration_ms << std::endl;
            // Use stringstream for thread safety during print
            std::cout << msg.str();
        }

        // Sleep for 50ms
        std::this_thread::sleep_for(std::chrono::milliseconds(sleep_interval));
    }

    // Close the data request by returning the tag provided by ClientContext::RequestDeviceData()
    std::cout << "[Main] Closing data request.\n";
    client_context.CloseRequest(request_tag);

    // Request that service stops raw data logging
    logging_state = client_context.DisableDataLogging();
    std::cout << "[Main] Disable raw data logging. Logging State: " << ToString(logging_state) << std::endl;

    // Request tracking device information
    std::cout << "\n[Main] Request tracking device information." << std::endl;
    ommo::api::TrackingDevicesUPtr devices_ptr(client_context.GetTrackingDevices());

    // Print the retrieved information.
    std::cout << "[Tracking Devices Information]" << std::endl;
    for (int i = 0; i < devices_ptr->device_count; i++)
    {
        PrintTrackingDeviceInformation(devices_ptr->devices[i]);
        std::cout << std::endl;
    }

    // Request the states of all hardware.
    std::cout << "\n[Main] Request the states of all hardware." << std::endl;
    ommo::api::HardwareStatesUPtr hardware_states_ptr(client_context.GetHardwareStates());

    // Print the base station states.
    std::cout << "[Base Station States]" << std::endl;
    for (int i = 0; i < hardware_states_ptr->basestation_state_count; i++)
    {
        PrintBaseStationState(hardware_states_ptr->basestation_states[i]);
        std::cout << std::endl;
    }

    // Print the SIU states.
    std::cout << "[SIU States]" << std::endl;
    for (int i = 0; i < hardware_states_ptr->siu_state_count; i++)
    {
        PrintSIUState(hardware_states_ptr->siu_states[i]);
        std::cout << std::endl;
    }

    // Print the wireless receiver states.
    std::cout << "[Wireless Receiver States]" << std::endl;
    for (int i = 0; i < hardware_states_ptr->wireless_receiver_state_count; i++)
    {
        PrintWirelessReceiverState(hardware_states_ptr->wireless_receiver_states[i]);
        std::cout << std::endl;
    }

    // Shut down the client context.
    std::cout << "[Main] Shutting down the client context.\n";
    client_context.Shutdown();

    // Stop the data printing thread.
    std::cout << "[Main] Stopping data printing thread.\n";
    stop_printing_data = true;
    print_data_thread.join();

    return 0;
}
