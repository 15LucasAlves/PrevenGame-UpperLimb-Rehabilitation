#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Net.Http;
using Grpc.Net.Client;

/// <summary>
/// OmmoPairingHelper — Menu de editor para emparelhar SIUs com o Wireless Receiver.
///
/// Menu: Ommo → Emparelhar SIU (Pairing)
///
/// O pairing só precisa de ser feito UMA VEZ por SIU — depois fica guardado
/// pelo Ommo Service e o SIU é reconhecido automaticamente em todas as sessões.
///
/// Pré-requisito: o serviço Ommo deve estar a correr (entra em Play Mode primeiro
/// para que o OmmoServiceLauncher o inicie, ou fica activo de uma sessão anterior).
/// </summary>
public static class OmmoPairingHelper
{
    [MenuItem("Ommo/Emparelhar SIU (Pairing)")]
    static void EmparelharSIU()
    {
        bool continuar = EditorUtility.DisplayDialog(
            "Ommo — Emparelhar SIU",
            "Processo de pairing (apenas necessário UMA VEZ por sensor):\n\n" +
            "1. Clica «Iniciar» — o modo de pairing fica ativo por 30s\n" +
            "2. Pressiona o botão no SIU (sensor branco pequeno)\n" +
            "3. O SIU é aprovado automaticamente\n\n" +
            "Pré-requisito: o Unity deve estar em Play Mode\n" +
            "(ou o ommo_service_v0.22.0.exe estar em execução).",
            "Iniciar", "Cancelar");

        if (!continuar) return;

        _ = EmparelharAsync();
    }

    static async Task EmparelharAsync()
    {
        var handler = new YetAnotherHttpHandler { Http2Only = true };
        var channel = GrpcChannel.ForAddress(
            "http://localhost:50051",
            new GrpcChannelOptions { HttpHandler = handler });

        try
        {
            // Verifica se o serviço está acessível
            EditorUtility.DisplayProgressBar("Ommo Pairing", "A ligar ao serviço...", 0.1f);
            var testClient = new Ommo.CoreService.CoreServiceClient(channel);
            try
            {
                testClient.GetTrackingDevices(
                    new Ommo.TrackingDevicesRequest(),
                    new Grpc.Core.CallOptions(deadline: System.DateTime.UtcNow.AddSeconds(3)));
            }
            catch (Grpc.Core.RpcException e)
                when (e.Status.StatusCode == Grpc.Core.StatusCode.Unavailable)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Ommo Pairing — Erro",
                    "Não foi possível ligar ao serviço Ommo em localhost:50051.\n\n" +
                    "Solução: entra em Play Mode primeiro para que o serviço seja iniciado automaticamente.",
                    "OK");
                return;
            }

            // Abre o stream bidirecional de wireless management
            var client = new Ommo.CoreService.CoreServiceClient(channel);
            using var stream = client.OpenWirelessManagementStream();

            // Passo 1: ativa o modo de pairing
            await stream.RequestStream.WriteAsync(new Ommo.WirelessManagementRequest
            {
                RequestType = Ommo.WirelessManagementRequestType.WirelessManagementRequestEnablePairingMode
            });

            Debug.Log("[OmmoPairing] Modo de pairing ativado — aguarda SIU (30s)...");
            EditorUtility.DisplayProgressBar("Ommo Pairing",
                "Modo de pairing ativo. Pressiona o botão no SIU...", 0.4f);

            // Passo 2: aguarda evento de pairing request (timeout 30s)
            using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(30));
            uint siuUuid = 0;

            while (await stream.ResponseStream.MoveNext(cts.Token))
            {
                var evt = stream.ResponseStream.Current;
                Debug.Log($"[OmmoPairing] Evento: {evt.EventType} | UUIDs: [{string.Join(", ", evt.SiuUuids)}]");

                if (evt.EventType == Ommo.WirelessManagementEventType.WirelessManagementEventPairingRequest
                    && evt.SiuUuids.Count > 0)
                {
                    siuUuid = evt.SiuUuids[0];
                    Debug.Log($"[OmmoPairing] SIU detetado: {siuUuid} — a aprovar...");
                    EditorUtility.DisplayProgressBar("Ommo Pairing", $"SIU {siuUuid} detetado — a aprovar...", 0.7f);
                    break;
                }

                if (evt.EventType == Ommo.WirelessManagementEventType.WirelessManagementEventPairingTimeout)
                {
                    Debug.LogWarning("[OmmoPairing] Timeout do servidor — nenhum SIU detetado.");
                    break;
                }
            }

            EditorUtility.ClearProgressBar();

            if (siuUuid != 0)
            {
                // Passo 3: aprova o pairing
                await stream.RequestStream.WriteAsync(new Ommo.WirelessManagementRequest
                {
                    RequestType = Ommo.WirelessManagementRequestType.WirelessManagementRequestApprovePairing,
                    SiuUuid     = siuUuid
                });

                // Aguarda confirmação (REQUEST_ACK)
                using var ackCts = new CancellationTokenSource(System.TimeSpan.FromSeconds(5));
                while (await stream.ResponseStream.MoveNext(ackCts.Token))
                {
                    var evt = stream.ResponseStream.Current;
                    Debug.Log($"[OmmoPairing] Resposta à aprovação: {evt.EventType}");
                    if (evt.EventType == Ommo.WirelessManagementEventType.WirelessManagementEventRequestAck ||
                        evt.EventType == Ommo.WirelessManagementEventType.WirelessManagementEventPairingApprovedList)
                        break;
                }

                Debug.Log($"[OmmoPairing] ✅ SIU {siuUuid} emparelhado com sucesso!");
                EditorUtility.DisplayDialog("Ommo Pairing — Concluído",
                    $"✅ Emparelhamento concluído!\n\n" +
                    $"SIU UUID: {siuUuid}\n\n" +
                    "O sensor será reconhecido automaticamente em todas as sessões futuras.\n" +
                    "Entra em Play Mode — o SIU deve aparecer como 3º dispositivo no hardware panel.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Ommo Pairing — Sem SIU",
                    "Nenhum SIU detetado em 30 segundos.\n\n" +
                    "Verifica que:\n" +
                    "• O SIU está carregado (LED aceso)\n" +
                    "• Premiste o botão no SIU durante o pairing\n" +
                    "• O SIU está a menos de 1m do Wireless Receiver",
                    "OK");
            }

            // Passo 4: desativa o modo de pairing e fecha o stream
            try
            {
                await stream.RequestStream.WriteAsync(new Ommo.WirelessManagementRequest
                {
                    RequestType = Ommo.WirelessManagementRequestType.WirelessManagementRequestDisablePairingMode
                });
                await stream.RequestStream.CompleteAsync();
            }
            catch { /* stream pode já ter fechado */ }
        }
        catch (System.OperationCanceledException)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Ommo Pairing — Timeout",
                "Timeout — nenhum SIU detetado em 30 segundos.\n\n" +
                "Pressiona o botão no SIU e tenta de novo.",
                "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[OmmoPairing] Erro: {e}");
            EditorUtility.DisplayDialog("Ommo Pairing — Erro",
                $"Erro durante o pairing:\n\n{e.Message}",
                "OK");
        }
        finally
        {
            await channel.ShutdownAsync();
        }
    }
}
#endif
