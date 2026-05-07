/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#include "grpcpp/grpcpp.h"
#include "ommo_service_api.grpc.pb.h"
#include "rpcClientCallData.h"

using grpc::ClientAsyncReader;

class RpcTrackingGroupsEventStreamClientCallData : public rpcClientCallData
{
public:
    RpcTrackingGroupsEventStreamClientCallData(
        std::shared_ptr<Channel> channel, 
        CompletionQueue* cq, 
        const ommo::TrackingGroupsEventStreamRequest &request, 
        const std::function<void(const ommo::TrackingGroupEvent&)> cb_handler
    );
    bool Proceed(OperationType op_type);

private:
    const std::function<void(const ommo::TrackingGroupEvent&)> cb_handler_;
    ommo::TrackingGroupEvent response_;
    std::unique_ptr<ClientAsyncReader<ommo::TrackingGroupEvent>> reader_;
};