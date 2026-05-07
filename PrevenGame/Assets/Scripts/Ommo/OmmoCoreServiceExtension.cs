// Extensão ao CoreServiceClient — adiciona métodos não incluídos no SDK Unity v0.19.1
// Usa partial class para não modificar os ficheiros originais do SDK.

using grpc = global::Grpc.Core;
using pb = global::Google.Protobuf;

namespace Ommo
{
    // ── BaseStationMotorRunningRequest ────────────────────────────────────
    // Mensagem proto inferida do binário ommo_service_v0.22.0
    // Campo: bool motor_running (field 1)
    public sealed class BaseStationMotorRunningRequest : pb::IMessage<BaseStationMotorRunningRequest>
    {
        private static readonly pb::MessageParser<BaseStationMotorRunningRequest> _parser =
            new pb::MessageParser<BaseStationMotorRunningRequest>(() => new BaseStationMotorRunningRequest());

        public static pb::MessageParser<BaseStationMotorRunningRequest> Parser => _parser;

        public bool MotorRunning { get; set; }

        public pb::Reflection.MessageDescriptor Descriptor => null;

        public BaseStationMotorRunningRequest() { }
        public BaseStationMotorRunningRequest(BaseStationMotorRunningRequest other)
        {
            MotorRunning = other.MotorRunning;
        }

        public BaseStationMotorRunningRequest Clone() => new BaseStationMotorRunningRequest(this);

        public void MergeFrom(BaseStationMotorRunningRequest other)
        {
            if (other.MotorRunning) MotorRunning = other.MotorRunning;
        }

        public void MergeFrom(pb::CodedInputStream input)
        {
            uint tag;
            while ((tag = input.ReadTag()) != 0)
            {
                switch (tag)
                {
                    case 8: MotorRunning = input.ReadBool(); break;
                    default: input.SkipLastField(); break;
                }
            }
        }

        public void WriteTo(pb::CodedOutputStream output)
        {
            if (MotorRunning) { output.WriteRawTag(8); output.WriteBool(MotorRunning); }
        }

        public int CalculateSize() => MotorRunning ? 2 : 0;

        public bool Equals(BaseStationMotorRunningRequest other)
            => other != null && MotorRunning == other.MotorRunning;

        public override bool Equals(object other) => Equals(other as BaseStationMotorRunningRequest);
        public override int GetHashCode() => MotorRunning.GetHashCode();
    }

    // ── BaseStationMotorRunningResponse ───────────────────────────────────
    // Campo: bool success (field 1)
    public sealed class BaseStationMotorRunningResponse : pb::IMessage<BaseStationMotorRunningResponse>
    {
        private static readonly pb::MessageParser<BaseStationMotorRunningResponse> _parser =
            new pb::MessageParser<BaseStationMotorRunningResponse>(() => new BaseStationMotorRunningResponse());

        public static pb::MessageParser<BaseStationMotorRunningResponse> Parser => _parser;

        public bool Success { get; set; }

        public pb::Reflection.MessageDescriptor Descriptor => null;

        public BaseStationMotorRunningResponse() { }
        public BaseStationMotorRunningResponse(BaseStationMotorRunningResponse other)
        {
            Success = other.Success;
        }

        public BaseStationMotorRunningResponse Clone() => new BaseStationMotorRunningResponse(this);

        public void MergeFrom(BaseStationMotorRunningResponse other)
        {
            if (other.Success) Success = other.Success;
        }

        public void MergeFrom(pb::CodedInputStream input)
        {
            uint tag;
            while ((tag = input.ReadTag()) != 0)
            {
                switch (tag)
                {
                    case 8: Success = input.ReadBool(); break;
                    default: input.SkipLastField(); break;
                }
            }
        }

        public void WriteTo(pb::CodedOutputStream output)
        {
            if (Success) { output.WriteRawTag(8); output.WriteBool(Success); }
        }

        public int CalculateSize() => Success ? 2 : 0;

        public bool Equals(BaseStationMotorRunningResponse other)
            => other != null && Success == other.Success;

        public override bool Equals(object other) => Equals(other as BaseStationMotorRunningResponse);
        public override int GetHashCode() => Success.GetHashCode();
    }

