/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
 */

#pragma once

#include "rpcClientCallData.h"

namespace ommo
{
    namespace api
    {
        class WirelessManager;
    }

    class WirelessManagerWrapper : public ommo::CallDataAssociation
    {
    public:
        explicit WirelessManagerWrapper(std::shared_ptr<api::WirelessManager> wireless_manager);
        ~WirelessManagerWrapper() = default;

        bool ClearAssociation(void *call_data_ptr) override;

        std::shared_ptr<api::WirelessManager> wireless_manager_ptr;
    };
} // namespace ommo
