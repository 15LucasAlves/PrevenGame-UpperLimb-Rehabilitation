/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#include "sdk_types.h"
#include "ommo_service_api.pb.h"

namespace ommo
{
    /*
     * ------------------------------------------------------------------------
     * Protobuf to ommo::api struct converters
     * ------------------------------------------------------------------------
     * 
     * !!!IMPORTANT NOTE!!!
     * Converter functions that return the struct by pointer will allocate memory dynamically
     * Allocation failure is currently NOT handled and will throw bad_alloc exception!
     */

    // Convert from ommo::Vector3i protobuf to ommo::api::Vector3i struct
    api::Vector3i ProtoToVector3i(const ommo::Vector3i& vector);

    // Convert from ommo::Vector3f protobuf to ommo::api::Vector3f struct
    api::Vector3f ProtoToVector3f(const ommo::Vector3f& vector);

    // Convert from ommo::Vector4f protobuf to ommo::api::Vector4f struct
    api::Vector4f ProtoToVector4f(const ommo::Vector4f& vector);

    // Convert from ommo::SensorUnitDescriptor protobuf to ommo::api::SensorUnitDescriptor struct
    api::SensorUnitDescriptor ProtoToSensorUnitDescriptor(const ommo::SensorUnitDescriptor& descriptor);

    // Convert from ommo::DeviceDescriptor protobuf to ommo::api::DeviceDescriptor struct
    api::DeviceDescriptorUPtr ProtoToDeviceDescriptor(const ommo::DeviceDescriptor& descriptor);

    // Convert from ommo::TrackingDeviceEvent protobuf to ommo::api::TrackingDeviceEvent struct
    api::TrackingDeviceEventUPtr ProtoToTrackingDeviceEvent(const ommo::TrackingDeviceEvent& event);

    // Convert from ommo::TrackingDevices protobuf to ommo::api::TrackingDevices struct
    api::TrackingDevicesUPtr ProtoToTrackingDevices(const ommo::TrackingDevices& tr_devices);

    // Convert from ommo::CommonHardwareState protobuf to ommo::api::CommonHardwareState struct
    api::CommonHardwareStateUPtr ProtoToCommonHardwareState(const ommo::CommonHardwareState& ch_state);

    // Convert from ommo::DirectCommStatus protobuf enum to ommo::api::DirectCommStatus struct
    api::DirectCommStatus ProtoToDirectCommStatus(const ommo::DirectCommStatus& status);

    // Convert from ommo::SensorDeviceState protobuf to ommo::api::SensorDeviceState struct
    api::SensorDeviceState ProtoToSensorDeviceState(const ommo::SensorDeviceState& state);

    // Convert from ommo::ReceiverConnection protobuf to ommo::api::ReceiverConnection struct
    api::ReceiverConnection ProtoToRecevierConnection(const ommo::ReceiverConnection& connection);

    // Convert from ommo::BasestationHardwareState protobuf to ommo::api::BasestationHardwareState struct
    api::BasestationHardwareStateUPtr ProtoToBasestationHardwareState(const ommo::BasestationHardwareState& bs_state);

    // Convert from ommo::SIUHardwareState protobuf to ommo::api::SIUHardwareState struct
    api::SIUHardwareStateUPtr ProtoToSIUHardwareState(const ommo::SIUHardwareState& siu_state);

    // Convert from ommo::WirelessReceiverHardwareState protobuf to ommo::api::WirelessReceiverHardwareState struct
    api::WirelessReceiverHardwareStateUPtr ProtoToWirelessReceiverHardwareState(const ommo::WirelessReceiverHardwareState& receiver_state);

    // Convert from ommo::HardwareStates protobuf to ommo::api::HardwareStates struct
    api::HardwareStatesUPtr ProtoToHardwareStates(const ommo::HardwareStates& states);

    // Convert from ommo::RawSensorData protobuf to ommo::api::RawSensorData struct
    api::RawSensorData ProtoToRawSensorData(const ommo::RawSensorData& data);

    // Convert from ommo::BatteryState protobuf to ommo::api::BatteryState struct
    api::BatteryState ProtoToBatteryInfo(const ommo::BatteryState& battery_state);

