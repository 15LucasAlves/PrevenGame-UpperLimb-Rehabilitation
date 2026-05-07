/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#include "protobuf_converters.h"

namespace ommo
{
    /*
     * ------------------------------------------------------------------------
     * Convert from protobuf messages to ommo::api structs
     * ------------------------------------------------------------------------
     */

    api::Vector3i ProtoToVector3i(const ommo::Vector3i& vector)
    {
        api::Vector3i vec;
        vec.x = vector.x();
        vec.y = vector.y();
        vec.z = vector.z();
        return vec;
    }

    api::Vector3f ProtoToVector3f(const ommo::Vector3f& vector)
    {
        api::Vector3f vec;
        vec.x = vector.x();
        vec.y = vector.y();
        vec.z = vector.z();
        return vec;
    }

    api::Vector4f ProtoToVector4f(const ommo::Vector4f& vector)
    {
        api::Vector4f vec;
        vec.w = vector.w();
        vec.x = vector.x();
        vec.y = vector.y();
        vec.z = vector.z();
        return vec;
    }

    api::SensorUnitDescriptor ProtoToSensorUnitDescriptor(const ommo::SensorUnitDescriptor& descriptor)
    {
        api::SensorUnitDescriptor sensor_unit_descriptor;
        sensor_unit_descriptor.timestamp_offset = ommo::ProtoToVector3i(descriptor.timestamp_offset());
        sensor_unit_descriptor.mag_present = descriptor.mag_present();
        sensor_unit_descriptor.mag_scale = descriptor.mag_scale();
        sensor_unit_descriptor.imu_present = descriptor.imu_present();
        sensor_unit_descriptor.accel_scale = descriptor.accel_scale();
        sensor_unit_descriptor.gyro_scale = descriptor.gyro_scale();
        sensor_unit_descriptor.uuid = descriptor.uuid();
        return sensor_unit_descriptor;
    }

    api::DeviceDescriptorUPtr ProtoToDeviceDescriptor(const ommo::DeviceDescriptor& descriptor)
    {
        api::DeviceDescriptorUPtr device_descriptor(new api::DeviceDescriptor);
        device_descriptor->siu_uuid = descriptor.siu_uuid();
        device_descriptor->port_id = descriptor.port_id();
        device_descriptor->user_device_type = descriptor.user_device_type();
        device_descriptor->button_count = descriptor.button_count();
        device_descriptor->device_part_number = descriptor.device_part_number();
        device_descriptor->secure_device_info = descriptor.secure_device_info();

        device_descriptor->sensor_unit_descriptor_count = descriptor.sensor_unit_descriptors_size();
        device_descriptor->sensor_unit_descriptors = new api::SensorUnitDescriptor[device_descriptor->sensor_unit_descriptor_count];
        // Sensor Unit Descriptors
        for (int sensor_index = 0; sensor_index < descriptor.sensor_unit_descriptors_size(); sensor_index++)
        {
            device_descriptor->sensor_unit_descriptors[sensor_index] = ommo::ProtoToSensorUnitDescriptor(descriptor.sensor_unit_descriptors(sensor_index));
        }

        device_descriptor->supported_fusion_modes_count = descriptor.supported_fusion_modes_size();
        device_descriptor->supported_fusion_modes = new api::DeviceFusionMode[device_descriptor->supported_fusion_modes_count];
        // Supported Fusion Modes
        for (int fusion_index = 0; fusion_index < descriptor.supported_fusion_modes_size(); fusion_index++)
        {
            device_descriptor->supported_fusion_modes[fusion_index] = ommo::ProtoToDeviceFusionMode(descriptor.supported_fusion_modes(fusion_index));
        }

        return device_descriptor;
    }

    api::TrackingDeviceEventUPtr ProtoToTrackingDeviceEvent(const ommo::TrackingDeviceEvent& event)
    {
        api::TrackingDeviceEventUPtr tracking_device_event(new api::TrackingDeviceEvent);
        tracking_device_event->connected = event.connected();
        // Dereference to copy and release the pointer so tracking_device_event now owns the memory
        api::MoveUniquePtr(tracking_device_event->device, ProtoToDeviceDescriptor(event.device()));

        return tracking_device_event;
    }

