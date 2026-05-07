/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#include "rpcClientCallData.h"


rpcClientCallData::rpcClientCallData(std::shared_ptr<Channel> channel, CompletionQueue* cq, ClientCallState status, std::weak_ptr<ommo::CallDataAssociation> association) :
    channel(channel), completion_queue(cq), status(status), stub(ommo::CoreService::NewStub(channel)), association_(association)
{
    /*
     * Set the internal CallDataInfo to point to this.
     * The CallDataInfo will be used when returning from the completion queue
     * to identify the specific operation the CallData is returning from.
     */
    internal_read_info = {this, OperationType::READ};
    internal_write_info = {this, OperationType::WRITE};
    finish_tag = {this, OperationType::FINISH};
}

rpcClientCallData::~rpcClientCallData()
{
    if (auto assoc = association_.lock())
    {
        assoc->ClearAssociation(static_cast<void*>(this));
    }
}

void rpcClientCallData::Stop()
{
    rwlock_wrlockguard lock(statusLock);
    status = ClientCallState::FINISH;
}

void rpcClientCallData::CancelCall()
{
    listener_active = false;
    grpc_client_context.TryCancel();
}
