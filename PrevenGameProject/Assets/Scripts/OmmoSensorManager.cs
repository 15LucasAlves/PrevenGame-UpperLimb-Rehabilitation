using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

/// <summary>
/// OmmoSensorManager — Gestor de hardware para o jogo PrevenGame.
///
/// Fluxo de inicialização:
///   1. App inicia → PainelALigar oculto; serviço Ommo lança em background
///   2. OmmoCalibracaoManager chama IniciarTracking(n) após seleção de modo
///   3. PainelALigar aparece com "A aguardar N sensor(es)..."
///   4. Motor da Base Station arranca (imediato se já detetada, ou no próximo poll)
///   5. Quando N sensores conectados → PainelALigar esconde-se automaticamente
/// </summary>
public class OmmoSensorManager : MonoBehaviour
{
    [Header("Referências de Hardware")]
    public OmmoDeviceManager DeviceManager;
    public OmmoHardwareMonitor HardwareMonitor;

    [Header("UI de Ligação (opcional)")]
    [Tooltip("Canvas exibido enquanto os sensores não estão prontos. Pode ser null.")]
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

    /// <summary>Emitido quando o número de sensores conectados muda.</summary>
    public event System.Action<int> OnNumeroDeSensoresMudou;

    // ── Privado ───────────────────────────────────────────────────────

    private bool _trackingIniciado  = false;
    private bool _motorEmInicio     = false;
    private bool _trackerSolicitado = false;
    private int  _sensoresNecessarios = 0; // definido pelo modo selecionado

    // ── Unity ─────────────────────────────────────────────────────────

    void Start()
    {
        // Painel oculto até o jogador selecionar o modo
        if (PainelALigar) PainelALigar.gameObject.SetActive(false);

        OmmoServiceLauncher.OnServiceReady += AoServicoPronto;

        if (HardwareMonitor)
            HardwareMonitor.OnHardwareUpdated += AoHardwareAtualizado;
    }

    void Update()
    {
        var devices = System.Array.FindAll(
            FindObjectsOfType<OmmoDevice>(),
            d => d.gameObject.activeInHierarchy);

        int contagem = devices.Length;
        if (contagem != NumeroSensores)
        {
            NumeroSensores     = contagem;
            SensoresConectados = contagem > 0;
            OnNumeroDeSensoresMudou?.Invoke(contagem);

            // Esconde o painel de espera quando o número de sensores esperados está ligado
            if (_sensoresNecessarios > 0 && contagem >= _sensoresNecessarios)
                OcultarPainelALigar();
            else if (_trackingIniciado && contagem < _sensoresNecessarios)
                MostrarPainelALigar(MensagemEspera());
        }
    }

    void OnDestroy()
    {
        OmmoServiceLauncher.OnServiceReady -= AoServicoPronto;

        if (HardwareMonitor)
            HardwareMonitor.OnHardwareUpdated -= AoHardwareAtualizado;

        PararHardware();
    }

    void OnApplicationQuit() => PararHardware();

    // ── Hardware ──────────────────────────────────────────────────────

    void AoServicoPronto()
    {
        OmmoServiceLauncher.OnServiceReady -= AoServicoPronto;
        Debug.Log("[OmmoSensorManager] Serviço pronto.");

        if (_trackerSolicitado)
            IniciarTrackingInterno();
    }

    /// <summary>
    /// Inicia o tracking após o jogador selecionar o modo.
    /// numSensores: 1 ou 2 — quantos sensores se espera receber.
    /// </summary>
    public void IniciarTracking(int numSensores = 1)
    {
        if (_trackingIniciado) return;

        _sensoresNecessarios = numSensores;
        MostrarPainelALigar(MensagemEspera());

        if (OmmoServiceLauncher.ServiceReady)
            IniciarTrackingInterno();
        else
            _trackerSolicitado = true;
    }

    void IniciarTrackingInterno()
    {
        if (_trackingIniciado) return;
        _trackingIniciado  = true;
        _trackerSolicitado = false;

        Debug.Log($"[OmmoSensorManager] A iniciar tracking ({_sensoresNecessarios} sensor(es))...");

        if (DeviceManager)
            DeviceManager.StartTracking();

        // Se a base station já está detetada, arranca o motor imediatamente
        // sem esperar pelo próximo poll do HardwareMonitor (1.5s)
        if (HardwareMonitor != null && !_motorEmInicio)
        {
            foreach (var d in HardwareMonitor.CurrentInfo.Connected)
            {
                if (d.IsBaseStation && !d.IsRunning)
                {
                    _motorEmInicio = true;
                    Debug.Log("[OmmoSensorManager] Base Station já detetada — a iniciar motor...");
                    IniciarMotorBaseStation(d.UUID);
                    break;
                }
            }
        }
    }

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

        foreach (var d in HardwareMonitor.CurrentInfo.Connected)
        {
            if (!d.IsBaseStation) continue;
            string uuid = d.UUID;
            Task.Run(() =>
            {
                try
                {
                    var handler = new Cysharp.Net.Http.YetAnotherHttpHandler { Http2Only = true };
                    var channel = Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost:50051",
                                      new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = handler });
                    var client  = new Ommo.CoreService.CoreServiceClient(channel);
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
        // Inicia motor quando tracking estiver ativo e base station ainda não a girar
        if (_trackingIniciado && !_motorEmInicio)
        {
            foreach (var d in info.Connected)
            {
                if (d.IsBaseStation && !d.IsRunning)
                {
                    _motorEmInicio = true;
                    Debug.Log("[OmmoSensorManager] Base Station detetada (poll) — a iniciar motor...");
                    IniciarMotorBaseStation(d.UUID);
                    break;
                }
            }
        }
    }

    void IniciarMotorBaseStation(string uuid)
    {
        Task.Run(() =>
        {
            System.Threading.Thread.Sleep(1000);
            try
            {
                var handler = new Cysharp.Net.Http.YetAnotherHttpHandler { Http2Only = true };
                var channel = Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost:50051",
                                  new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = handler });
                var client  = new Ommo.CoreService.CoreServiceClient(channel);
                bool ok = client.SetBaseStationMotorRunning(true);
                channel.ShutdownAsync().Wait();

                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    _motorEmInicio = false;
                    if (ok)
                    {
                        Debug.Log("[OmmoSensorManager] ✅ Motor iniciado.");
                        HardwareMonitor?.SetMotorRunningState(uuid, true);
                    }
                    else
                    {
                        Debug.LogWarning("[OmmoSensorManager] Motor recusou — nova tentativa em ~1.5s...");
                    }
                });
            }
            catch (System.Exception e)
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    _motorEmInicio = false;
                    Debug.LogWarning("[OmmoSensorManager] Erro ao iniciar motor: " + e.Message);
                });
            }
        });
    }

    // ── UI ────────────────────────────────────────────────────────────

    string MensagemEspera()
        => _sensoresNecessarios == 1
            ? "A aguardar 1 sensor..."
            : "A aguardar 2 sensores...";

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
