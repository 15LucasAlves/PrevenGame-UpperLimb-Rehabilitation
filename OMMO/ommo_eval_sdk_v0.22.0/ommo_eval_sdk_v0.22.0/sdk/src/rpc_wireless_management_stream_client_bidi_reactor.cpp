/*
 * Copyright 2026 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
 */

#include "rpc_wireless_management_stream_client_bidi_reactor.h"

#include <grpc/support/log.h>
#include <cassert>

#include "logger_base.h"

/*
 * The constructor of RpcWirelessManagementStreamClientBidiReactor is private;
 * objects must be created using the Create() function. This ensures that the user cannot 
 * instantiate an object directly, and allows the object to hold a shared pointer 
 * to itself to manage its own lifecycle.
 * 
 * After the reactor is created, its shared pointer is passed to the WirelessManager. 
 * However, the WirelessManager does not need to maintain the reactor's lifecycle explicitly. 
 * The reactor will automatically self-destruct after OnDone() is called.
 */

RpcWirelessManagementStreamClientBidiReactor::RpcWirelessManagementStreamClientBidiReactor(
    std::shared_ptr<grpc::Channel> channel,
    const std::function<void(const ommo::WirelessManagementEvent &)> cb_handler,
    std::weak_ptr<ommo::CallDataAssociation> association)
    : stub_(ommo::CoreService::NewStub(channel)),
      cb_handler_(cb_handler),
      association_(association)
{
}

std::shared_ptr<RpcWirelessManagementStreamClientBidiReactor> RpcWirelessManagementStreamClientBidiReactor::Create(
        std::shared_ptr<grpc::Channel> channel,
        const std::function<void(const ommo::WirelessManagementEvent&)> cb_handler,
        std::weak_ptr<ommo::CallDataAssociation> association)
{
    std::shared_ptr<RpcWirelessManagementStreamClientBidiReactor> instance(
        new RpcWirelessManagementStreamClientBidiReactor(channel, cb_handler, association)
    );
    instance->Start();

    return instance;
}

void RpcWirelessManagementStreamClientBidiReactor::Start()
{
    stub_->async()->OpenWirelessManagementStream(&context_, this);

    StartRead(&response_);
    // Add a hold to ensure OnDone is not called before the user or ommo service closes the stream.
    AddHold();

    stream_active_ = true;
    StartCall();

    self_ptr_ = shared_from_this();
}

void RpcWirelessManagementStreamClientBidiReactor::SendWirelessManagementRequest(const ommo::WirelessManagementRequest &request)
{
    std::lock_guard<std::mutex> lock(write_mutex_);
    if (!stream_active_)
    {
        OMMOLOG_WARN("Attempted to send a wireless management request to an inactive stream");
        return;
    }

    write_queue_.push(request);

    if (!write_in_progress_)
    {
        // Keep the request valid until the write is done.
        write_in_progress_ = true;
        StartWrite(&write_queue_.front());
    }
}

void RpcWirelessManagementStreamClientBidiReactor::OnReadDone(bool ok)
{
    std::lock_guard<std::mutex> lock(write_mutex_);

    if (!ok)
    {
        stream_active_ = false;
        TryRemoveHold();
        return;
    }

    if (cb_handler_)
    {
        cb_handler_(response_);
    }

    // Make sure not to start a new StartRead while attempting to cancel.
    if (stream_active_)
    {
        StartRead(&response_);
    }
}

void RpcWirelessManagementStreamClientBidiReactor::OnWriteDone(bool ok)
{
    std::lock_guard<std::mutex> lock(write_mutex_);

    // Remove the request that just finished whether it was successful or not
    if (!write_queue_.empty())
    {
        write_queue_.pop();
    }
    write_in_progress_ = false;

    if (!ok)
    {
        OMMOLOG_WARN("Failed to send a wireless management request");
        stream_active_ = false;
        TryRemoveHold();
        return;
    }

    if (stream_active_ && !write_queue_.empty())
    {
        StartWrite(&write_queue_.front());
        write_in_progress_ = true;
    }
}

void RpcWirelessManagementStreamClientBidiReactor::TryRemoveHold()
{
    // Do not remove hold if it has been removed.
    if (hold_removed_)
    {
        return;
    }

    RemoveHold();
    hold_removed_ = true;
}

void RpcWirelessManagementStreamClientBidiReactor::OnDone(const grpc::Status &status)
{
    OMMOLOG_INFO("Wireless management stream is closing with status: {}", status.error_message());
    /*
     * If service shuts down or the client cancels the stream, OnDone() will be called as the 
     * final callback.
     * 
     * The associated WirelessManager is informed to clear its stored pointer for this reactor.
     * Finally, self_ptr_ is reset to allow the reactor to be destructed.
     */
    if (auto assoc = association_.lock())
    {
        assoc->ClearAssociation(static_cast<void*>(this));
    }

    self_ptr_.reset();
}


void RpcWirelessManagementStreamClientBidiReactor::CancelCall()
{
    /*
     * After calling TryCancel(), OnReadDone() or OnWriteDone() will be called with 
     * ok == false if a Read or Write operation was in progress, followed by OnDone(), 
     * which is the final reactor callback.
     * 
     * OnDone() will notify the associated WirelessManager to remove its stored pointer 
     * for this reactor, and then reset self_ptr_ to allow the reactor to be destructed.
     */

    // Track whether StartRead or StartWrite can be called
    stream_active_ = false;

    context_.TryCancel();
}

bool RpcWirelessManagementStreamClientBidiReactor::IsStreamActive() const
{
    return stream_active_;
}