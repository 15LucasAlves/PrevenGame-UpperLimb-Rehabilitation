using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

/// <summary>
/// OmmoSensorManager — Gestor de hardware para o jogo PrevenGame.
///
/// Substitui OmmoUIManager nas scenes do jogo: não tem UI de diagnóstico,
/// inicia o tracking automaticamente quando o serviço Ommo fica pronto,
/// e garante que o motor da Base Station é parado em segurança ao sair.
///
/// Expõe propriedades e eventos para o jogo reagir ao estado dos sensores.
/// </summary>
public class OmmoSensorManager : MonoBehaviour
{
    [Header("Referências de Hardware")]
    public OmmoDeviceManager DeviceManager;
    public OmmoHardwareMonitor HardwareMonitor;

    [Header("UI de Ligação (opcional)")]
    [Tooltip("Canvas exibido enquanto o serviço Ommo não está pronto. Pode ser null.")]
    public Canvas PainelALigar;
    [Tooltip("Texto de estado dentro do PainelALigar. Pode ser null.")]
    public TextMeshProUGUI TextoEstado;

    // ── Estado público ────────────────────────────────────────────────

    /// <summary>True quando pelo menos um sensor está conectado e a fazer streaming.</summary>
    public bool SensoresConectados { get; private set; } = false;

    /// <summary>Número de sensores (OmmoDevice) activos em cena.</summary>
    public int NumeroSensores { get; private set; } = 0;

    /// <summary>True quando o serviço Ommo está pronto (gRPC acessível).</summary>
    public bool ServicoAtivo => OmmoServiceLauncher.ServiceReady;

    // ── Eventos para o jogo ───────────────────────────────────────────

    /// <summary>
    /// Emitido quando o número de sensores conectados muda.
    /// Parâmetro: novo número de sensores.
    /// </summary>
    public event System.Action<int> OnNumeroDeSensoresMudou;

    // ── Privado ───────────────────────────────────────────────────────

    private bool _trackingIniciado = false;
    private bool _motorEmInicio    = false; // evita chamar IniciarMotorBaseStation duas vezes

    // ── Unity ─────────────────────────────────────────────────────────

    void Start()
    {
        MostrarPainelALigar("A iniciar serviço Ommo...");

        if (OmmoServiceLauncher.ServiceReady)
            IniciarTracking();
        else
            OmmoServiceLauncher.OnServiceReady += IniciarTracking;

        if (HardwareMonitor)
            HardwareMonitor.OnHardwareUpdated += AoHardwareAtualizado;
    }

    void Update()
    {
        // Atualiza contagem de sensores activos em tempo real
        var devices = System.Array.FindAll(
            FindObjectsOfType<OmmoDevice>(),
            d => d.gameObject.activeInHierarchy);

        int contagem = devices.Length;
        if (contagem != NumeroSensores)
        {
            NumeroSensores     = contagem;
            SensoresConectados = contagem > 0;
            OnNumeroDeSensoresMudou?.Invoke(contagem);
        }
    }

    void OnDestroy()
    {
        OmmoServiceLauncher.OnServiceReady -= IniciarTracking;

        if (HardwareMonitor)
            HardwareMonitor.OnHardwareUpdated -= AoHardwareAtualizado;

        PararHardware();
    }

    void OnApplicationQuit()
    {
        PararHardware();
    }

    // ── Hardware ──────────────────────────────────────────────────────

    void IniciarTracking()
    {
        OmmoServiceLauncher.OnServiceReady -= IniciarTracking;

        if (_trackingIniciado) return;
        _trackingIniciado = true;

        Debug.Log("[OmmoSensorManager] Serviço pronto — a iniciar tracking...");
        OcultarPainelALigar();

        if (DeviceManager)
            DeviceManager.StartTracking();
    }

    /// <summary>
    /// Para o motor da Base Station via gRPC e o tracking de dispositivos.
    /// Chamado automaticamente em OnDestroy e OnApplicationQuit.
    /// Pode também ser chamado manualmente (ex: ao terminar uma sessão de jogo).
    /// </summary>
    public void PararHardware()
    {
        PararMotorBaseStation();

        if (DeviceManager)
            DeviceManager.StopTracking();

        _trackingIniciado = false;
        _motorEmInicio    = false;
    }