    api::TrackingDevicesUPtr ProtoToTrackingDevices(const ommo::TrackingDevices& tr_devices)
    {
        api::TrackingDevicesUPtr tracking_devices(new api::TrackingDevices);

        tracking_devices->device_count = tr_devices.devices_size();
        tracking_devices->devices = new api::DeviceDescriptor[tracking_devices->device_count];
        for (int device = 0; device < tr_devices.devices_size(); device++)
        {
            // Dereference to copy and release the pointer so tracking_devices now owns the memory
            api::MoveUniquePtr(tracking_devices->devices[device], ProtoToDeviceDescriptor(tr_devices.devices(device)));
        }
        return tracking_devices;
    }

    api::HardwareStatus ProtoToHardwareStatus(const ommo::HardwareStatus& state)
    {
        return static_cast<api::HardwareStatus>(state);
    }

    api::CommonHardwareStateUPtr ProtoToCommonHardwareState(const ommo::CommonHardwareState& state)
    {
        api::CommonHardwareStateUPtr common_hardware_state(new api::CommonHardwareState);
        common_hardware_state->connected = state.connected();

        common_hardware_state->serial_number = new char[state.serial_number().length() + 1];
        std::strcpy(common_hardware_state->serial_number, state.serial_number().c_str());

        common_hardware_state->uuid = state.uuid();

        common_hardware_state->usb_port_name = new char[state.usb_port_name().length() + 1];
        std::strcpy(common_hardware_state->usb_port_name, state.usb_port_name().c_str());

        common_hardware_state->hardware_status = ommo::ProtoToHardwareStatus(state.hardware_status());
        return common_hardware_state;
    }

    api::DirectCommStatus ProtoToDirectCommStatus(const ommo::DirectCommStatus& status)
    {
        return static_cast<api::DirectCommStatus>(status);
    }

    api::SensorDeviceState ProtoToSensorDeviceState(const ommo::SensorDeviceState& state)
    {
        api::SensorDeviceState sensor_device_state;
        sensor_device_state.port_number = state.port_number();
        sensor_device_state.mag_sensor_count = state.mag_sensor_count();
        return sensor_device_state;
    }

    api::ReceiverConnection ProtoToRecevierConnection(const ommo::ReceiverConnection& connection)
    {
        api::ReceiverConnection receiver_connection;
        receiver_connection.uuid = connection.uuid();
        receiver_connection.time_slot = connection.time_slot();
        return receiver_connection;
    }

    api::BasestationHardwareStateUPtr ProtoToBasestationHardwareState(const ommo::BasestationHardwareState& bs_state)
    {
        api::BasestationHardwareStateUPtr basestation_hardware_state(new api::BasestationHardwareState);
        // Dereference to copy and release the pointer so basestation_hardware_state now owns the memory
        api::MoveUniquePtr(basestation_hardware_state->common_state, ProtoToCommonHardwareState(bs_state.common_state()));
        basestation_hardware_state->sync_channel = bs_state.sync_channel();
        basestation_hardware_state->direct_comm_status = ommo::ProtoToDirectCommStatus(bs_state.direct_comm_status());
        basestation_hardware_state->direct_comm_uuid = bs_state.direct_comm_uuid();
        basestation_hardware_state->motor_running = bs_state.motor_running();
        return basestation_hardware_state;
    }

