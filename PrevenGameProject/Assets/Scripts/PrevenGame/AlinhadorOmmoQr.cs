using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// AlinhadorOmmoQr — Ancora o espaço de tracking Ommo no mundo VR através do
/// QR code colado na base station.
///
/// Como funciona:
///   1. O MRUK deteta o QR (trackable) e dá a sua pose no mundo Unity/VR.
///   2. A pose da ORIGEM Ommo = pose do QR ∘ offset físico QR→origem (medido uma
///      vez na base station real e definido no Inspector).
///   3. Depois de N leituras consecutivas estáveis, a pose é aplicada ao
///      <see cref="OmmoRoot"/> (o BaseStation da cena — referencial dos
///      <see cref="OmmoDevice"/>s) e CONGELADA (a base não se move durante a
///      sessão), e persiste no <see cref="SessionManager"/> para as cenas
///      seguintes a aplicarem sem re-detetar.
///   4. Se o QR voltar a ser detetado com desvio grande (base foi movida),
///      re-alinha automaticamente.
///
/// Plano B (sem QR/Link a detetar): <see cref="AlinharManualFrenteCabeca"/> —
/// o operador posiciona o jogador de frente para a base station a ~DistanciaManual
/// e prime F2; alinhamento grosseiro mas funcional (tecla tratada aqui).
/// </summary>
public class AlinhadorOmmoQr : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Transform da origem Ommo na cena (BaseStation). Se vazio, procura o OmmoDeviceManager.")]
    public Transform OmmoRoot;

    [Header("QR → origem Ommo (medido na base station física)")]
    [Tooltip("Posição da origem Ommo no referencial do QR (metros). Ex.: QR colado no topo da base, origem no centro da base.")]
    public Vector3 OffsetPosicao = Vector3.zero;
    [Tooltip("Rotação (euler, graus) da origem Ommo no referencial do QR.")]
    public Vector3 OffsetRotacaoEuler = Vector3.zero;

    [Header("Estabilização")]
    [Tooltip("Leituras consecutivas estáveis necessárias antes de congelar o alinhamento.")]
    public int LeiturasEstaveis = 20;
    [Tooltip("Jitter máximo entre leituras para contarem como estáveis (metros).")]
    public float LimiarJitter = 0.01f;
    [Tooltip("Desvio (metros) a partir do qual um QR re-detetado força um re-alinhamento.")]
    public float LimiarRealinhar = 0.05f;
    [Tooltip("Filtrar por payload do QR (vazio = aceita qualquer QR).")]
    public string PayloadFiltro = "";

    [Header("Plano B — alinhamento manual (F2)")]
    [Tooltip("Distância assumida da cabeça à base station no alinhamento manual (metros).")]
    public float DistanciaManual = 0.6f;
    [Tooltip("Altura assumida da base station acima do chão no alinhamento manual (metros).")]
    public float AlturaManual = 0.75f;

    [Header("Convenção de eixos do Ommo")]
    [Tooltip("Yaw extra (graus) aplicado à origem Ommo em TODOS os alinhamentos (QR e manual). " +
             "Corrige a convenção de eixos do hardware — observado: a frente física da base " +
             "fica 90° à direita do que os dados assumem.")]
    public float CorrecaoYawOmmoGraus = 90f;

    [Header("Visual de debug")]
    [Tooltip("Caixa laranja no sítio onde o jogo pensa que a base station está (com marcador de frente). " +
             "Move-se com o alinhamento — deve sobrepor-se à base física real.")]
    public bool MostrarVisualBase = true;

    /// <summary>De onde veio o alinhamento atual (para logs/diagnóstico).</summary>
    public enum Fonte { Nenhum, Persistido, Qr, Manual }

    /// <summary>True depois de o alinhamento ter sido aplicado ao OmmoRoot nesta cena.</summary>
    public bool Alinhado { get; private set; }

    /// <summary>Fonte do alinhamento atual.</summary>
    public Fonte FonteAtual { get; private set; } = Fonte.Nenhum;

    private bool _subscrito;
    private bool _avisoMrukDado;
    private float _tempoInicioEspera = -1f;
    private float _proximoLogEstado;
    private float _proximoWatchdog = -1f;
    private int _leiturasOk;
    private Pose _ultimaLeitura;
    private bool _temUltimaLeitura;
    private readonly List<MRUKTrackable> _trackables = new List<MRUKTrackable>();

    void Start()
    {
        if (OmmoRoot == null)
        {
            var dm = FindObjectOfType<OmmoDeviceManager>();
            if (dm != null && dm.BaseStation != null) OmmoRoot = dm.BaseStation.transform;
        }

        // Alinhamento persistido de uma cena anterior — aplica já, sem esperar pelo QR.
        var sm = SessionManager.Instancia;
        if (sm != null && sm.AlinhamentoValido)
        {
            AplicarPose(sm.AlinhamentoOmmo, persistir: false, Fonte.Persistido);
            Debug.Log("[AlinhadorQr] Alinhamento persistido aplicado ao OmmoRoot.");
        }

        CriarVisualBase();
    }

    /// <summary>
    /// Caixa de debug presa ao OmmoRoot: mostra onde o jogo pensa que a base
    /// station está (acompanha o alinhamento automaticamente por ser filha).
    /// A caixa laranja é o corpo; o cubo pequeno marca o +Z (frente) da origem.
    /// </summary>
    void CriarVisualBase()
    {
        if (!MostrarVisualBase || OmmoRoot == null) return;

        var corpo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        corpo.name = "VisualBaseStation";
        Object.Destroy(corpo.GetComponent<Collider>());
        corpo.transform.SetParent(OmmoRoot, false);
        corpo.transform.localPosition = Vector3.zero;
        corpo.transform.localScale    = new Vector3(0.28f, 0.05f, 0.28f);
        corpo.GetComponent<Renderer>().material.color = new Color(1f, 0.55f, 0.1f);

        var frente = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frente.name = "VisualBaseFrente";
        Object.Destroy(frente.GetComponent<Collider>());
        frente.transform.SetParent(OmmoRoot, false);
        frente.transform.localPosition = new Vector3(0f, 0f, 0.18f);
        frente.transform.localScale    = new Vector3(0.03f, 0.03f, 0.08f);
        frente.GetComponent<Renderer>().material.color = Color.white;
    }

    void Update()
    {
        // Plano B: alinhamento manual com F2 (operador).
        if (Input.GetKeyDown(KeyCode.F2)) AlinharManualFrenteCabeca();

        if (MRUK.Instance == null)
        {
            // Diagnóstico: se o VR está ativo mas o MRUK nunca aparece (>5 s),
            // algo falhou na ativação — avisa uma vez em vez de esperar em silêncio.
            if (!_avisoMrukDado && GestorXR.Instancia != null && GestorXR.Instancia.VrAtivo)
            {
                if (_tempoInicioEspera < 0f) _tempoInicioEspera = Time.unscaledTime;
                else if (Time.unscaledTime - _tempoInicioEspera > 5f)
                {
                    _avisoMrukDado = true;
                    Debug.LogWarning("[AlinhadorQr] VR ativo mas MRUK.Instance continua null após 5 s — " +
                                     "o MRUK está na cena e foi ativado pelo GestorXR? Usa F2 (alinhamento manual) para desbloquear.");
                }
            }
            return;
        }
        if (!_subscrito)
        {
            _subscrito = true;
            _proximoWatchdog = Time.unscaledTime + 8f; // dá tempo ao caminho normal
            if (!MRUK.Instance.QRCodeTrackingSupported)
                Debug.LogWarning("[AlinhadorQr] QR tracking NÃO suportado neste runtime — usa F2 (manual).");
        }

        WatchdogConfigTracker();
        LogEstadoPeriodico();
        ProcurarQrEstavel();
    }

    /// <summary>
    /// Watchdog da configuração do tracker: se o runtime recusou o pedido de QR
    /// (config ativa=False — ex.: o MRUK foi ativado antes de a sessão ter foco),
    /// o MRUK NUNCA re-tenta sozinho — só tenta quando o pedido MUDA. Alternar a
    /// flag do teclado (mantendo QR=true) muda o pedido e força novas tentativas
    /// a cada ciclo, até a config ativa do QR ficar True.
    /// </summary>
    void WatchdogConfigTracker()
    {
        if (MRUK.Instance.TrackerConfiguration.QRCodeTrackingEnabled) return; // já ativa
        if (_proximoWatchdog < 0f || Time.unscaledTime < _proximoWatchdog) return;
        _proximoWatchdog = Time.unscaledTime + 5f;

        var cfg = MRUK.Instance.SceneSettings.TrackerConfiguration;
        cfg.QRCodeTrackingEnabled       = true;
        cfg.KeyboardTrackingEnabled     = !cfg.KeyboardTrackingEnabled; // muda o pedido → força retry
        MRUK.Instance.SceneSettings.TrackerConfiguration = cfg;
        Debug.Log("[AlinhadorQr] Watchdog: config ativa do QR ainda False — a forçar novo pedido ao runtime.");
    }

    /// <summary>
    /// Enquanto não há alinhamento, reporta a cada 2 s onde a cadeia está:
    /// suporte do runtime → configuração do tracker (pedida vs ativa) → QRs
    /// detetados (payload/tracked/posição) → progresso da estabilização.
    /// </summary>
    void LogEstadoPeriodico()
    {
        if (Alinhado && FonteAtual == Fonte.Qr) return; // nada a diagnosticar
        if (Time.unscaledTime < _proximoLogEstado) return;
        _proximoLogEstado = Time.unscaledTime + 2f;

        var sb = new System.Text.StringBuilder("[AlinhadorQr] Estado: ");
        sb.Append($"suportado={MRUK.Instance.QRCodeTrackingSupported} | ");
        sb.Append($"config pedida QR={MRUK.Instance.SceneSettings.TrackerConfiguration.QRCodeTrackingEnabled} | ");
        sb.Append($"config ATIVA QR={MRUK.Instance.TrackerConfiguration.QRCodeTrackingEnabled} | ");

        MRUK.Instance.GetTrackables(_trackables);
        int qrs = 0;
        foreach (var t in _trackables)
        {
            if (t == null || t.TrackableType != OVRAnchor.TrackableType.QRCode) continue;
            qrs++;
            sb.Append($"QR \"{t.MarkerPayloadString}\" tracked={t.IsTracked} pos={t.transform.position} | ");
        }
        if (qrs == 0) sb.Append("nenhum QR detetado | ");
        sb.Append($"estabilidade={_leiturasOk}/{LeiturasEstaveis} | fonte atual={FonteAtual}");
        Debug.Log(sb.ToString());
    }

    // ── Deteção + estabilização ───────────────────────────────────────
    void ProcurarQrEstavel()
    {
        MRUK.Instance.GetTrackables(_trackables);

        foreach (var t in _trackables)
        {
            if (t == null || !t.IsTracked) continue;
            if (t.TrackableType != OVRAnchor.TrackableType.QRCode) continue;
            if (!string.IsNullOrEmpty(PayloadFiltro) &&
                (t.MarkerPayloadString == null || !t.MarkerPayloadString.Contains(PayloadFiltro))) continue;

            var leitura = new Pose(t.transform.position, t.transform.rotation);

            // Já alinhado: só re-alinha se a base tiver sido mesmo movida.
            if (Alinhado)
            {
                Pose origemNova = ComporOrigem(leitura);
                if (OmmoRoot != null &&
                    Vector3.Distance(origemNova.position, OmmoRoot.position) > LimiarRealinhar)
                {
                    Debug.Log("[AlinhadorQr] QR re-detetado com desvio grande — a re-alinhar.");
                    Alinhado = false;
                    _leiturasOk = 0;
                    _temUltimaLeitura = false;
                }
                return;
            }

            // Estabilização: N leituras consecutivas com jitter baixo.
            if (_temUltimaLeitura &&
                Vector3.Distance(leitura.position, _ultimaLeitura.position) <= LimiarJitter)
                _leiturasOk++;
            else
                _leiturasOk = 0;

            _ultimaLeitura    = leitura;
            _temUltimaLeitura = true;

            if (_leiturasOk >= LeiturasEstaveis)
            {
                var origem = ComporOrigem(leitura);
                AplicarPose(origem, persistir: true, Fonte.Qr);
                Debug.Log($"[AlinhadorQr] ✅ Alinhado pelo QR \"{t.MarkerPayloadString}\" após {LeiturasEstaveis} leituras estáveis:\n" +
                          $"  pose QR:     pos=({leitura.position.x:F3}, {leitura.position.y:F3}, {leitura.position.z:F3}) " +
                          $"euler={leitura.rotation.eulerAngles}\n" +
                          $"  offset QR→origem: pos={OffsetPosicao} euler={OffsetRotacaoEuler}\n" +
                          $"  origem Ommo: pos=({origem.position.x:F3}, {origem.position.y:F3}, {origem.position.z:F3}) " +
                          $"euler={origem.rotation.eulerAngles}");
            }
            return; // considera só o primeiro QR válido
        }
    }

    /// <summary>Compõe a pose da origem Ommo a partir da pose do QR e do offset físico.</summary>
    Pose ComporOrigem(Pose poseQr)
    {
        Quaternion rotOffset = Quaternion.Euler(OffsetRotacaoEuler) * Quaternion.Euler(0f, CorrecaoYawOmmoGraus, 0f);
        return new Pose(
            poseQr.position + poseQr.rotation * OffsetPosicao,
            poseQr.rotation * rotOffset);
    }

    void AplicarPose(Pose pose, bool persistir, Fonte fonte)
    {
        if (OmmoRoot != null)
        {
            OmmoRoot.SetPositionAndRotation(pose.position, pose.rotation);
        }
        Alinhado   = true;
        FonteAtual = fonte;
        if (persistir && SessionManager.Instancia != null)
            SessionManager.Instancia.GuardarAlinhamentoOmmo(pose);
    }

    // ── Plano B — manual ──────────────────────────────────────────────
    /// <summary>
    /// Alinhamento grosseiro sem QR: assume que o jogador está de frente para a
    /// base station, a <see cref="DistanciaManual"/> da cabeça, com a base a
    /// <see cref="AlturaManual"/> do chão, virada para o jogador.
    /// </summary>
    public void AlinharManualFrenteCabeca()
    {
        var xr = GestorXR.Instancia;
        var cabeca = xr != null ? xr.Cabeca : null;
        if (cabeca == null)
        {
            Debug.LogWarning("[AlinhadorQr] Alinhamento manual falhou — VR não está ativo.");
            return;
        }

        Vector3 frente = cabeca.forward; frente.y = 0f;
        if (frente.sqrMagnitude < 0.001f) frente = Vector3.forward;
        frente.Normalize();

        Vector3 pos = cabeca.position + frente * DistanciaManual;
        pos.y = AlturaManual;
        // A base fica virada para o jogador (frente = -frente da cabeça), com a
        // correção da convenção de eixos do Ommo por cima.
        Quaternion rot = Quaternion.LookRotation(-frente, Vector3.up) *
                         Quaternion.Euler(0f, CorrecaoYawOmmoGraus, 0f);

        AplicarPose(new Pose(pos, rot), persistir: true, Fonte.Manual);
        Debug.Log($"[AlinhadorQr] Alinhamento MANUAL aplicado: origem Ommo em {pos} (euler {rot.eulerAngles}).");
    }
}
