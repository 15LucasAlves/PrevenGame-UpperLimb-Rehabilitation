/*
 * Copyright 2026 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
 */

#include "rpc_reference_device_state_stream_client_read_reactor.h"


RpcReferenceDeviceStateStreamClientReadReactor::RpcReferenceDeviceStateStreamClientReadReactor(
    std::shared_ptr<grpc::Channel> channel,
    const ommo::ReferenceDeviceStateStreamRequest &request,
    std::function<void(const ommo::ReferenceDeviceState &)> cb)
    : stub_(ommo::CoreService::NewStub(channel)), request_(request), cb_handler_(cb)
{
    stub_->async()->OpenReferenceDeviceStateStream(&context_, &request_, this);

    StartRead(&response_);

    StartCall();

    if (cb_handler_)
    {
        listener_active_.store(true);
    }
}

void RpcReferenceDeviceStateStreamClientReadReactor::OnReadDone(bool ok)
{
    if (!ok)
    {
        // OnDone will be called next.
        return;
    }

    if (listener_active_ && cb_handler_)
    {
        cb_handler_(response_);
    }

    // Make sure not to start a new StartRead while attempting to cancel.
    if (listener_active_ && !done_)
    {
        StartRead(&response_);
    }
}

void RpcReferenceDeviceStateStreamClientReadReactor::OnDone(const grpc::Status &status)
{
    std::unique_lock<std::mutex> lock(mu_);
    done_ = true;
    lock.unlock();
    cv_.notify_all();
}

void RpcReferenceDeviceStateStreamClientReadReactor::CancelCall()
{
    /*
     * After TryCancel() is called, OnReadDone() will be invoked with ok == false,
     * followed by OnDone(), which is the final reactor callback.
     *
     * Although the gRPC documentation states that a reactor can be deleted in OnDone(),
     * that does not apply here because ClientManager could terminate the reactor externally,
     * by invoking CancelCall(). In that case, if the reactor was deleted in OnDone(), 
     * it is possible that CancelCall() has not completed yet, leading to a use-after-free.
     *
     * Therefore, ClientManager must wait until OnDone() has completed before destroying
     * the reactor.
     */
    listener_active_ = false;
    context_.TryCancel();
}

void RpcReferenceDeviceStateStreamClientReadReactor::WaitForDone()
{
    std::unique_lock<std::mutex> lock(mu_);
    cv_.wait(lock, [this] { return done_.load(); });
}