    api::SIUHardwareStateUPtr ProtoToSIUHardwareState(const ommo::SIUHardwareState& siu_state)
    {
        api::SIUHardwareStateUPtr siu_hardware_state(new api::SIUHardwareState);
        // Dereference to copy and release the pointer so siu_hardware_state now owns the memory
        api::MoveUniquePtr(siu_hardware_state->common_state, ProtoToCommonHardwareState(siu_state.common_state()));
        siu_hardware_state->wireless = siu_state.wireless();
        siu_hardware_state->sync_channel = siu_state.sync_channel();
        siu_hardware_state->data_channel = siu_state.data_channel();

        // Sensor Device States
        siu_hardware_state->sensor_device_state_count = siu_state.sensor_device_states_size();
        siu_hardware_state->sensor_device_states = new api::SensorDeviceState[siu_hardware_state->sensor_device_state_count];
        for (int sensor_index = 0; sensor_index < siu_state.sensor_device_states_size(); sensor_index++)
        {
            siu_hardware_state->sensor_device_states[sensor_index] = ProtoToSensorDeviceState(siu_state.sensor_device_states(sensor_index));
        }

        return siu_hardware_state;
    }

    api::WirelessReceiverHardwareStateUPtr ProtoToWirelessReceiverHardwareState(const ommo::WirelessReceiverHardwareState& wr_state)
    {
        api::WirelessReceiverHardwareStateUPtr wireless_receiver_hardware_state(new api::WirelessReceiverHardwareState);
        // Dereference to copy and release the pointer so wireless_receiver_hardware_state now owns the memory
        api::MoveUniquePtr(wireless_receiver_hardware_state->common_state, ProtoToCommonHardwareState(wr_state.common_state()));
        wireless_receiver_hardware_state->data_channel = wr_state.data_channel();

        // Receiver Connections
        wireless_receiver_hardware_state->connected_siu_count = wr_state.connected_sius_size();
        wireless_receiver_hardware_state->connected_sius = new api::ReceiverConnection[wireless_receiver_hardware_state->connected_siu_count];
        for (int siu_index = 0; siu_index < wr_state.connected_sius_size(); siu_index++)
        {
            wireless_receiver_hardware_state->connected_sius[siu_index] = ProtoToRecevierConnection(wr_state.connected_sius(siu_index));
        }

        return wireless_receiver_hardware_state;
    }

    api::HardwareStatesUPtr ProtoToHardwareStates(const ommo::HardwareStates& hw_states)
    {
        api::HardwareStatesUPtr hardware_states(new api::HardwareStates);

        // Basestation States
        hardware_states->basestation_state_count = hw_states.basestation_states_size();
        hardware_states->basestation_states = new api::BasestationHardwareState[hardware_states->basestation_state_count];
        for (int bs_state = 0; bs_state < hw_states.basestation_states_size(); bs_state++)
        {
            // Dereference to copy and release the pointer so hardware_states now owns the memory
            api::MoveUniquePtr(hardware_states->basestation_states[bs_state], ProtoToBasestationHardwareState(hw_states.basestation_states(bs_state)));
        }

        // SIU States
        hardware_states->siu_state_count = hw_states.siu_states_size();
        hardware_states->siu_states = new api::SIUHardwareState[hardware_states->siu_state_count];
        for (int siu_state = 0; siu_state < hw_states.siu_states_size(); siu_state++)
        {
            // Dereference to copy and release the pointer so hardware_states now owns the memory
            api::MoveUniquePtr(hardware_states->siu_states[siu_state], ommo::ProtoToSIUHardwareState(hw_states.siu_states(siu_state)));
        }

        // Wireless Receiver States
        hardware_states->wireless_receiver_state_count = hw_states.wireless_receiver_states_size();
        hardware_states->wireless_receiver_states = new api::WirelessReceiverHardwareState[hardware_states->wireless_receiver_state_count];
        for (int wr_state = 0; wr_state < hw_states.wireless_receiver_states_size(); wr_state++)
        {
            // Dereference to copy and release the pointer so hardware_states now owns the memory
            api::MoveUniquePtr(hardware_states->wireless_receiver_states[wr_state], ommo::ProtoToWirelessReceiverHardwareState(hw_states.wireless_receiver_states(wr_state)));
        }

        return hardware_states;
    }

