/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#include "rpc_base_station_data_stream_client_call_data.h"
#include <cassert>

#include <grpc/support/log.h>

using grpc::ClientAsyncReader;

RpcBaseStationDataStreamClientCallData::RpcBaseStationDataStreamClientCallData(
    std::shared_ptr<Channel> channel, 
    CompletionQueue* cq, 
    const ommo::BaseStationDataStreamRequest &request, 
    const std::function<void(const ommo::BaseStationData&)> cb_handler, 
    std::weak_ptr<ommo::CallDataAssociation> association)
    : rpcClientCallData(
        channel, cq, ClientCallState::CONNECTING,
        association
    ), cb_handler_(cb_handler)
{
    // Prepare call get reader
    reader_ = stub->PrepareAsyncOpenBaseStationDataStream(&grpc_client_context, request, completion_queue);

    // StartCall initiates the RPC call
    reader_->StartCall(&internal_read_info);

    if (cb_handler)
    {
        listener_active = true;
    }
}

bool RpcBaseStationDataStreamClientCallData::Proceed(OperationType op_type)
{
    rwlock_wrlockguard lock(statusLock);

    if (status == ClientCallState::CONNECTING)
    {       
        // Start a read
        reader_->Read(&response_, &internal_read_info);
        status = ClientCallState::PROCESSING;

        return true;
    }
    else if (status == ClientCallState::PROCESSING)
    {
        // Read finished send to callback_handler
        if (listener_active && cb_handler_)
        {
            cb_handler_(response_);
        }

        // Start another read
        reader_->Read(&response_, &internal_read_info);

        return true;
    }
    else
    {
        // Once in the FINISH state, deallocate ourselves (CallData).
        assert(status == ClientCallState::FINISH);
        // Delete this object
        return false;
    }
}