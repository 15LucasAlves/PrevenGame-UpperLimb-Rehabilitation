/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#include "wireless_manager.h"
#include "wireless_manager_impl.h"


#include "protobuf_converters.h"
#include "rpc_wireless_management_stream_client_bidi_reactor.h"

namespace ommo::api
{
    WirelessManager::WirelessManager() : p_impl_(new WirelessManager::impl()) {}

    WirelessManager::~WirelessManager()
    {
        p_impl_->CancelStream();
        delete p_impl_;
    }

    void WirelessManager::CancelStream()
    {
        p_impl_->CancelStream();
    }

    bool WirelessManager::IsStreamActive()
    {
        return p_impl_->IsStreamActive();
    }

    void WirelessManager::RegisterWirelessEventCallback(std::function<void(WirelessManagementEvent*)> callback_function)
    {
        p_impl_->RegisterWirelessEventCallback(callback_function);
    }

    void WirelessManager::ResetWirelessEventCallback()
    {
        p_impl_->ResetWirelessEventCallback();
    }

    void WirelessManager::EnablePairingMode()
    {
        p_impl_->EnablePairingMode();
    }

    void WirelessManager::DisablePairingMode()
    {
        p_impl_->DisablePairingMode();
    }

    void WirelessManager::GetPairingApprovedList()
    {
        p_impl_->GetPairingApprovedList();
    }

    void WirelessManager::ApprovePairing(uint32_t uuid)
    {
        p_impl_->ApprovePairing(uuid);
    }

    void WirelessManager::DenyPairing(uint32_t uuid)
    {
        p_impl_->DenyPairing(uuid);
    }

    void WirelessManager::Unpair(uint32_t uuid)
    {
        p_impl_->Unpair(uuid);
    }

    void WirelessManager::GetPairingBlockedList()
    {
        p_impl_->GetPairingBlockedList();
    }

    void WirelessManager::BlockPairing(uint32_t uuid)
    {
        p_impl_->BlockPairing(uuid);
    }

    void WirelessManager::UnblockPairing(uint32_t uuid)
    {
        p_impl_->UnblockPairing(uuid);
    }

    void WirelessManager::ClearBlockedList()
    {
        p_impl_->ClearBlockedList();
    }

    void WirelessManager::ClearApprovedList()
    {
        p_impl_->ClearApprovedList();
    }

    void WirelessManager::ResetWirelessConfig()
    {
        p_impl_->ResetWirelessConfig();
    }

    void WirelessManager::SetIntervalLength(uint32_t interval_length)
    {
        p_impl_->SetIntervalLength(interval_length);
    }

    void WirelessManager::ApproveIntervalPairing(uint32_t uuid)
    {
        p_impl_->ApproveIntervalPairing(uuid);
    }

    void WirelessManager::SleepDevice(uint32_t uuid)
    {
        p_impl_->SleepDevice(uuid);
    }

    void WirelessManager::WakeDevice(uint32_t uuid)
    {
        p_impl_->WakeDevice(uuid);
    }

    void WirelessManager::GetPairingApprovedIntervalList()
    {
        p_impl_->GetPairingApprovedIntervalList();
    }


    void WirelessManager::impl::CancelStream()
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            client_reactor_->CancelCall();
        }
    }

    bool WirelessManager::impl::IsStreamActive() const
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_)
        {
            return client_reactor_->IsStreamActive();
        }
        return false;
    }

    void WirelessManager::impl::SetClientReactor(std::shared_ptr<RpcWirelessManagementStreamClientBidiReactor> client_reactor)
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        client_reactor_ = client_reactor;
    }

    void WirelessManager::impl::RegisterWirelessEventCallback(std::function<void(WirelessManagementEvent*)> callback_function)
    {
        wireless_management_event_user_callback_ = callback_function;
    }

    void WirelessManager::impl::ResetWirelessEventCallback()
    {
        wireless_management_event_user_callback_ = nullptr;
    }

    void WirelessManager::impl::EnablePairingMode()
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_ENABLE_PAIRING_MODE);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::DisablePairingMode()
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_DISABLE_PAIRING_MODE);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::GetPairingApprovedList()
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_GET_PAIRING_APPROVED_LIST);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::ApprovePairing(uint32_t uuid)
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_APPROVE_PAIRING);
            request.set_siu_uuid(uuid);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::DenyPairing(uint32_t uuid)
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_DENY_PAIRING);
            request.set_siu_uuid(uuid);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::Unpair(uint32_t uuid)
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_UNPAIR);
            request.set_siu_uuid(uuid);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::GetPairingBlockedList()
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_GET_PAIRING_BLOCKED_LIST);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::BlockPairing(uint32_t uuid)
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_BLOCK_PAIRING);
            request.set_siu_uuid(uuid);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::UnblockPairing(uint32_t uuid)
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_UNBLOCK_PAIRING);
            request.set_siu_uuid(uuid);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::ClearBlockedList()
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_CLEAR_BLOCKED_LIST);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::ClearApprovedList()
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_CLEAR_APPROVED_LIST);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::ResetWirelessConfig()
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_RESET_WIRELESS_CONFIG);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::SetIntervalLength(uint32_t interval_length)
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_SET_INTERVAL_LENGTH);
            request.set_interval_length(interval_length);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::ApproveIntervalPairing(uint32_t uuid)
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_APPROVE_INTERVAL_PAIRING);
            request.set_siu_uuid(uuid);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::SleepDevice(uint32_t uuid)
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_SLEEP_DEVICE);
            request.set_siu_uuid(uuid);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::WakeDevice(uint32_t uuid)
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_WAKE_DEVICE);
            request.set_siu_uuid(uuid);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::GetPairingApprovedIntervalList()
    {
        std::lock_guard<std::mutex> lock(reactor_mutex_);
        if (client_reactor_ && client_reactor_->IsStreamActive())
        {
            ommo::WirelessManagementRequest request;
            request.set_request_type(ommo::WirelessManagementRequestType::WIRELESS_MANAGEMENT_REQUEST_GET_PAIRING_APPROVED_INTERVAL_LIST);
            client_reactor_->SendWirelessManagementRequest(request);
        }
    }

    void WirelessManager::impl::HandleEvent(const ommo::WirelessManagementEvent& event)
    {
        if (wireless_management_event_user_callback_)
        {
            // Release the memory to the client
            WirelessManagementEvent* raw_event = ProtoToWirelessManagementEvent(event).release();
            wireless_management_event_user_callback_(raw_event);
        }
    }

} // namespace ommo::api