    api::RawSensorData ProtoToRawSensorData(const ommo::RawSensorData& data)
    {
        api::RawSensorData raw_sensor_data;
        raw_sensor_data.mag = ommo::ProtoToVector3i(data.mag());
        raw_sensor_data.gyro = ommo::ProtoToVector3i(data.gyro());
        raw_sensor_data.accel = ommo::ProtoToVector3i(data.accel());
        return raw_sensor_data;
    }


    api::BatteryState ProtoToBatteryInfo(const ommo::BatteryState& battery_state)
    {
        api::BatteryState b_state;
        b_state.state_of_charge = battery_state.state_of_charge();
        b_state.current = battery_state.current();
        b_state.remaining_capacity = battery_state.remaining_capacity();
        return b_state;
    }

    api::TrackingDeviceDataUPtr ProtoToTrackingDeviceData(const ommo::TrackingDeviceData& data)
    {
        api::TrackingDeviceDataUPtr tracking_device_data(new api::TrackingDeviceData);

        tracking_device_data->siu_uuid = data.siu_uuid();
        tracking_device_data->port_id = data.port_id();
        tracking_device_data->basestation_angle = data.basestation_angle();
        tracking_device_data->basestation_speed = data.basestation_speed();
        tracking_device_data->timestamp = data.timestamp();

        // Raw Sensor Data
        tracking_device_data->raw_sensor_data_count = data.raw_sensor_data_size();
        tracking_device_data->raw_sensor_data = new api::RawSensorData[tracking_device_data->raw_sensor_data_count];
        for (int raw_data = 0; raw_data < data.raw_sensor_data_size(); raw_data++)
        {
            tracking_device_data->raw_sensor_data[raw_data] = ProtoToRawSensorData(data.raw_sensor_data(raw_data));
        }

        // Battery States
        if (data.has_battery_state())
        {
            tracking_device_data->battery_state = ProtoToBatteryInfo(data.battery_state());
        }
        else
        {
            tracking_device_data->battery_state.state_of_charge = -1;
            tracking_device_data->battery_state.current = -1;
            tracking_device_data->battery_state.remaining_capacity = -1;
        }

        // Poses - use positions_size() since positions, quaternions, and indicator values always have the same size
        tracking_device_data->pose_count = data.positions_size();
        tracking_device_data->poses = new api::PoseData[tracking_device_data->pose_count];
        for (int pose_index = 0; pose_index < tracking_device_data->pose_count; pose_index++)
        {
            tracking_device_data->poses[pose_index].position = ProtoToVector3f(data.positions(pose_index));
            tracking_device_data->poses[pose_index].quaternion = ProtoToVector4f(data.quaternions(pose_index));
            tracking_device_data->poses[pose_index].indicator_value = data.indicator_values(pose_index);
            tracking_device_data->poses[pose_index].motion_indicator = pose_index < data.motion_indicators_size() ? data.motion_indicators(pose_index) : 0;
            tracking_device_data->poses[pose_index].bad_data_indicator = pose_index < data.bad_data_indicators_size() ? data.bad_data_indicators(pose_index) : 0;
        }

        // Buttons
        tracking_device_data->button_count = data.buttons_size();
        tracking_device_data->buttons = new api::ButtonState[tracking_device_data->button_count];
        for (int i = 0; i < tracking_device_data->button_count; i++)
        {
            tracking_device_data->buttons[i] = (api::ButtonState)data.buttons(i);
        }

        // Latency Timestamps
        tracking_device_data->latency_timestamp_count = data.latency_timestamps_size();
        tracking_device_data->latency_timestamps = new api::TimestampData[tracking_device_data->latency_timestamp_count];
        for (int i = 0; i < tracking_device_data->latency_timestamp_count; i++)
        {
            tracking_device_data->latency_timestamps[i].timestamp_type = (api::TimestampType)data.latency_timestamps(i).timestamp_type();
            tracking_device_data->latency_timestamps[i].steady_timestamp_milliseconds = data.latency_timestamps(i).steady_timestamp_milliseconds();
            tracking_device_data->latency_timestamps[i].system_timestamp_milliseconds = data.latency_timestamps(i).system_timestamp_milliseconds();
        }

        return tracking_device_data;
    }

