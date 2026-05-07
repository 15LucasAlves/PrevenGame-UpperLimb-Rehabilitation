/*
 * Copyright 2026 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#include <atomic>
#include <memory>
#include <mutex>
#include <queue>

#include "grpcpp/grpcpp.h"

#include "ommo_service_api.grpc.pb.h"
#include "rpcClientCallData.h"

class RpcWirelessManagementStreamClientBidiReactor : 
    public grpc::ClientBidiReactor<ommo::WirelessManagementRequest, ommo::WirelessManagementEvent>,
    public std::enable_shared_from_this<RpcWirelessManagementStreamClientBidiReactor>
{
public:
    static std::shared_ptr<RpcWirelessManagementStreamClientBidiReactor> Create(
        std::shared_ptr<grpc::Channel> channel,
        const std::function<void(const ommo::WirelessManagementEvent&)> cb_handler,
        std::weak_ptr<ommo::CallDataAssociation> association = std::weak_ptr<ommo::CallDataAssociation>{});

    RpcWirelessManagementStreamClientBidiReactor(const RpcWirelessManagementStreamClientBidiReactor&) = delete;
    RpcWirelessManagementStreamClientBidiReactor& operator=(const RpcWirelessManagementStreamClientBidiReactor&) = delete;

    virtual void OnReadDone(bool ok) override;
    virtual void OnWriteDone(bool ok) override;
    virtual void OnDone(const grpc::Status& status) override;

    void CancelCall();
    void SendWirelessManagementRequest(const ommo::WirelessManagementRequest &request);
    bool IsStreamActive() const;

private:
    void Start();

    explicit RpcWirelessManagementStreamClientBidiReactor(
        std::shared_ptr<grpc::Channel> channel,
        const std::function<void(const ommo::WirelessManagementEvent&)> cb_handler,
        std::weak_ptr<ommo::CallDataAssociation> association);

    void TryRemoveHold();
    
    std::shared_ptr<RpcWirelessManagementStreamClientBidiReactor> self_ptr_;
    
    std::unique_ptr<ommo::CoreService::Stub> stub_;
    grpc::ClientContext context_;
    ommo::WirelessManagementEvent response_;
    const std::function<void(const ommo::WirelessManagementEvent&)> cb_handler_;
    std::atomic<bool> stream_active_{false};
    std::atomic<bool> hold_removed_{false};
    std::atomic<bool> write_in_progress_{false};
    std::mutex write_mutex_;
    std::queue<ommo::WirelessManagementRequest> write_queue_;

    std::weak_ptr<ommo::CallDataAssociation> association_{};
};
