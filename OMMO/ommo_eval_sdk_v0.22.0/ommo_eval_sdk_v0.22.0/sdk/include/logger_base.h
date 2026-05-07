/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#include <cassert>
#include <memory>
#include <string>
#include <sstream>


#ifdef OMMO_CUSTOM_LOGGING
#define OMMOLOG_INFO(msg, ...) \
    if (ommo::api::logger) ommo::api::logger->log(ommo::api::LoggerBase::LogLevel::INFO_LEVEL, msg, ##__VA_ARGS__)

#define OMMOLOG_WARN(msg, ...) \
    if (ommo::api::logger) ommo::api::logger->log(ommo::api::LoggerBase::LogLevel::WARN_LEVEL, msg, ##__VA_ARGS__)

#define OMMOLOG_ERROR(msg, ...) \
    if (ommo::api::logger) ommo::api::logger->log(ommo::api::LoggerBase::LogLevel::ERROR_LEVEL, msg, ##__VA_ARGS__)
#else
    #include <spdlog/spdlog.h>
    #define OMMOLOG_INFO(...)  SPDLOG_INFO(__VA_ARGS__)
    #define OMMOLOG_WARN(...)  SPDLOG_WARN(__VA_ARGS__)
    #define OMMOLOG_ERROR(...) SPDLOG_ERROR(__VA_ARGS__)
#endif

namespace ommo::api
{
    // Abstract base class for logging
    class LoggerBase
    {
        public:
            virtual ~LoggerBase() = default;

            virtual void InfoLog(const std::string& message) = 0;
            virtual void WarnLog(const std::string& message) = 0;
            virtual void ErrorLog(const std::string& message) = 0;

            enum LogLevel
            {
                INFO_LEVEL,
                WARN_LEVEL,
                ERROR_LEVEL
            };

            template <typename... Args>
            void log(LogLevel logLevel, const std::string& message, Args&&... args)
            {
                switch (logLevel)
                {
                    case INFO_LEVEL:
                        InfoLog(formatMessage(message, std::forward<Args>(args)...));
                        break;
                    case WARN_LEVEL:
                        WarnLog(formatMessage(message, std::forward<Args>(args)...));
                        break;
                    case ERROR_LEVEL:
                        ErrorLog(formatMessage(message, std::forward<Args>(args)...));
                        break;
                    default:
                        InfoLog(formatMessage(message, std::forward<Args>(args)...));
                        break;
                }
            }

        protected:
            // Variadic template for logging messages with variables
            template <typename... Args>
            std::string  formatMessage(const std::string& message, Args&&... args)
            {
                try
                {
                    return fmt::vformat(message, fmt::make_format_args(args...));
                }
                catch (const fmt::format_error& e)
                {
                    assert(false);
                    return ("[ERROR] Format error: " + std::string(e.what()));
                }
            }
    };

} // namespace ommo::api


namespace ommo::api
{
    inline std::unique_ptr<LoggerBase> logger = nullptr;
    inline void SetLogger(std::unique_ptr<LoggerBase> l) { logger = std::move(l); }
}