    void PararMotorBaseStation()
    {
        if (HardwareMonitor == null) return;

        foreach (var dispositivo in HardwareMonitor.CurrentInfo.Connected)
        {
            if (!dispositivo.IsBaseStation) continue;

            string uuid = dispositivo.UUID;
            Task.Run(() =>
            {
                try
                {
                    var handler = new Cysharp.Net.Http.YetAnotherHttpHandler { Http2Only = true };
                    var channel = Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost:50051",
                                      new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = handler });
                    var client = new Ommo.CoreService.CoreServiceClient(channel);
                    client.SetBaseStationMotorRunning(false);
                    channel.ShutdownAsync().Wait();

                    UnityMainThreadDispatcher.Enqueue(() =>
                        HardwareMonitor?.SetMotorRunningState(uuid, false));
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[OmmoSensorManager] Erro ao parar motor: " + e.Message);
                }
            });
        }
    }

    // ── Eventos de hardware ───────────────────────────────────────────

    void AoHardwareAtualizado(OmmoHardwareMonitor.ServiceInfo info)
    {
        // Actualiza o texto do painel de ligação se ainda estiver visível
        if (PainelALigar != null && PainelALigar.gameObject.activeSelf && TextoEstado != null)
        {
            TextoEstado.text = info.IsConnected
                ? "Hardware conectado. A aguardar sensores..."
                : "A aguardar serviço Ommo...";
        }

        // Inicia o motor da Base Station automaticamente quando o tracking está ativo
        // e a base station aparece conectada mas ainda não a girar.
        // _motorEmInicio evita chamadas duplicadas entre polls (a cada 1.5s).
        if (_trackingIniciado && !_motorEmInicio)
        {
            foreach (var dispositivo in info.Connected)
            {
                if (dispositivo.IsBaseStation && !dispositivo.IsRunning)
                {
                    _motorEmInicio = true;
                    Debug.Log("[OmmoSensorManager] Base Station detetada — a iniciar motor automaticamente...");
                    IniciarMotorBaseStation(dispositivo.UUID);
                    break; // há sempre no máximo uma Base Station
                }
            }
        }
    }

    void IniciarMotorBaseStation(string uuid)
    {
        Task.Run(() =>
        {
            // Aguarda 1s antes de tentar — garante que a Base Station
            // já completou a inicialização interna após o serviço arrancar.
            System.Threading.Thread.Sleep(1000);

            try
            {
                var handler = new Cysharp.Net.Http.YetAnotherHttpHandler { Http2Only = true };
                var channel = Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost:50051",
                                  new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = handler });
                var client = new Ommo.CoreService.CoreServiceClient(channel);
                bool ok = client.SetBaseStationMotorRunning(true);
                channel.ShutdownAsync().Wait();

                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    _motorEmInicio = false; // liberta para nova tentativa se ok=false

                    if (ok)
                    {
                        Debug.Log("[OmmoSensorManager] ✅ Motor da Base Station iniciado — posições dos sensores a caminho.");
                        HardwareMonitor?.SetMotorRunningState(uuid, true); // marca como running → evita novas tentativas
                    }
                    else
                    {
                        // ok=false: servidor recusou (Base Station ainda não pronta).
                        // NÃO actualizar estado local → próximo poll (1.5s) vai tentar de novo.
                        Debug.LogWarning("[OmmoSensorManager] Motor recusou arranque (success=False) — nova tentativa em ~1.5s...");
                    }
                });
            }
            catch (System.Exception e)
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    _motorEmInicio = false; // liberta para nova tentativa
                    Debug.LogWarning("[OmmoSensorManager] Erro ao iniciar motor: " + e.Message);
                });
            }
        });
    }

    // ── UI de ligação (mínima, opcional) ─────────────────────────────

    void MostrarPainelALigar(string mensagem)
    {
        if (PainelALigar) PainelALigar.gameObject.SetActive(true);
        if (TextoEstado)  TextoEstado.text = mensagem;
    }

    void OcultarPainelALigar()
    {
        if (PainelALigar) PainelALigar.gameObject.SetActive(false);
    }
}
