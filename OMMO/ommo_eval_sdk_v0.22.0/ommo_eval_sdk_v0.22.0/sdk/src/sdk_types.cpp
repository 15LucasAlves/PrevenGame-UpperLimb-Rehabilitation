/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#include "sdk_types.h"

#include <cstring>

namespace ommo::api {

    DataRequest* CreateDefaultDataRequest()
    {
        DataRequest* req = new DataRequest;
        req->data_field_mask = kSiuUuid | kPortId | kBasestationAngle | kBasestationSpeed | kTimestamp | kButtonStatus | kBatteryStatus;
        req->report_interval = 0;
        req->buffer_depth = 50;
        req->requested_fusion_mode = DeviceFusionMode::kDeviceFusionModeDefault;
        req->include_raw_sensor_data = false;
        req->requested_devices = nullptr;
        req->requested_device_count = 0;
        return req;
    }

    DeviceDescriptor* CopyDeviceDescriptor(const DeviceDescriptor& source)
    {
        DeviceDescriptor* new_des = new DeviceDescriptor;
        new_des->siu_uuid = source.siu_uuid;
        new_des->port_id = source.port_id;
        new_des->user_device_type = source.user_device_type;
        new_des->button_count = source.button_count;
        new_des->device_part_number = source.device_part_number;
        new_des->secure_device_info = source.secure_device_info;

        new_des->sensor_unit_descriptor_count = source.sensor_unit_descriptor_count;
        new_des->sensor_unit_descriptors = new SensorUnitDescriptor[new_des->sensor_unit_descriptor_count];
        for (int i = 0; i < new_des->sensor_unit_descriptor_count; i++)
        {
            // Performs an implicit copy given that SensorUnitDescriptor does not require a deep copy
            new_des->sensor_unit_descriptors[i] = source.sensor_unit_descriptors[i];
        }

        new_des->supported_fusion_modes_count = source.supported_fusion_modes_count;
        new_des->supported_fusion_modes = new DeviceFusionMode[new_des->supported_fusion_modes_count];
        for (int i = 0; i < new_des->supported_fusion_modes_count; i++)
        {
            // Performs an implicit copy given that DeviceFusionMode does not require a deep copy
            new_des->supported_fusion_modes[i] = source.supported_fusion_modes[i];
        }

        return new_des;
    }

    DevicePacket* CopyDevicePacket(const DevicePacket& source)
    {
        DevicePacket* new_packet = new DevicePacket;
        new_packet->packet_idx = source.packet_idx;
        MoveAndDeletePtr(new_packet->device_data, CopyTrackingDeviceData(source.device_data));
        return new_packet;
    }

    TrackingDeviceData* CopyTrackingDeviceData(const TrackingDeviceData& source)
    {
        TrackingDeviceData* new_data = new TrackingDeviceData;
        new_data->siu_uuid = source.siu_uuid;
        new_data->port_id = source.port_id;
        new_data->basestation_angle = source.basestation_angle;
        new_data->basestation_speed = source.basestation_speed;
        new_data->timestamp = source.timestamp;

        new_data->raw_sensor_data_count = source.raw_sensor_data_count;
        new_data->raw_sensor_data = new RawSensorData[new_data->raw_sensor_data_count];
        for (int i = 0; i < new_data->raw_sensor_data_count; i++)
        {
            new_data->raw_sensor_data[i] = source.raw_sensor_data[i];
        }

        new_data->battery_state = source.battery_state;

        new_data->pose_count = source.pose_count;
        new_data->poses = new PoseData[new_data->pose_count];
        for (int i = 0; i < new_data->pose_count; i++)
        {
            new_data->poses[i] = source.poses[i];
        }

        new_data->button_count = source.button_count;
        new_data->buttons = new ButtonState[new_data->button_count];
        for (int i = 0; i < new_data->button_count; i++)
        {
            new_data->buttons[i] = source.buttons[i];
        }

        new_data->latency_timestamp_count = source.latency_timestamp_count;
        new_data->latency_timestamps = new TimestampData[new_data->latency_timestamp_count];
        for (int i = 0; i < new_data->latency_timestamp_count; i++)
        {
            new_data->latency_timestamps[i] = source.latency_timestamps[i];
        }

        return new_data;
    }

