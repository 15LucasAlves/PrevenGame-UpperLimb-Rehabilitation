/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
 */

#include "wireless_manager_wrapper.h"

#include "wireless_manager.h"
#include "wireless_manager_impl.h"

namespace ommo
{
    WirelessManagerWrapper::WirelessManagerWrapper(std::shared_ptr<api::WirelessManager> wireless_manager) : wireless_manager_ptr(wireless_manager) {}

    bool WirelessManagerWrapper::ClearAssociation(void *call_data_ptr)
    {
        if (wireless_manager_ptr)
        {
            wireless_manager_ptr->p_impl_->SetClientReactor(nullptr);
            return true;
        }
        return false;
    }
}// namespace ommo
