using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Controla a posição e rotação de um GameObject com base nos dados
/// recebidos em tempo real do sensor Ommo.
///
/// Configuração mínima na scene:
///   1. Um GameObject com OmmoServiceLauncher
///   2. Este script no GameObject que queres controlar (ou num gestor separado
///      com o campo "AlvoDoControlo" preenchido)
/// </summary>
public class OmmoObjectController : MonoBehaviour
{
    [Header("Alvo")]
    [Tooltip("Objeto a controlar. Se vazio usa o próprio GameObject.")]
    public Transform AlvoDoControlo;

    [Header("Sensor Ommo")]
    [Tooltip("UUID da SIU (deixar 0 para usar o primeiro dispositivo encontrado).")]
    public uint SiuUuid = 0;
    [Tooltip("ID da porta do sensor a usar.")]
    public uint PortId = 0;
    [Tooltip("Índice do sensor dentro do dispositivo (normalmente 0).")]
    public int IndiceSensor = 0;
    public Ommo.DeviceFusionMode ModoFusao = Ommo.DeviceFusionMode.Default;

    [Header("O que controlar")]
    public bool ControlarPosicao = true;
    public bool ControlarRotacao = true;

    [Header("Posição")]
    [Tooltip("Escala: quantos centímetros correspondem a 1 unidade Unity.")]
    public float EscalaCM = 10f;
    [Tooltip("Offset aplicado à posição (posição de repouso / origem).")]
    public Vector3 OffsetPosicao = Vector3.zero;
    [Tooltip("Suavização de posição. 0 = sem suavização, valores altos = mais suave.")]
    [Range(0f, 20f)]
    public float SuavizacaoPosicao = 8f;

    [Header("Rotação")]
    [Tooltip("Suavização de rotação. 0 = sem suavização.")]
    [Range(0f, 20f)]
    public float SuavizacaoRotacao = 8f;

    [Header("Calibração")]
    [Tooltip("Guarda a posição atual do sensor como ponto de origem ao pressionar.")]
    public KeyCode TeclaCalibracao = KeyCode.Space;

    // Dados recebidos da thread de background (protegidos por lock)
    private Vector3 _posicaoSensor;
    private Quaternion _rotacaoSensor;
    private bool _temDados = false;
    private readonly object _lock = new object();

    // Valores que o Update aplica ao alvo (main thread)
    private Vector3 _posicaoAlvo;
    private Quaternion _rotacaoAlvo;

    // Calibração
    private Vector3 _origemCalibracao = Vector3.zero;
    private bool _calibrado = false;

    private Task _streamTask;
    private CancellationTokenSource _cts;
    private Ommo.Client _client;

    void Awake()
    {
        if (AlvoDoControlo == null)
            AlvoDoControlo = transform;
    }

    void Start()
    {
        _posicaoAlvo = AlvoDoControlo.position;
        _rotacaoAlvo = AlvoDoControlo.rotation;

        if (OmmoServiceLauncher.ServiceReady)
            IniciarStream();
        else
            OmmoServiceLauncher.OnServiceReady += IniciarStream;
    }

    void Update()
    {
        // Calibração manual por tecla
        if (Input.GetKeyDown(TeclaCalibracao))
            Calibrar();

        if (!_temDados) return;

        Vector3 posicaoLida;
        Quaternion rotacaoLida;

        lock (_lock)
        {
            posicaoLida = _posicaoSensor;
            rotacaoLida = _rotacaoSensor;
        }

        if (ControlarPosicao)
        {
            Vector3 destino = OffsetPosicao + (posicaoLida - _origemCalibracao);
            _posicaoAlvo = SuavizacaoPosicao > 0f
                ? Vector3.Lerp(_posicaoAlvo, destino, Time.deltaTime * SuavizacaoPosicao)
                : destino;
            AlvoDoControlo.position = _posicaoAlvo;
        }

        if (ControlarRotacao)
        {
            _rotacaoAlvo = SuavizacaoRotacao > 0f
                ? Quaternion.Slerp(_rotacaoAlvo, rotacaoLida, Time.deltaTime * SuavizacaoRotacao)
                : rotacaoLida;
            AlvoDoControlo.rotation = _rotacaoAlvo;
        }
    }

    /// <summary>
    /// Define a posição atual do sensor como ponto de origem (zeragem).
    /// Pode ser chamado por código (ex: no início de cada exercício).
    /// </summary>
    public void Calibrar()
    {
        lock (_lock)
        {
            _origemCalibracao = _posicaoSensor;
        }
        _calibrado = true;
        Debug.Log("[OmmoObjectController] Calibrado na posição: " + _origemCalibracao);
    }

    // ─── Dados do sensor ────────────────────────────────────────────────────

    private void IniciarStream()
    {
        OmmoServiceLauncher.OnServiceReady -= IniciarStream;

        _client = Ommo.Client.GetClient();
        _cts = new CancellationTokenSource();

        var request = new Ommo.TrackingDeviceDataStreamRequest
        {
            FieldMask = Ommo.RawDataFieldMask.OMMO_SIU_UUID
                      | Ommo.RawDataFieldMask.OMMO_PORT_ID
                      | Ommo.RawDataFieldMask.OMMO_DEVICE_TYPE,
            SiuUuid = SiuUuid,
            PortId = PortId,
            ReportInterval = 0,
            BufferDepth = 5,
            RequestedFusionMode = ModoFusao
        };

        _streamTask = Task.Factory.StartNew(async () =>
        {
            try
            {
                await _client.StreamTrackingDeviceData(request, _cts.Token, ProcessarDados);
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception ex)
            {
                Debug.LogError("[OmmoObjectController] Erro no stream: " + ex.Message);
            }
        }, TaskCreationOptions.LongRunning);

        Debug.Log($"[OmmoObjectController] Stream iniciado → SIU:{SiuUuid} Porta:{PortId}");
    }

    private void ProcessarDados(Ommo.TrackingDeviceData dados)
    {
        if (dados.Positions.Count <= IndiceSensor || dados.Quaternions.Count <= IndiceSensor)
            return;

        // Conversão do sistema de coordenadas Ommo → Unity (igual ao OmmoDevice.cs)
        var p = dados.Positions[IndiceSensor];
        var q = dados.Quaternions[IndiceSensor];

        Vector3 posicao = new Vector3(p.X, p.Z, p.Y) / EscalaCM;
        Quaternion rotacao = Quaternion.Inverse(new Quaternion(q.X, q.Z, q.Y, q.W));

        lock (_lock)
        {
            _posicaoSensor = posicao;
            _rotacaoSensor = rotacao;
            _temDados = true;
        }

        // Calibração automática no primeiro pacote recebido
        if (!_calibrado)
        {
            _origemCalibracao = posicao;
            _calibrado = true;
        }
    }

    // ─── Ciclo de vida ──────────────────────────────────────────────────────

    void OnDestroy()
    {
        OmmoServiceLauncher.OnServiceReady -= IniciarStream;
        _cts?.Cancel();
        try { _streamTask?.Wait(1000); } catch (System.OperationCanceledException) { }
        _cts?.Dispose();
    }

    // ─── Gizmos de debug ────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(AlvoDoControlo != null ? AlvoDoControlo.position : transform.position, 0.1f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(OffsetPosicao, 0.05f);
    }
}
