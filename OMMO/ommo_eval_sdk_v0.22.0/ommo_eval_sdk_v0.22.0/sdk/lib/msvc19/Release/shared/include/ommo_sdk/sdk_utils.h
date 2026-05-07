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

namespace ommo::api
{
    OMMO_SDK_API uint64_t Hash(const DeviceDescriptor& r);

    OMMO_SDK_API uint64_t Hash(const TrackingDeviceData& r);

    OMMO_SDK_API uint64_t Hash(const DeviceID& r);

    OMMO_SDK_API uint64_t Hash(uint32_t siu_uuid, uint32_t port_id);

    /*
     * Convert a milliseconds timestamp to an ISO 8601-1:2019/Amd 1:2022 formatted string in local time.
     * 
     * ISO 8601 format: YYYY-MM-DDTHH:MM:SS.sss+hh:mm
     * 
     * @param milliseconds The timestamp in milliseconds since epoch
     * @param buffer Output buffer to store the formatted string (must be at least 30 characters)
     * @param buffer_size Size of the output buffer
     * @return true if conversion was successful, false otherwise
     * 
     * Note: The function will return false if:
     * - buffer_size is less than 30 (29 characters + 1 null terminator)
     * - milliseconds value is too large for std::time_t
     * - time conversion fails
     * 
     * Notes:
     * - The formatted string is represented in local time using the +hh:mm standard
     * - Daylight savings time adjustments are included in the offset calculation
     */
    OMMO_SDK_API bool SystemTimeToString(uint64_t milliseconds, char* buffer, size_t buffer_size);

}  // namespace ommo::api
