/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#include "std_out_logger.h"

#include <iostream>

namespace ommo::api
{
    void StdOutLogger::InfoLog(const std::string& message)
    {
        std::cout << "[OMMOLOG_INFO] " << message << std::endl;
    }

    void StdOutLogger::WarnLog(const std::string& message)
    {
        std::cout << "[OMMOLOG_WARN] " << message << std::endl;
    }

    void StdOutLogger::ErrorLog(const std::string& message)
    {
        std::cerr << "[OMMOLOG_ERROR] " << message << std::endl;
    }
}

