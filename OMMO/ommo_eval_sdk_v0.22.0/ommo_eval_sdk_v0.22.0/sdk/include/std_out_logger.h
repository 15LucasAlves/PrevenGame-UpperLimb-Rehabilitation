/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#include "logger_base.h"

namespace ommo::api
{
    // Logger implementation for std::cout
    class StdOutLogger : public LoggerBase
    {
        public:
            void InfoLog(const std::string& message) override;
            void WarnLog(const std::string& message) override;
            void ErrorLog(const std::string& message) override;
    };

} // namespace ommo::api