    // Convert from ommo::TrackingDeviceData protobuf to ommo::api::TrackingDeviceData struct
    api::TrackingDeviceDataUPtr ProtoToTrackingDeviceData(const ommo::TrackingDeviceData& data);

    // Convert from ommo::DataFrame protobuf to ommo::api::DataFrame struct
    api::DataFrameUPtr ProtoToDataFrame(const ommo::DataFrame& frame);

    // Convert from ommo::DeviceFusionMode protobuf enum to ommo::api::DeviceFusionMode enum
    api::DeviceFusionMode ProtoToDeviceFusionMode(const ommo::DeviceFusionMode& fusion_mode);

    // Convert from ommo::HardwareStatus protobuf enum to ommo::api::HardwareStatus enum
    api::HardwareStatus ProtoToHardwareStatus(const ommo::HardwareStatus& state);

    // Convert from ommo::TrackingGroup protobuf to ommo::api::TrackingGroup struct
    api::TrackingGroupUPtr ProtoToTrackingGroup(const ommo::TrackingGroup& group);

    // Convert from ommo::TrackingGroupEvent protobuf to ommo::api::TrackingGroupEvent struct
    api::TrackingGroupEventUPtr ProtoToTrackingGroupEvent(const ommo::TrackingGroupEvent& event);

    // Convert from ommo::SpecParamf protobuf to ommo::api::SpecParamf struct
    api::SpecParamf ProtoToSpecParamf(const ommo::SpecParamf& param);

    // Convert from ommo::BaseStationData protobuf to ommo::api::BaseStationData struct
    api::BaseStationData ProtoToBaseStationData(const ommo::BaseStationData& data);

    // Convert from ommo::DeviceFusionMode protobuf enum to ommo::api::DeviceFusionMode enum
    api::WirelessManagementRequestType ProtoToWirelessManagementRequestType(const ommo::WirelessManagementRequestType& type);

    // Convert from ommo::WirelessManagementEventType protobuf enum to ommo::api::WirelessManagementEventType enum
    api::WirelessManagementEventType ProtoToWirelessManagementEventType(const ommo::WirelessManagementEventType& type);

    // Convert from ommo::WirelessManagementError protobuf enum to ommo::api::WirelessManagementError enum
    api::WirelessManagementError ProtoToWirelessManagementError(const ommo::WirelessManagementError& error);

    // Convert from ommo::WirelessManagementEvent protobuf to ommo::api::WirelessManagementEvent struct
    api::WirelessManagementEventUPtr ProtoToWirelessManagementEvent(const ommo::WirelessManagementEvent& event);

    // Convert from ommo::DataLogState protobuf enum to ommo::api::DataLogState enum
    api::DataLogState ProtoToDataLogState(const ommo::DataLogState& state);

    // Convert from ommo::SelectReferenceDeviceResponse protobuf to ommo::api::SelectReferenceDeviceResponse struct
    api::SelectReferenceDeviceResponse ProtoToSelectReferenceDeviceResponse(const ommo::SelectReferenceDeviceResponse& response);

    // Convert from ommo::ReferenceDeviceState protobuf to ommo::api::ReferenceDeviceState struct
    api::ReferenceDeviceState ProtoToReferenceDeviceStateEvent(const ommo::ReferenceDeviceState& event);

    /*
     * ------------------------------------------------------------------------
     * ommo::api struct to protobuf converters
     * ------------------------------------------------------------------------
     */

    // Convert from ommo::api::DeviceFusionMode enum to ommo::DeviceFusionMode protobuf enum
    ommo::DeviceFusionMode DeviceFusionModeToProto(const api::DeviceFusionMode fusion_mode);

    // Convert from ommo::api::DeviceFusionMode enum to ommo::DeviceFusionMode protobuf enum
    ommo::WirelessManagementRequestType ProtoToWirelessManagementRequestType(const api::WirelessManagementRequestType& type);

    // Convert from ommo::api::SelectReferenceDeviceRequest struct to ommo::SelectReferenceDeviceRequest protobuf
    ommo::SelectReferenceDeviceRequest SelectReferenceDeviceRequestToProto(const api::SelectReferenceDeviceRequest& request);
}
