/*
 * Copyright 2026 Ommo Technologies, Inc. - All Rights Reserved
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


class RpcReferenceDeviceStateStreamClientReadReactor : public grpc::ClientReadReactor<ommo::ReferenceDeviceState>
{
public:
    RpcReferenceDeviceStateStreamClientReadReactor(
                        std::shared_ptr<grpc::Channel> channel,
                        const ommo::ReferenceDeviceStateStreamRequest &request,
                        std::function<void(const ommo::ReferenceDeviceState&)> cb);

    virtual void OnReadDone(bool ok) override;
    virtual void OnDone(const grpc::Status& status) override;
    void CancelCall();
    void WaitForDone();

private:
    ommo::ReferenceDeviceStateStreamRequest request_;
    std::unique_ptr<ommo::CoreService::Stub> stub_;
    grpc::ClientContext context_;
    ommo::ReferenceDeviceState response_;
    const std::function<void(const ommo::ReferenceDeviceState&)> cb_handler_;
    std::atomic<bool> listener_active_{false}; 
    std::mutex mu_;
    std::condition_variable cv_;
    std::atomic<bool> done_{false};
};