    api::DataFrameUPtr ProtoToDataFrame(const ommo::DataFrame& frame)
    {
        api::DataFrameUPtr data_frame(new api::DataFrame);

        data_frame->device_data_count = frame.device_data_size();
        data_frame->device_data = new api::TrackingDeviceData[data_frame->device_data_count];
        for (int device_index = 0; device_index < frame.device_data_size(); device_index++)
        {
            // Dereference to copy and release the pointer so data_frame now owns the memory
            api::MoveUniquePtr(data_frame->device_data[device_index], ProtoToTrackingDeviceData(frame.device_data(device_index)));
        }
        return data_frame;
    }

    api::DeviceFusionMode ProtoToDeviceFusionMode(const ommo::DeviceFusionMode& fusion_mode)
    {
        return static_cast<api::DeviceFusionMode>(fusion_mode);
    }

    api::TrackingGroupUPtr ProtoToTrackingGroup(const ommo::TrackingGroup& group)
    {
        api::TrackingGroupUPtr tracking_group(new api::TrackingGroup);
        tracking_group->siu_uuid = group.siu_uuid();
        tracking_group->port_id = group.port_id();
        tracking_group->device_count = group.devices_size();
        tracking_group->devices = new api::DeviceDescriptor[tracking_group->device_count];
        for (int i = 0; i < group.devices_size(); i++)
        {
            api::MoveUniquePtr(tracking_group->devices[i], ProtoToDeviceDescriptor(group.devices(i)));
        }
        return tracking_group;
    }

    api::TrackingGroupEventUPtr ProtoToTrackingGroupEvent(const ommo::TrackingGroupEvent& event)
    {
        api::TrackingGroupEventUPtr tracking_group_event(new api::TrackingGroupEvent);
        tracking_group_event->connected = event.connected();
        api::MoveUniquePtr(tracking_group_event->tracking_group, ProtoToTrackingGroup(event.tracking_group()));
        return tracking_group_event;
    }

    api::SpecParamf ProtoToSpecParamf(const ommo::SpecParamf& param)
    {
        api::SpecParamf spec_param;
        spec_param.value = param.value();
        spec_param.out_of_spec = param.out_of_spec();
        return spec_param;
    }

    api::BaseStationData ProtoToBaseStationData(const ommo::BaseStationData& data)
    {
        api::BaseStationData basestation_data;
        basestation_data.accel_figure_of_merit = ProtoToSpecParamf(data.accel_figure_of_merit());
        basestation_data.max_phase_std = ProtoToSpecParamf(data.max_phase_std());
        basestation_data.max_phase_drift = ProtoToSpecParamf(data.max_phase_drift());
        basestation_data.temp_diff_from_calib_c = ProtoToSpecParamf(data.temp_diff_from_calib_c());
        basestation_data.mean_rotation_rate_hz = ProtoToSpecParamf(data.mean_rotation_rate_hz());
        basestation_data.tilt_angle_deg = ProtoToSpecParamf(data.tilt_angle_deg());
        basestation_data.max_mag_rms_res = ProtoToSpecParamf(data.max_mag_rms_res());
        basestation_data.accel_dc_magnitude_g = ProtoToSpecParamf(data.accel_dc_magnitude_g());
        return basestation_data;
    }

    api::WirelessManagementRequestType ProtoToWirelessManagementRequestType(const ommo::WirelessManagementRequestType& type)
    {
        return static_cast<api::WirelessManagementRequestType>(type);
    }

    api::WirelessManagementEventType ProtoToWirelessManagementEventType(const ommo::WirelessManagementEventType& type)
    {
        return static_cast<api::WirelessManagementEventType>(type);
    }

    api::WirelessManagementError ProtoToWirelessManagementError(const ommo::WirelessManagementError& error)
    {
        return static_cast<api::WirelessManagementError>(error);
    }

