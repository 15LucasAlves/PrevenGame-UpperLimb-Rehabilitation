/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
 */

#include "rpc_tracking_devices_event_stream_client_read_reactor.h"


RpcTrackingDevicesEventStreamClientReadReactor::RpcTrackingDevicesEventStreamClientReadReactor(
    std::shared_ptr<grpc::Channel> channel,
    const ommo::TrackingDevicesEventStreamRequest &request,
    std::function<void(const ommo::TrackingDeviceEvent &)> cb)
    : stub_(ommo::CoreService::NewStub(channel)), request_(request), cb_handler_(cb)
{
    stub_->async()->OpenTrackingDevicesEventStream(&context_, &request_, this);

    StartRead(&response_);

    StartCall();

    if (cb_handler_)
    {
        listener_active_.store(true);
    }
}

void RpcTrackingDevicesEventStreamClientReadReactor::OnReadDone(bool ok)
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

void RpcTrackingDevicesEventStreamClientReadReactor::OnDone(const grpc::Status &status)
{
    std::unique_lock<std::mutex> lock(mu_);
    done_ = true;
    lock.unlock();
    cv_.notify_all();
}

void RpcTrackingDevicesEventStreamClientReadReactor::CancelCall()
{
    /*
     * After TryCancel() is called, OnReadDone() will be invoked with ok == false,
     * followed by OnDone(), which is the final reactor callback.
     *
     * Although the gRPC documentation states that a reactor can be deleted in OnDone(),
     * that does not apply here because ClientManager could terminate the reactor externally,
     * by invoking CancelCall(). In that case, if the reactor were deleted in OnDone(), 
     * it is possible that CancelCall() has not completed yet, leading to a use-after-free.
     *
     * Therefore, ClientManager must wait until OnDone() has completed before destroying
     * the reactor.
     */
    listener_active_ = false;
    context_.TryCancel();
}

void RpcTrackingDevicesEventStreamClientReadReactor::WaitForDone()
{
    std::unique_lock<std::mutex> lock(mu_);
    cv_.wait(lock, [this] { return done_.load(); });
}
