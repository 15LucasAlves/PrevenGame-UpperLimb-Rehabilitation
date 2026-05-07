/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#include "spdlog_logger.h"

#include <spdlog/async.h>
#include <spdlog/sinks/basic_file_sink.h>
#include <spdlog/sinks/rotating_file_sink.h>
#include <spdlog/sinks/stdout_color_sinks.h>
#include <spdlog/spdlog.h>

#include <mutex>
#include <iostream>

namespace ommo::api
{
    void SpdLogLogger::InfoLog(const std::string& message)
    {
        SPDLOG_INFO(message);
    }

    void SpdLogLogger::WarnLog(const std::string& message)
    {
        SPDLOG_WARN(message);
    }

    void SpdLogLogger::ErrorLog(const std::string& message)
    {
        SPDLOG_ERROR(message);
    }

    void ConfigureSpdLog(const SpdLogConfig& cfg)
    {
        static std::once_flag once;
        std::call_once(once, [&]()
        {
            // Ensure there are no existing sinks
            spdlog::drop_all();

            try
            {
                std::vector<spdlog::sink_ptr> sinks;

                // Configure the console sink
                auto console_sink = std::make_shared<spdlog::sinks::stdout_color_sink_mt>();
                console_sink->set_level(cfg.console_level);
                console_sink->set_pattern(cfg.pattern);
                sinks.push_back(console_sink);

                // If no file name was provided, only use the console logger
                if (!cfg.file_name.empty())
                {
                    spdlog::sink_ptr file_sink;
                    if (cfg.rotating)
                    {
                        file_sink = std::make_shared<spdlog::sinks::rotating_file_sink_mt>(
                            cfg.file_name, cfg.max_size, cfg.max_files
                        );
                    }
                    else
                    {
                        file_sink = std::make_shared<spdlog::sinks::basic_file_sink_mt>(
                            cfg.file_name, /*truncate=*/true
                        );
                    }
                    file_sink->set_level(cfg.file_level);
                    file_sink->set_pattern(cfg.pattern);
                    sinks.push_back(file_sink);
                }

                // Create logger
                auto logger = std::make_shared<spdlog::logger>(cfg.name, sinks.begin(), sinks.end());
                logger->set_level(cfg.console_level);
                spdlog::set_default_logger(logger);
            }
            catch (const spdlog::spdlog_ex& ex)
            {
                std::cerr << "[ConfigureSpdLog] error: " << ex.what() << "\n";
            }
        });
    }
} // namespace ommo::api