    DataRequest* CopyDataRequest(const DataRequest& source)
    {
        DataRequest* new_req = new DataRequest;
        new_req->data_field_mask = source.data_field_mask;
        new_req->report_interval = source.report_interval;
        new_req->buffer_depth = source.buffer_depth;
        new_req->requested_fusion_mode = source.requested_fusion_mode;
        new_req->include_raw_sensor_data = source.include_raw_sensor_data;

        new_req->requested_device_count = source.requested_device_count;
        if (new_req->requested_device_count == 0)
        {
            new_req->requested_devices = nullptr;
        }
        else
        {
            new_req->requested_devices = new DeviceID[new_req->requested_device_count];
            for (int i = 0; i < new_req->requested_device_count; i++)
            {
                new_req->requested_devices[i] = source.requested_devices[i];
            }
        }
        return new_req;
    }

    TrackingGroup* CopyTrackingGroup(const TrackingGroup& source)
    {
        TrackingGroup* new_group = new TrackingGroup;
        new_group->siu_uuid = source.siu_uuid;
        new_group->port_id = source.port_id;
        new_group->device_count = source.device_count;
        new_group->devices = new DeviceDescriptor[new_group->device_count];
        for (uint32_t i = 0; i < new_group->device_count; i++)
        {
            MoveAndDeletePtr(new_group->devices[i], CopyDeviceDescriptor(source.devices[i]));
        }
        return new_group;
    }

    TrackingGroupEvent* CopyTrackingGroupEvent(const TrackingGroupEvent& source)
    {
        TrackingGroupEvent* new_event = new TrackingGroupEvent;
        new_event->connected = source.connected;
        MoveAndDeletePtr(new_event->tracking_group, CopyTrackingGroup(source.tracking_group));
        return new_event;
    }


    WirelessManagementEvent* CopyWirelessManagementEvent(const WirelessManagementEvent& source)
    {
        WirelessManagementEvent* new_event = new WirelessManagementEvent;
        new_event->event_type = source.event_type;
        new_event->siu_uuid_count = source.siu_uuid_count;
        new_event->siu_uuids = new uint32_t[new_event->siu_uuid_count];
        std::memcpy(new_event->siu_uuids, source.siu_uuids, new_event->siu_uuid_count * sizeof(uint32_t));

        new_event->pairing_information.siu_uuid = source.pairing_information.siu_uuid;
        new_event->pairing_information.device_pairing_information_count = source.pairing_information.device_pairing_information_count;
        new_event->pairing_information.device_pairing_information = new DevicePairingInformation[new_event->pairing_information.device_pairing_information_count];
        for (int i = 0; i < new_event->pairing_information.device_pairing_information_count; i++)
        {
            uint32_t device_part_num_count = source.pairing_information.device_pairing_information[i].device_part_num_count;
            new_event->pairing_information.device_pairing_information[i].device_part_num_count = device_part_num_count;
            new_event->pairing_information.device_pairing_information[i].device_part_nums = new uint32_t[device_part_num_count];
            std::memcpy(new_event->pairing_information.device_pairing_information[i].device_part_nums,
                        source.pairing_information.device_pairing_information[i].device_part_nums,
                        device_part_num_count * sizeof(uint32_t));
        }
        return new_event;
    }

    void DestroyDeviceDescriptorMembers(DeviceDescriptor& descriptor)
    {
        descriptor.sensor_unit_descriptor_count = 0;
        delete[] descriptor.sensor_unit_descriptors;
        descriptor.sensor_unit_descriptors = nullptr;

        descriptor.supported_fusion_modes_count = 0;
        delete[] descriptor.supported_fusion_modes;
        descriptor.supported_fusion_modes = nullptr;
    }

    void DestroyCommonHardwareStateMembers(CommonHardwareState& state)
    {
        delete[] state.serial_number;
        state.serial_number = nullptr;
        delete[] state.usb_port_name;
        state.usb_port_name = nullptr;
    }

    void DestroyBasestationHardwareStateMembers(BasestationHardwareState& state)
    {
        DestroyCommonHardwareStateMembers(state.common_state);
    }

    void DestroySIUHardwareStateMembers(SIUHardwareState& state)
    {
        DestroyCommonHardwareStateMembers(state.common_state);
        state.sensor_device_state_count = 0;
        delete[] state.sensor_device_states;
        state.sensor_device_states = nullptr;
    }

