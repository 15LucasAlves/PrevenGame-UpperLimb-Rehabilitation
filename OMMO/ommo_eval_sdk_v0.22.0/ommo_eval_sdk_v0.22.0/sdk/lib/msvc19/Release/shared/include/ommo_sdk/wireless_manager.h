/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
 */

#pragma once

// Define DLL exports
#ifdef _WIN32
#ifdef OMMO_SDK_STATIC
#define OMMO_SDK_API
#elif defined(OMMO_SDK_EXPORTS)
#define OMMO_SDK_API __declspec(dllexport)
#else
#define OMMO_SDK_API __declspec(dllimport)
#endif
#else
#ifdef OMMO_SDK_STATIC
#define OMMO_SDK_API
#else
#define OMMO_SDK_API __attribute__((visibility("default")))
#endif
#endif

#include "sdk_types.h"

#include <functional>

namespace ommo
{
    class ClientManager;
    class WirelessManagerWrapper;
}

namespace ommo::api
{

    class OMMO_SDK_API WirelessManager
    {
        public:
            explicit WirelessManager();
            WirelessManager(const WirelessManager& other) = delete;
            WirelessManager& operator= (const WirelessManager& other) = delete;
            ~WirelessManager();
            void CancelStream();
            bool IsStreamActive();

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

        private:
            class impl;
            WirelessManager::impl* const p_impl_;

            friend class ommo::ClientManager;
            friend class ommo::WirelessManagerWrapper;
    };
} // namespace ommo::api
