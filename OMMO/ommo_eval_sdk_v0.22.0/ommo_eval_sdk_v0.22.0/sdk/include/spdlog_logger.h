/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#include "logger_base.h"
#include <spdlog/spdlog.h>

namespace ommo::api
{
    // Logger implementation for SpdLog
    class SpdLogLogger : public LoggerBase
    {
        public:
            void InfoLog(const std::string& message) override;
            void WarnLog(const std::string& message) override;
            void ErrorLog(const std::string& message) override;
    };

    struct SpdLogConfig
    {
        std::string                    name = "ommo_sdk";
        std::string                    pattern = "[%Y-%m-%d %H:%M:%S.%e] [%l] [%s:%!:%#] [thread %t] %v";
        std::string                    file_name;             // empty => no file sink
        spdlog::level::level_enum      console_level = spdlog::level::trace;
        spdlog::level::level_enum      file_level = spdlog::level::trace;
        bool                           rotating = false;
        size_t                         max_size = 1048576;
        size_t                         max_files = 3;
    };

    void ConfigureSpdLog(const SpdLogConfig& cfg);
} // namespace ommo::api

