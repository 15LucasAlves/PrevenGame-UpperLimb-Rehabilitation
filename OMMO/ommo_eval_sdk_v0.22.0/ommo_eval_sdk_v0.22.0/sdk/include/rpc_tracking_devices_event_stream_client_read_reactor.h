/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#include <atomic>
#include <condition_variable>
#include <mutex>

#include "grpcpp/grpcpp.h"

#include "ommo_service_api.grpc.pb.h"


class RpcTrackingDevicesEventStreamClientReadReactor : public grpc::ClientReadReactor<ommo::TrackingDeviceEvent>
{
public:
    RpcTrackingDevicesEventStreamClientReadReactor(
                        std::shared_ptr<grpc::Channel> channel,
                        const ommo::TrackingDevicesEventStreamRequest &request,
                        std::function<void(const ommo::TrackingDeviceEvent&)> cb);

    virtual void OnReadDone(bool ok) override;
    virtual void OnDone(const grpc::Status& status) override;
    void CancelCall();
    void WaitForDone();

private:
    ommo::TrackingDevicesEventStreamRequest request_;
    std::unique_ptr<ommo::CoreService::Stub> stub_;
    grpc::ClientContext context_;
    ommo::TrackingDeviceEvent response_;
    const std::function<void(const ommo::TrackingDeviceEvent&)> cb_handler_;
    std::atomic<bool> listener_active_{false}; 
    std::mutex mu_;
    std::condition_variable cv_;
    std::atomic<bool> done_{false};
};