    void DestroyWirelessReceiverHardwareStateMembers(WirelessReceiverHardwareState& state)
    {
        DestroyCommonHardwareStateMembers(state.common_state);
        state.connected_siu_count = 0;
        delete[] state.connected_sius;
        state.connected_sius = nullptr;
    }

    void DestroyTrackingDeviceDataMembers(TrackingDeviceData& data)
    {
        data.raw_sensor_data_count = 0;
        delete[] data.raw_sensor_data;
        data.raw_sensor_data = nullptr;

        data.pose_count = 0;
        delete[] data.poses;
        data.poses = nullptr;

        data.button_count = 0;
        delete[] data.buttons;
        data.buttons = nullptr;

        data.latency_timestamp_count = 0;
        delete[] data.latency_timestamps;
        data.latency_timestamps = nullptr;
    }

    void DestroyDevicePacketMembers(DevicePacket& packet)
    {
        DestroyTrackingDeviceDataMembers(packet.device_data);
    }

    void DestroyTrackingGroupMembers(TrackingGroup& tracking_group)
    {
        tracking_group.device_count = 0;
        delete[] tracking_group.devices;
        tracking_group.devices = nullptr;
    }

    void DestroyTrackingGroupEventMembers(TrackingGroupEvent& event)
    {
        DestroyTrackingGroupMembers(event.tracking_group);
    }

    void DestroyWirelessManagementEventMembers(WirelessManagementEvent& event)
    {
        event.siu_uuid_count = 0;
        delete[] event.siu_uuids;

        for (int i = 0; i < event.pairing_information.device_pairing_information_count; i++)
        {
            delete[] event.pairing_information.device_pairing_information[i].device_part_nums;
            event.pairing_information.device_pairing_information[i].device_part_num_count = 0;
        }
        delete[] event.pairing_information.device_pairing_information;
        event.pairing_information.device_pairing_information_count = 0;
        event.pairing_information.siu_uuid = 0;

        event.siu_uuids = nullptr;
    }

    void DestroyDeviceDescriptor(DeviceDescriptor* descriptor)
    {
        if (descriptor == nullptr) return;

        DestroyDeviceDescriptorMembers(*descriptor);
        delete descriptor;
    }

    void DestroyTrackingDeviceEvent(TrackingDeviceEvent* event)
    {
        if (event == nullptr) return;

        // Since the struct member device is not dynamically allocated,
        // use the helper function to clean up dynamically allocated members
        DestroyDeviceDescriptorMembers(event->device);
        delete event;
    }

    void DestroyTrackingDevices(TrackingDevices* tracking_devices)
    {
        if (tracking_devices == nullptr) return;

        for (uint32_t i = 0; i < tracking_devices->device_count; i++)
        {
            // The array is allocated with new[] but individual elements need to clean up dynamically allocated members
            DestroyDeviceDescriptorMembers(tracking_devices->devices[i]);
        }
        delete[] tracking_devices->devices;
        delete tracking_devices;
    }

    void DestroyCommonHardwareState(CommonHardwareState* state)
    {
        if (state == nullptr) return;

        DestroyCommonHardwareStateMembers(*state);
        delete state;
    }

    void DestroyBasestationHardwareState(BasestationHardwareState* state)
    {
        if (state == nullptr) return;

        DestroyBasestationHardwareStateMembers(*state);
        delete state;
    }

    void DestroySIUHardwareState(SIUHardwareState* state)
    {
        if (state == nullptr) return;

        DestroySIUHardwareStateMembers(*state);
        delete state;
    }

    void DestroyWirelessReceiverHardwareState(WirelessReceiverHardwareState* state)
    {
        if (state == nullptr) return;

        DestroyWirelessReceiverHardwareStateMembers(*state);
        delete state;
    }

    void DestroyHardwareStates(HardwareStates* states)
    {
        if (states == nullptr) return;

        for (uint32_t i = 0; i < states->basestation_state_count; i++)
        {
            DestroyBasestationHardwareStateMembers(states->basestation_states[i]);
        }
        delete[] states->basestation_states;

        for (uint32_t i = 0; i < states->siu_state_count; i++)
        {
            DestroySIUHardwareStateMembers(states->siu_states[i]);
        }
        delete[] states->siu_states;

        for (uint32_t i = 0; i < states->wireless_receiver_state_count; i++)
        {
            DestroyWirelessReceiverHardwareStateMembers(states->wireless_receiver_states[i]);
        }
        delete[] states->wireless_receiver_states;

        delete states;
    }

