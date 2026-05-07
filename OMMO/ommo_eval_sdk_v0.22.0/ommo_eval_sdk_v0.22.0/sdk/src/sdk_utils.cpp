/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#include "sdk_utils.h"

#include <cstdio>
#include <cstdlib>
#include <ctime>

namespace ommo::api
{
    uint64_t Hash(const DeviceDescriptor& r)
    {
        uint64_t hash = r.siu_uuid;
        return (hash << 8) | r.port_id;
    }

    uint64_t Hash(const TrackingDeviceData& r)
    {
        uint64_t hash = r.siu_uuid;
        return (hash << 8) | r.port_id;
    }

    uint64_t Hash(const DeviceID& r)
    {
        uint64_t hash = r.siu_uuid;
        return (hash << 8) | r.port_id;
    }

    uint64_t Hash(uint32_t siu_uuid, uint32_t port_id)
    {
        uint64_t hash = siu_uuid;
        return (hash << 8) | port_id;
    }

    bool SystemTimeToString(uint64_t milliseconds, char* buffer, size_t buffer_size)
    {
        constexpr size_t buffer_size_min = 30;
        // Check the provided buffer size, the output format requires 30 characters (29 + null terminator)
        if (buffer_size < buffer_size_min || buffer == nullptr)
        {
            return false;
        }

        // Verify that the millisecond input is able to be converted to a time_t
        const uint64_t time_t_seconds = milliseconds / 1000;
        // Calculate the maximum value of time_t
        const uint64_t time_t_max = static_cast<uint64_t>(std::time_t(~std::time_t{0} >> 1));
        if (time_t_seconds > time_t_max)
        {
            return false;
        }
        
        std::time_t seconds = milliseconds / 1000;
        std::tm tm_local;
        
#ifdef _WIN32
        if (localtime_s(&tm_local, &seconds) != 0)
        {
            return false;
        }
#else
        if (localtime_r(&seconds, &tm_local) == nullptr)
        {
            return false;
        }
#endif
        int ms = static_cast<int>(milliseconds % 1000);

        // Get the UTC offset in seconds
        int offset_sec = 0;
#ifdef _WIN32
        // _get_timezone() provides the base offset, it may need to be adjusted for DST
        long timezone = 0;
        if (_get_timezone(&timezone) == 0)
        {
            offset_sec = -timezone;
            
            // Check if DST is active and adjust
            if (tm_local.tm_isdst > 0)
            {
                // Add 1 hour (3600 seconds)
                offset_sec += 3600;
            }
        }
        else
        {
            return false;
        }
#else
        offset_sec = tm_local.tm_gmtoff;
#endif
        char sign = (offset_sec >= 0) ? '+' : '-';
        int offset_abs = std::abs(offset_sec);
        int offset_hour = offset_abs / 3600;
        int offset_min = (offset_abs % 3600) / 60;

        /*
         * Verify the values that are not guaranteed to be in the expected range
         * 
         * offset_min and milliseconds are guaranteed due to the use of the % operator
         */
        int year = tm_local.tm_year + 1900;
        if (year < 1900 || year > 9999 ||
            offset_hour < 0 || offset_hour > 23)
        {
            return false;
        }
        
        // Format the output string: YYYY-MM-DDTHH:MM:SS.sss+hh:mm or -hh:mm
        int result = std::snprintf(buffer, buffer_size, "%04d-%02d-%02dT%02d:%02d:%02d.%03d%c%02d:%02d",
            year,
            tm_local.tm_mon + 1,
            tm_local.tm_mday,
            tm_local.tm_hour,
            tm_local.tm_min,
            tm_local.tm_sec,
            ms,
            sign,
            offset_hour,
            offset_min
        );
        
        /*
         * Check if snprintf succeced and produced the expected length (29 characters).
         * The output should be one less than the buffer size due to the null terminator.
         */
        return (result == (buffer_size_min - 1));
    }

}  // namespace ommo::api