    // ── CoreService extension ─────────────────────────────────────────────
    public static partial class CoreService
    {
        // Marshallers para HardwareStates
        static readonly grpc::Marshaller<global::Ommo.HardwareStatesRequest> __Marshaller_HardwareStatesRequest =
            grpc::Marshallers.Create(
                arg => pb::MessageExtensions.ToByteArray(arg),
                global::Ommo.HardwareStatesRequest.Parser.ParseFrom);

        static readonly grpc::Marshaller<global::Ommo.HardwareStates> __Marshaller_HardwareStates =
            grpc::Marshallers.Create(
                arg => pb::MessageExtensions.ToByteArray(arg),
                global::Ommo.HardwareStates.Parser.ParseFrom);

        // Marshallers para MotorRunning
        static readonly grpc::Marshaller<global::Ommo.BaseStationMotorRunningRequest> __Marshaller_MotorRequest =
            grpc::Marshallers.Create(
                (arg) => pb::MessageExtensions.ToByteArray(arg),
                global::Ommo.BaseStationMotorRunningRequest.Parser.ParseFrom);

        static readonly grpc::Marshaller<global::Ommo.BaseStationMotorRunningResponse> __Marshaller_MotorResponse =
            grpc::Marshallers.Create(
                (arg) => pb::MessageExtensions.ToByteArray(arg),
                global::Ommo.BaseStationMotorRunningResponse.Parser.ParseFrom);

        // Métodos gRPC
        static readonly grpc::Method<global::Ommo.HardwareStatesRequest, global::Ommo.HardwareStates>
            __Method_GetHardwareStates =
                new grpc::Method<global::Ommo.HardwareStatesRequest, global::Ommo.HardwareStates>(
                    grpc::MethodType.Unary, "ommo.CoreService", "GetHardwareStates",
                    __Marshaller_HardwareStatesRequest, __Marshaller_HardwareStates);

        static readonly grpc::Method<global::Ommo.BaseStationMotorRunningRequest, global::Ommo.BaseStationMotorRunningResponse>
            __Method_SetBaseStationMotorRunning =
                new grpc::Method<global::Ommo.BaseStationMotorRunningRequest, global::Ommo.BaseStationMotorRunningResponse>(
                    grpc::MethodType.Unary, "ommo.CoreService", "SetBaseStationMotorRunning",
                    __Marshaller_MotorRequest, __Marshaller_MotorResponse);

        public partial class CoreServiceClient
        {
            // GetHardwareStates
            public virtual global::Ommo.HardwareStates GetHardwareStates(
                global::Ommo.HardwareStatesRequest request, grpc::CallOptions options)
                => CallInvoker.BlockingUnaryCall(__Method_GetHardwareStates, null, options, request);

            public virtual global::Ommo.HardwareStates GetHardwareStates(
                global::Ommo.HardwareStatesRequest request)
                => GetHardwareStates(request, new grpc::CallOptions());

            public virtual grpc::AsyncUnaryCall<global::Ommo.HardwareStates> GetHardwareStatesAsync(
                global::Ommo.HardwareStatesRequest request, grpc::CallOptions options)
                => CallInvoker.AsyncUnaryCall(__Method_GetHardwareStates, null, options, request);

            public virtual grpc::AsyncUnaryCall<global::Ommo.HardwareStates> GetHardwareStatesAsync(
                global::Ommo.HardwareStatesRequest request)
                => GetHardwareStatesAsync(request, new grpc::CallOptions());

            // SetBaseStationMotorRunning
            public virtual bool SetBaseStationMotorRunning(bool enable)
            {
                try
                {
                    var request = new global::Ommo.BaseStationMotorRunningRequest { MotorRunning = enable };
                    var response = CallInvoker.BlockingUnaryCall(
                        __Method_SetBaseStationMotorRunning, null, new grpc::CallOptions(), request);
                    UnityEngine.Debug.Log($"[MotorControl] SetMotorRunning({enable}) — success: {response?.Success}");
                    return response?.Success ?? false;
                }
                catch (grpc::RpcException e)
                {
                    UnityEngine.Debug.LogWarning($"[MotorControl] RPC error: {e.Status}");
                    return false;
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[MotorControl] Erro: {e.Message}");
                    return false;
                }
            }
        }
    }
}