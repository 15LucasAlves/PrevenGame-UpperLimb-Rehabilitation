/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#include <variant>
#include "grpcpp/grpcpp.h"
#include "ommo_service_api.grpc.pb.h"
#include "rwlock.h"

using grpc::Channel;
using grpc::ClientAsyncResponseReader;
using grpc::ClientContext;
using grpc::CompletionQueue;
using grpc::Status;

enum class ClientCallState { CONNECTING, PROCESSING, WAITING, FINISH };

enum class OperationType { READ, WRITE, FINISH };

typedef struct
{
    void* call_data;
    OperationType op_type;
} CallDataInfo;

namespace ommo
{
    class CallDataAssociation
    {
    public:
        virtual bool ClearAssociation(void* call_data_ptr) = 0;
    };
}

class rpcClientCallData
{
public:
    rpcClientCallData(
        std::shared_ptr<Channel> channel, 
        CompletionQueue* cq, 
        ClientCallState status, 
        std::weak_ptr<ommo::CallDataAssociation> association = std::weak_ptr<ommo::CallDataAssociation>{}
    );
    virtual ~rpcClientCallData();
    void CancelCall();
    void Stop();

    virtual bool Proceed(OperationType op_type) = 0;

    bool listener_active = false; 

protected:
    CompletionQueue* completion_queue;
    ClientContext grpc_client_context;
    ClientCallState status;
    std::shared_ptr<Channel> channel;
    std::unique_ptr<ommo::CoreService::Stub> stub;
    rwlock statusLock = RWLOCK_INIT_VAL;

    // Store each internal tag type so that we can distinguish what event is being returned
    CallDataInfo internal_read_info;
    CallDataInfo internal_write_info;
    CallDataInfo finish_tag;

    std::weak_ptr<ommo::CallDataAssociation> association_{};
};
