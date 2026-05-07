/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#include "protobuf_converters.h"
#include "rpc_wireless_management_stream_client_bidi_reactor.h"
#include "sdk_types.h"

namespace ommo::api
{
    class WirelessManager::impl
    {
        public:
            explicit impl() {}

            void CancelStream();

            bool IsStreamActive() const;

            void SetClientReactor(std::shared_ptr<RpcWirelessManagementStreamClientBidiReactor> client_reactor);

            void RegisterWirelessEventCallback(std::function<void(WirelessManagementEvent*)> callback_function);

            void ResetWirelessEventCallback();

            void EnablePairingMode();

            void DisablePairingMode();

            void GetPairingApprovedList();

            void ApprovePairing(uint32_t uuid);

            void DenyPairing(uint32_t uuid);

            void Unpair(uint32_t uuid);

            void GetPairingBlockedList();

            void BlockPairing(uint32_t uuid);

            void UnblockPairing(uint32_t uuid);

            void ClearBlockedList();

            void ClearApprovedList();

            void ResetWirelessConfig();

            void SetIntervalLength(uint32_t interval_length);

            void ApproveIntervalPairing(uint32_t uuid);

            void SleepDevice(uint32_t uuid);

            void WakeDevice(uint32_t uuid);

            void GetPairingApprovedIntervalList();

            void HandleEvent(const ommo::WirelessManagementEvent& event);

        private:
            mutable std::mutex reactor_mutex_;
            std::shared_ptr<RpcWirelessManagementStreamClientBidiReactor> client_reactor_;
            std::function<void(WirelessManagementEvent*)> wireless_management_event_user_callback_;

    };
}