    api::WirelessManagementEventUPtr ProtoToWirelessManagementEvent(const ommo::WirelessManagementEvent& event)
    {
        api::WirelessManagementEventUPtr wireless_event(new api::WirelessManagementEvent);
        wireless_event->event_type = ProtoToWirelessManagementEventType(event.event_type());
        wireless_event->client_request_type = ProtoToWirelessManagementRequestType(event.client_request_type());
        wireless_event->siu_uuid_count = event.siu_uuids_size();
        wireless_event->siu_uuids = new uint32_t[wireless_event->siu_uuid_count];
        for (int i = 0; i < event.siu_uuids_size(); i++)
        {
            wireless_event->siu_uuids[i] = event.siu_uuids(i);
        }
        wireless_event->request_error = ProtoToWirelessManagementError(event.request_error());

        wireless_event->pairing_information.siu_uuid = event.pairing_information().siu_uuid();
        wireless_event->pairing_information.device_pairing_information_count = event.pairing_information().device_pairing_information_size();
        wireless_event->pairing_information.device_pairing_information = new api::DevicePairingInformation[wireless_event->pairing_information.device_pairing_information_count];
        for (int i = 0; i < event.pairing_information().device_pairing_information_size(); i++)
        {
            wireless_event->pairing_information.device_pairing_information[i].device_part_num_count = event.pairing_information().device_pairing_information(i).device_part_num_size();
            wireless_event->pairing_information.device_pairing_information[i].device_part_nums = new uint32_t[wireless_event->pairing_information.device_pairing_information[i].device_part_num_count];
            for (int j = 0; j < event.pairing_information().device_pairing_information(i).device_part_num_size(); j++)
            {
                wireless_event->pairing_information.device_pairing_information[i].device_part_nums[j] = event.pairing_information().device_pairing_information(i).device_part_num(j);
            }
        }
        return wireless_event;
    }

    api::DataLogState ProtoToDataLogState(const ommo::DataLogState& state)
    {
        switch (state)
        {
            case ommo::DataLogState::DATA_LOG_STATE_UNKNOWN:
                return api::DataLogState::kUnknown;
            case ommo::DataLogState::DATA_LOG_STATE_ENABLED:
                return api::DataLogState::kEnabled;
            case ommo::DataLogState::DATA_LOG_STATE_DISABLED:
                return api::DataLogState::kDisabled;
            case ommo::DataLogState::DATA_LOG_STATE_ERROR:
                return api::DataLogState::kError;
            
            default:
                return api::DataLogState::kUnknown;
        }
    }

    /*
     * ------------------------------------------------------------------------
     * Convert from ommo::api structs back to protobuf
     * ------------------------------------------------------------------------
     */

    ommo::DeviceFusionMode DeviceFusionModeToProto(const api::DeviceFusionMode fusion_mode)
    {
        return static_cast<ommo::DeviceFusionMode>(fusion_mode);
    }

    ommo::WirelessManagementRequestType ProtoToWirelessManagementRequestType(const api::WirelessManagementRequestType& type)
    {
        return static_cast<ommo::WirelessManagementRequestType>(type);
    }

    api::SelectReferenceDeviceResponse ProtoToSelectReferenceDeviceResponse(const ommo::SelectReferenceDeviceResponse& response)
    {
        api::SelectReferenceDeviceResponse api_response;
        api_response.success = response.success();
        return api_response;
    }

    ommo::SelectReferenceDeviceRequest SelectReferenceDeviceRequestToProto(const api::SelectReferenceDeviceRequest& request)
    {
        ommo::SelectReferenceDeviceRequest proto_request;
        proto_request.set_enabled(request.enabled);
        proto_request.set_siu_uuid(request.siu_uuid);
        proto_request.set_port_num(request.port_num);
        return proto_request;
    }

    api::ReferenceDeviceState ProtoToReferenceDeviceStateEvent(const ommo::ReferenceDeviceState& event)
    {
        api::ReferenceDeviceState ref_device_event;
        ref_device_event.is_enabled = event.is_enabled();
        ref_device_event.siu_uuid = event.siu_uuid();
        ref_device_event.port_num = event.port_num();
        return ref_device_event;
    }

}