    void DestroyTrackingDeviceData(TrackingDeviceData* data)
    {
        if (data == nullptr) return;

        DestroyTrackingDeviceDataMembers(*data);
        delete data;
    }

    void DestroyDataFrame(DataFrame* data_frame)
    {
        if (data_frame == nullptr) return;

        for (uint32_t i = 0; i < data_frame->device_data_count; i++)
        {
            DestroyTrackingDeviceDataMembers(data_frame->device_data[i]);
        }
        delete[] data_frame->device_data;

        delete data_frame;
    }

    void DestroyDevicePacket(DevicePacket* packet)
    {
        if (packet == nullptr) return;

        DestroyDevicePacketMembers(*packet);
        delete packet;
    }

    void DestroyDataResponse(DataResponse* response)
    {
        if (response == nullptr) return;

        for (uint32_t i = 0; i < response->packet_count; i++)
        {
            DestroyDevicePacketMembers(response->packets[i]);
        }
        delete[] response->packets;
        delete response;
    }

    void DestroyBaseStationDataResponse(BaseStationDataResponse* response)
    {
        if (response == nullptr) return;

        delete[] response->packets;
        delete response;
    }

    void DestroyDeviceIDList(DeviceIDList* list)
    {
        if (list == nullptr) return;

        delete[] list->devices;
        delete list;
    }

    void DestroyDataRequest(DataRequest* request)
    {
        if (request == nullptr) return;

        delete[] request->requested_devices;
        delete request;
    }

    void DestroyTrackingGroup(TrackingGroup* group)
    {
        if (group == nullptr) return;
        for (uint32_t i = 0; i < group->device_count; i++)
        {
            DestroyDeviceDescriptorMembers(group->devices[i]);
        }
        delete[] group->devices;
        delete group;
    }

    void DestroyTrackingGroupEvent(TrackingGroupEvent* event)
    {
        if (event == nullptr) return;
        DestroyTrackingGroupEventMembers(*event);
        delete event;
    }

    void DestroyWirelessManagementEvent(WirelessManagementEvent* event)
    {
        if (event == nullptr) return;
        delete[] event->siu_uuids;
        for (int i = 0; i < event->pairing_information.device_pairing_information_count; i++)
        {
            delete[] event->pairing_information.device_pairing_information[i].device_part_nums;
        }
        delete[] event->pairing_information.device_pairing_information;
        delete event;
    }

    const char* GetHardwareStatusName(HardwareStatus status)
    {
        switch (status)
        {
        case HardwareStatus::kHardwareStateUnknown:
            return "Unknown";
        case HardwareStatus::kHardwareStateIdle:
            return "Idle";
        case HardwareStatus::kHardwareStateSettingUp:
            return "SettingUp";
        case HardwareStatus::kHardwareStateWaitingOnCommand:
            return "WaitingOnCommand";
        case HardwareStatus::kHardwareStateRunning:
            return "Running";
        case HardwareStatus::kHardwareStateError:
            return "Error";
        default:
            return "Invalid";
        }
    }

    const char* GetDirectCommStatusName(DirectCommStatus status)
    {
        switch (status)
        {
        case DirectCommStatus::kDirectCommNone:
            return "None";
        case DirectCommStatus::kDirectCommIdle:
            return "Idle";
        case DirectCommStatus::kDirectCommConnected:
            return "Connected";
        case DirectCommStatus::kDirectCommDescriptorRequest:
            return "DescriptorRequest";
        case DirectCommStatus::kDirectCommChannelSearch:
            return "ChannelSearch";
        default:
            return "Invalid";
        }
    }

    const char* GetTimestampTypeName(TimestampType type)
    {
        switch (type)
        {
        case TimestampType::kTimestampTypeUnknown:
            return "Unknown";
        case TimestampType::kTimestampTypeSample:
            return "Sample";
        case TimestampType::kTimestampTypeServiceReceived:
            return "ServiceReceived";
        case TimestampType::kTimestampTypeServiceSent:
            return "ServiceSent";
        case TimestampType::kTimestampTypeSdkReceived:
            return "SdkReceived";
        default:
            return "Invalid";
        }
    }
    
}  // namespace ommo::api
