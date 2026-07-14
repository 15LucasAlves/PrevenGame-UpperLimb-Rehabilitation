using UnityEngine;
using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Net.Http;
using Grpc.Net.Client;

/// <summary>
/// OmmoAutoPairing — Emparelhamento automático do SIU em runtime.
///
/// Enquanto o jogo espera por sensores (<see cref="OmmoSensorManager"/> chama
/// <see cref="Iniciar"/>), mantém o modo de pairing do serviço Ommo ativo: quando o
/// jogador prime o botão do SIU, o serviço emite um PairingRequest e nós aprovamos
/// automaticamente. O modo expira no serviço (~30 s) — é re-armado a cada timeout,
/// funcionando como "ping" contínuo até o sensor ligar.
///
/// Segue a mesma sequência de RPCs do OmmoPairingHelper (menu de editor Ommo →
/// Emparelhar SIU), que continua disponível para emparelhar manualmente.
/// </summary>
public class OmmoAutoPairing : MonoBehaviour
{
    /// <summary>Emitido (no main thread) quando um SIU é aprovado com sucesso.</summary>
    public event Action<uint> OnSiuEmparelhado;

    /// <summary>True enquanto o ciclo de emparelhamento está a correr.</summary>
    public bool Ativo { get; private set; }

    [Tooltip("Espera entre tentativas quando o stream falha (segundos).")]
    public float EsperaAposErro = 5f;

    private CancellationTokenSource _cts;
    private uint _uuidPorConfirmar;   // SIU aprovado à espera do RequestAck

    // ── Ciclo de vida ─────────────────────────────────────────────────
    public void Iniciar()
    {
        if (Ativo) return;
        Ativo = true;
        _cts  = new CancellationTokenSource();
        var token = _cts.Token;
        Task.Factory.StartNew(() => CicloPairing(token), token,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Debug.Log("[AutoPairing] Iniciado — o botão do SIU emparelha automaticamente.");
    }

    public void Parar()
    {
        if (!Ativo) return;
        Ativo = false;
        _cts?.Cancel();
        _cts = null;
        Debug.Log("[AutoPairing] Parado.");
    }

    void OnDestroy() => Parar();

    // ── Ciclo de emparelhamento (thread própria) ──────────────────────
    async Task CicloPairing(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var handler = new YetAnotherHttpHandler { Http2Only = true };
            var channel = GrpcChannel.ForAddress("http://localhost:50051",
                new GrpcChannelOptions { HttpHandler = handler });
            try
            {
                // Espera o serviço estar pronto antes de abrir o stream.
                while (!OmmoServiceLauncher.ServiceReady && !token.IsCancellationRequested)
                    await Task.Delay(500, token);
                if (token.IsCancellationRequested) break;

                var client = new Ommo.CoreService.CoreServiceClient(channel);
                using var stream = client.OpenWirelessManagementStream();

                await ArmarPairing(stream);
                Debug.Log("[AutoPairing] Modo de emparelhamento ativo — prime o botão do SIU.");

                while (await stream.ResponseStream.MoveNext(token))
                {
                    var evt = stream.ResponseStream.Current;
                    switch (evt.EventType)
                    {
                        case Ommo.WirelessManagementEventType.WirelessManagementEventPairingRequest
                            when evt.SiuUuids.Count > 0:
                        {
                            _uuidPorConfirmar = evt.SiuUuids[0];
                            Debug.Log($"[AutoPairing] SIU {_uuidPorConfirmar} pediu emparelhamento — a aprovar...");
                            await stream.RequestStream.WriteAsync(new Ommo.WirelessManagementRequest
                            {
                                RequestType = Ommo.WirelessManagementRequestType.WirelessManagementRequestApprovePairing,
                                SiuUuid     = _uuidPorConfirmar,
                            });
                            break;
                        }

                        case Ommo.WirelessManagementEventType.WirelessManagementEventRequestAck:
                        case Ommo.WirelessManagementEventType.WirelessManagementEventPairingApprovedList:
                            if (_uuidPorConfirmar != 0)
                            {
                                uint uuid = _uuidPorConfirmar;
                                _uuidPorConfirmar = 0;
                                Debug.Log($"[AutoPairing] ✅ SIU {uuid} emparelhado.");
                                UnityMainThreadDispatcher.Enqueue(() => OnSiuEmparelhado?.Invoke(uuid));
                                // Re-arma: o modo pode ter caído com a aprovação e outros
                                // SIUs podem ainda querer emparelhar.
                                await ArmarPairing(stream);
                            }
                            break;

                        case Ommo.WirelessManagementEventType.WirelessManagementEventPairingTimeout:
                            // O serviço expira o modo (~30 s) — re-arma enquanto esperamos.
                            Debug.Log("[AutoPairing] Timeout do modo de emparelhamento — a re-armar...");
                            await ArmarPairing(stream);
                            break;
                    }
                }
            }
            catch (OperationCanceledException) { /* Parar() — sai abaixo */ }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                    Debug.LogWarning($"[AutoPairing] Stream falhou ({e.Message}) — nova tentativa em {EsperaAposErro:F0}s.");
            }
            finally
            {
                try { await channel.ShutdownAsync(); } catch { }
            }

            if (token.IsCancellationRequested) break;
            try { await Task.Delay(TimeSpan.FromSeconds(EsperaAposErro), token); }
            catch (OperationCanceledException) { break; }
        }

        await DesativarPairing();
    }

    static Task ArmarPairing(Grpc.Core.AsyncDuplexStreamingCall<Ommo.WirelessManagementRequest, Ommo.WirelessManagementEvent> stream)
        => stream.RequestStream.WriteAsync(new Ommo.WirelessManagementRequest
        {
            RequestType = Ommo.WirelessManagementRequestType.WirelessManagementRequestEnablePairingMode
        });

    /// <summary>Best-effort: ao terminar o ciclo, desliga o modo de pairing no serviço.</summary>
    static async Task DesativarPairing()
    {
        try
        {
            var handler = new YetAnotherHttpHandler { Http2Only = true };
            var channel = GrpcChannel.ForAddress("http://localhost:50051",
                new GrpcChannelOptions { HttpHandler = handler });
            var client = new Ommo.CoreService.CoreServiceClient(channel);
            using var stream = client.OpenWirelessManagementStream();
            await stream.RequestStream.WriteAsync(new Ommo.WirelessManagementRequest
            {
                RequestType = Ommo.WirelessManagementRequestType.WirelessManagementRequestDisablePairingMode
            });
            await stream.RequestStream.CompleteAsync();
            await channel.ShutdownAsync();
        }
        catch { /* o serviço pode já ter fechado */ }
    }
}
