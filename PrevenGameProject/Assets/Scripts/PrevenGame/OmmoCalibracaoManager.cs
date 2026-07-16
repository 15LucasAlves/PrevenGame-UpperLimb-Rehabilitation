using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// OmmoCalibracaoManager — Calibração do membro superior (1 sensor na palma), braço a braço.
///
/// Arrancada pelo <see cref="GameFlowManager"/> após o splash. A conversa de introdução
/// (<see cref="IntroSeq"/> — a Jane e o Patrick apresentam o jogo; avança por clique) corre
/// enquanto o sensor liga. Depois os helpers guiam as capturas: o Patrick o braço direito,
/// a Jane o esquerdo. Cada braço tem dois passos — mão estendida (alcance máximo) e ombro —
/// confirmados com PRESSÃO no Ommo (<see cref="EntradaPressao"/>) ou Enter (fallback).
///
/// Como os braços podem ter comprimentos/ROM diferentes, cada braço grava o seu conjunto
/// (ombro, comprimento, direção frente) no <see cref="SessionManager"/> via
/// <see cref="SessionManager.GuardarCalibracaoBraco"/>.
///
/// Fluxo: AguardarSensores → BracoEstendidoDireito → OmbroDireito → BracoEstendidoEsquerdo
///        → OmbroEsquerdo → Completo.
/// </summary>
public class OmmoCalibracaoManager : MonoBehaviour
{
    public enum EstadoCalibracao
    {
        AguardarSensores,
        BracoEstendidoDireito, OmbroDireito,
        BracoEstendidoEsquerdo, OmbroEsquerdo,
        Completo
    }

    [Header("Referências")]
    public OmmoSensorManager SensorManager;
    [Tooltip("Alinhador Ommo↔VR (QR). Em VR, as capturas ESPERAM pelo alinhamento — senão as posições ficariam no referencial errado. Auto-encontrado se vazio.")]
    public AlinhadorOmmoQr Alinhador;

    [Header("Diálogo")]
    [Tooltip("Personagens + balão que dão as instruções (Patrick braço direito, Jane esquerdo).")]
    public HelperDialogueManager Dialogo;
    [Tooltip("Conversa de introdução (avança por clique) antes das capturas.")]
    public DialogueSequence IntroSeq;
    [Tooltip("Ecrã VR do paciente — as instruções das capturas aparecem também aqui (headset posto).")]
    public EcraVR EcraVr;

    [Header("Entrada de captura")]
    [Tooltip("Serviço de pressão BLE (opcional). Enter funciona sempre como fallback.")]
    public EntradaPressao Pressao;
    [Tooltip("Emparelhamento automático do SIU (opcional) — dá feedback no balão quando emparelha.")]
    public OmmoAutoPairing AutoPairing;
    [Tooltip("Tempo mínimo entre duas capturas (segundos) — evita capturas duplas.")]
    public float DebounceCaptura = 0.75f;

    [Tooltip("Arrancar a calibração automaticamente no Start (para testar a cena isolada).")]
    public bool AutoIniciar = false;

    /// <summary>Emitido quando a calibração termina (resultados já gravados no SessionManager).</summary>
    public event System.Action OnCalibracaoConcluida;

    private EstadoCalibracao _estado = EstadoCalibracao.AguardarSensores;
    private OmmoDevice _devicePalma;
    private bool  _ativo;
    private bool  _introTerminada;
    private bool  _confirmacaoAgendada;
    private bool  _aguardaAlinhamento;
    private bool  _pressaoPendente;
    private float _ultimaCaptura = -999f;

    // Posições da mão estendida, capturadas antes do ombro respetivo (o comprimento do
    // braço e a direção frente só se calculam quando o ombro é conhecido).
    private Vector3 _posEstendidaDireita;
    private Vector3 _posEstendidaEsquerda;

    // Pose da cabeça (CenterEyeAnchor) no instante da captura do ombro — permite
    // derivar o ombro da posição da câmara em runtime (RastreadorCorpoJogador).
    private Vector3    _posCabecaCaptura;
    private Quaternion _rotCabecaCaptura;
    private bool       _temCabecaCaptura;

    // ── Unity ─────────────────────────────────────────────────────────
    void Start()
    {
        if (SensorManager == null) SensorManager = FindObjectOfType<OmmoSensorManager>();
        if (Alinhador     == null) Alinhador     = FindObjectOfType<AlinhadorOmmoQr>();
        if (SensorManager) SensorManager.OnNumeroDeSensoresMudou += AoNumeroDeSensoresMudou;
        if (AutoIniciar) IniciarCalibracao();
    }

    void OnDestroy()
    {
        if (SensorManager) SensorManager.OnNumeroDeSensoresMudou -= AoNumeroDeSensoresMudou;
        if (Pressao)       Pressao.OnPressao -= AoPressao;
        if (AutoPairing)   AutoPairing.OnSiuEmparelhado -= AoSiuEmparelhado;
    }

    /// <summary>Arranca a calibração: intro dos helpers + tracking do sensor em paralelo.</summary>
    public void IniciarCalibracao()
    {
        if (_ativo) return;
        _ativo               = true;
        _estado              = EstadoCalibracao.AguardarSensores;
        _introTerminada      = false;
        _confirmacaoAgendada = false;
        _pressaoPendente     = false;

        if (SensorManager) SensorManager.IniciarTracking(1);
        if (Pressao)       Pressao.OnPressao += AoPressao;
        if (AutoPairing)   AutoPairing.OnSiuEmparelhado += AoSiuEmparelhado;

        if (Dialogo != null && IntroSeq != null && IntroSeq.TemFalas)
        {
            Debug.Log($"[Calibracao] A reproduzir intro ({IntroSeq.Falas.Count} falas).");
            Dialogo.Reproduzir(IntroSeq, AoIntroTerminada);
        }
        else
        {
            Debug.LogWarning("[Calibracao] Sem IntroSeq/Dialogo — a saltar a introdução.");
            AoIntroTerminada();
        }
    }

    void Update()
    {
        if (!_ativo || !EmCaptura()) return;
        if (_devicePalma == null || _devicePalma.NumeroSensores == 0) return;

        bool pedir = _pressaoPendente ||
                     Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        _pressaoPendente = false;

        if (!pedir) return;
        if (Time.unscaledTime - _ultimaCaptura < DebounceCaptura) return;

        _ultimaCaptura = Time.unscaledTime;
        CapturarEstado(_devicePalma.ObterPosicaoSensor(0));
    }

    // ── Intro / arranque das capturas ─────────────────────────────────
    void AoIntroTerminada()
    {
        Debug.Log("[Calibracao] Intro terminada.");
        _introTerminada = true;

        // A intro termina com "agarra o Ommo e põe o headset" — a partir daqui a
        // calibração decorre em VR, para capturar a relação cabeça↔ombro no mesmo
        // referencial. Se o VR falhar (sem headset/Link), continua no monitor.
        var xr = GestorXR.ObterOuCriar();
        bool emVr = xr != null && xr.ModoVR();
        if (!emVr)
            Debug.LogWarning("[Calibracao] VR indisponível — calibração continua no monitor.");

        // (O passthrough é gerido pelo GestorXR: liga as câmaras ANTES de o MRUK
        //  configurar os trackers — sem isso o QR nunca deteta.)

        // Ecrã VR do paciente: as instruções das capturas passam a aparecer também lá.
        if (emVr && EcraVr != null) EcraVr.Mostrar(true);

        TentarComecarCapturas();
    }

    void AoNumeroDeSensoresMudou(int count)
    {
        if (!_ativo) return;
        if (_estado != EstadoCalibracao.AguardarSensores) return;
        if (count < 1) return;

        var devices = new List<OmmoDevice>(FindObjectsOfType<OmmoDevice>());
        if (devices.Count < 1) return;
        devices.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        _devicePalma = devices[0];

        TentarComecarCapturas();
    }

    void TentarComecarCapturas()
    {
        // Nunca interrompe a intro: isto só corre depois de o jogador a ver toda.
        if (!_introTerminada || _estado != EstadoCalibracao.AguardarSensores) return;

        if (_devicePalma == null)
        {
            // A intro acabou mas o sensor ainda não ligou — informa e fica à espera
            // (AoNumeroDeSensoresMudou volta a chamar isto quando ligar).
            Debug.Log("[Calibracao] Sem sensor — a mostrar linha de espera (auto-pairing ativo).");
            MostrarLinhaAmbos(HelperId.Patrick, HelperEmocao.Neutral, "A aguardar sensor...");
            return;
        }

        // Em VR, as capturas SÓ começam depois do alinhamento QR: sem ele, as
        // posições do sensor estariam num referencial que o alinhamento posterior
        // invalidaria (ombros/offsets errados). F2 = alinhamento manual de recurso.
        var xr = GestorXR.Instancia;
        if (xr != null && xr.VrAtivo && Alinhador != null && !Alinhador.Alinhado)
        {
            if (!_aguardaAlinhamento)
            {
                _aguardaAlinhamento = true;
                Debug.Log("[Calibracao] À espera do alinhamento QR antes das capturas...");
                MostrarLinhaAmbos(HelperId.Patrick, HelperEmocao.Neutral,
                    "Olha para a base do Ommo — estou a detetar o código QR...");
                StartCoroutine(EsperarAlinhamento());
            }
            return;
        }

        // Sensor presente: confirmação breve para o jogador perceber que o
        // dispositivo foi reconhecido, e só depois começam as capturas.
        if (_confirmacaoAgendada) return;
        _confirmacaoAgendada = true;
        Debug.Log("[Calibracao] Sensor ligado — confirmação antes das capturas.");
        MostrarLinhaAmbos(HelperId.Patrick, HelperEmocao.Pleased, "Sensor ligado!");
        Invoke(nameof(ComecarCapturas), 1.2f);
    }

    System.Collections.IEnumerator EsperarAlinhamento()
    {
        while (_ativo && Alinhador != null && !Alinhador.Alinhado)
            yield return null;
        _aguardaAlinhamento = false;
        if (!_ativo) yield break;

        Debug.Log($"[Calibracao] Alinhamento pronto (fonte: {(Alinhador != null ? Alinhador.FonteAtual.ToString() : "?")}) — a prosseguir.");
        MostrarLinhaAmbos(HelperId.Patrick, HelperEmocao.Pleased, "Base station encontrada!");
        TentarComecarCapturas();
    }

    void ComecarCapturas()
    {
        if (!_ativo || _estado != EstadoCalibracao.AguardarSensores) return;
        Debug.Log("[Calibracao] A começar as capturas (braço direito primeiro).");
        _pressaoPendente = false;
        _ultimaCaptura   = Time.unscaledTime; // debounce inicial: nada captura no 1º instante
        _estado = EstadoCalibracao.BracoEstendidoDireito;
        MostrarInstrucao();
    }

    /// <summary>Pressão no Ommo só conta nos passos de captura (não salta a intro).</summary>
    void AoPressao()
    {
        if (EmCaptura()) _pressaoPendente = true;
    }

    /// <summary>
    /// Feedback no balão quando o auto-pairing aprova o SIU. Só na fase de ESPERA —
    /// nunca interrompe a intro (o MostrarLinha mataria a sequência) nem as capturas.
    /// O fluxo avança sozinho quando o OmmoDevice ligar.
    /// </summary>
    void AoSiuEmparelhado(uint uuid)
    {
        if (!_ativo || !_introTerminada || _confirmacaoAgendada) return;
        if (_estado != EstadoCalibracao.AguardarSensores || _devicePalma != null) return;
        MostrarLinhaAmbos(HelperId.Patrick, HelperEmocao.Pleased, "Sensor emparelhado!");
    }

    // ── Captura ───────────────────────────────────────────────────────
    void CapturarEstado(Vector3 pos)
    {
        // Pose da cabeça no instante da captura (só interessa nos passos de ombro,
        // mas capturar sempre é inofensivo).
        var cabeca = GestorXR.Instancia != null ? GestorXR.Instancia.Cabeca : null;
        _temCabecaCaptura = cabeca != null;
        if (_temCabecaCaptura)
        {
            _posCabecaCaptura = cabeca.position;
            _rotCabecaCaptura = cabeca.rotation;
        }

        switch (_estado)
        {
            case EstadoCalibracao.BracoEstendidoDireito:
                _posEstendidaDireita = pos;
                _estado = EstadoCalibracao.OmbroDireito;
                break;

            case EstadoCalibracao.OmbroDireito:
                GuardarBraco(direito: true, posOmbro: pos, posEstendida: _posEstendidaDireita);
                _estado = EstadoCalibracao.BracoEstendidoEsquerdo;
                break;

            case EstadoCalibracao.BracoEstendidoEsquerdo:
                _posEstendidaEsquerda = pos;
                _estado = EstadoCalibracao.OmbroEsquerdo;
                break;

            case EstadoCalibracao.OmbroEsquerdo:
                GuardarBraco(direito: false, posOmbro: pos, posEstendida: _posEstendidaEsquerda);
                _estado = EstadoCalibracao.Completo;
                ConcluirCalibracao();
                return;
        }
        MostrarInstrucao();
    }

    /// <summary>
    /// Calcula o comprimento e a direção frente do braço e grava no SessionManager.
    /// Com o headset posto, grava também o offset cabeça→ombro (referencial yaw-local
    /// da cabeça) para o ombro poder seguir a câmara em runtime.
    /// </summary>
    void GuardarBraco(bool direito, Vector3 posOmbro, Vector3 posEstendida)
    {
        float comprimento = Vector3.Distance(posEstendida, posOmbro);

        // Direção frente: do ombro para a mão estendida, projetada no plano horizontal.
        Vector3 dir        = posEstendida - posOmbro;
        Vector3 horizontal = new Vector3(dir.x, 0f, dir.z);
        Vector3 direcao    = horizontal.magnitude > 0.05f ? horizontal.normalized : Vector3.forward;

        // Offsets relativos à cabeça (a captura do ombro foi feita com headset posto).
        Vector3 offsetLocal   = Vector3.zero;
        Vector3 direcaoLocal  = Vector3.zero;
        bool temDadosCabeca   = _temCabecaCaptura;
        float  yawCabecaGraus = 0f;
        if (temDadosCabeca)
        {
            yawCabecaGraus    = _rotCabecaCaptura.eulerAngles.y;
            Quaternion yaw    = Quaternion.Euler(0f, yawCabecaGraus, 0f);
            Quaternion yawInv = Quaternion.Inverse(yaw);
            offsetLocal  = yawInv * (posOmbro - _posCabecaCaptura);
            direcaoLocal = yawInv * direcao;
        }

        if (SessionManager.Instancia != null)
            SessionManager.Instancia.GuardarCalibracaoBraco(direito, posOmbro, comprimento, direcao,
                                                            offsetLocal, direcaoLocal, temDadosCabeca);

        // ── Log detalhado da captura (números + cálculos) ─────────────
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Calibracao] ══ Braço {(direito ? "DIREITO" : "ESQUERDO")} ══");
        sb.AppendLine($"  mão estendida (mundo): ({posEstendida.x:F3}, {posEstendida.y:F3}, {posEstendida.z:F3}) m");
        sb.AppendLine($"  ombro (mundo):         ({posOmbro.x:F3}, {posOmbro.y:F3}, {posOmbro.z:F3}) m");
        sb.AppendLine($"  comprimento do braço = |estendida − ombro| = {comprimento:F3} m ({comprimento * 100f:F1} cm)");
        sb.AppendLine($"  direção frente (horizontal, normalizada): ({direcao.x:F3}, {direcao.y:F3}, {direcao.z:F3})");
        if (temDadosCabeca)
        {
            Vector3 delta = posOmbro - _posCabecaCaptura;
            sb.AppendLine($"  cabeça VR (mundo):     ({_posCabecaCaptura.x:F3}, {_posCabecaCaptura.y:F3}, {_posCabecaCaptura.z:F3}) m | yaw={yawCabecaGraus:F1}°");
            sb.AppendLine($"  cabeça→ombro (mundo):  ({delta.x:F3}, {delta.y:F3}, {delta.z:F3}) | distância={delta.magnitude:F3} m " +
                          $"(vertical={-delta.y:F3} m abaixo da cabeça)");
            sb.AppendLine($"  offset yaw-local (guardado): ({offsetLocal.x:F3}, {offsetLocal.y:F3}, {offsetLocal.z:F3}) " +
                          $"[x=lateral, y=vertical, z=frente]");
            sb.AppendLine($"  direção frente yaw-local:    ({direcaoLocal.x:F3}, {direcaoLocal.y:F3}, {direcaoLocal.z:F3})");
        }
        else sb.AppendLine("  ⚠ SEM dados de cabeça (VR inativo) — ombro fica fixo no mundo (fallback).");
        Debug.Log(sb.ToString());
    }

    void ConcluirCalibracao()
    {
        LogResumoCalibracao();

        MostrarLinhaAmbos(HelperId.Patrick, HelperEmocao.Neutral, "Calibração concluída. Boa sorte!");
        Dialogo?.DefinirEmocao(HelperId.Jane, HelperEmocao.Neutral); // a Jane volta também a Neutral

        Invoke(nameof(EmitirConclusao), 1.5f); // deixa ler a mensagem final
    }

    /// <summary>Resumo final na consola: os dois braços + estado do alinhamento Ommo↔VR.</summary>
    void LogResumoCalibracao()
    {
        var sm = SessionManager.Instancia;
        if (sm == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[Calibracao] ════════ RESUMO DA CALIBRAÇÃO ════════");

        // Alinhamento Ommo↔VR (QR na base station).
        if (Alinhador != null && Alinhador.Alinhado && Alinhador.OmmoRoot != null)
        {
            var root = Alinhador.OmmoRoot;
            sb.AppendLine($"  Alinhamento Ommo↔VR: {Alinhador.FonteAtual} | origem Ommo em " +
                          $"({root.position.x:F3}, {root.position.y:F3}, {root.position.z:F3}) m, " +
                          $"euler {root.rotation.eulerAngles}");
        }
        else sb.AppendLine("  Alinhamento Ommo↔VR: NENHUM (BaseStation na pose por defeito da cena)");

        // Cabeça agora vs na captura (sanidade).
        var cabeca = GestorXR.Instancia != null ? GestorXR.Instancia.Cabeca : null;
        if (cabeca != null)
            sb.AppendLine($"  Cabeça VR agora: ({cabeca.position.x:F3}, {cabeca.position.y:F3}, {cabeca.position.z:F3}) m " +
                          $"| altura ao chão={cabeca.position.y:F2} m | yaw={cabeca.eulerAngles.y:F1}°");

        ResumirBraco(sb, "Direito ", sm.ObterBraco(true), cabeca);
        ResumirBraco(sb, "Esquerdo", sm.ObterBraco(false), cabeca);

        // Diferença entre braços (assimetrias grandes podem indicar má captura).
        var d = sm.ObterBraco(true); var e = sm.ObterBraco(false);
        if (d.Valido && e.Valido)
        {
            sb.AppendLine($"  Δ comprimento dir−esq: {(d.ComprimentoBraco - e.ComprimentoBraco) * 100f:+0.0;-0.0} cm");
            if (d.TemDadosCabeca && e.TemDadosCabeca)
                sb.AppendLine($"  largura de ombros (|offset dir − offset esq| lateral): " +
                              $"{Mathf.Abs(d.OffsetOmbroLocalCabeca.x - e.OffsetOmbroLocalCabeca.x):F3} m");
        }
        sb.AppendLine("[Calibracao] ═══════════════════════════════════════");
        Debug.Log(sb.ToString());
    }

    static void ResumirBraco(System.Text.StringBuilder sb, string nome, SessionManager.DadosBraco b, Transform cabeca)
    {
        if (!b.Valido) { sb.AppendLine($"  Braço {nome}: INVÁLIDO"); return; }
        sb.Append($"  Braço {nome}: comprimento={b.ComprimentoBraco * 100f:F1} cm | " +
                  $"ombro mundo=({b.PosOmbro.x:F3}, {b.PosOmbro.y:F3}, {b.PosOmbro.z:F3})");
        if (b.TemDadosCabeca)
        {
            sb.Append($" | offset cabeça→ombro (yaw-local)=({b.OffsetOmbroLocalCabeca.x:F3}, " +
                      $"{b.OffsetOmbroLocalCabeca.y:F3}, {b.OffsetOmbroLocalCabeca.z:F3}) " +
                      $"dist={b.OffsetOmbroLocalCabeca.magnitude:F3} m");
            // Ombro derivado da câmara NESTE momento (o que o jogo vai usar).
            if (cabeca != null)
            {
                Quaternion yaw = Quaternion.Euler(0f, cabeca.eulerAngles.y, 0f);
                Vector3 ombroAgora = cabeca.position + yaw * b.OffsetOmbroLocalCabeca;
                sb.Append($" | ombro derivado AGORA=({ombroAgora.x:F3}, {ombroAgora.y:F3}, {ombroAgora.z:F3})");
            }
        }
        else sb.Append(" | sem dados de cabeça (fallback mundo)");
        sb.AppendLine();
    }

    void EmitirConclusao()
    {
        _ativo = false;
        if (Pressao)     Pressao.OnPressao -= AoPressao;
        if (AutoPairing) AutoPairing.OnSiuEmparelhado -= AoSiuEmparelhado;
        if (EcraVr != null) EcraVr.Mostrar(false);
        OnCalibracaoConcluida?.Invoke();
    }

    /// <summary>
    /// Mostra a mesma linha no diálogo do monitor (fisioterapeuta) e no EcraVR
    /// (paciente com o headset posto). Um dos dois pode não existir.
    /// </summary>
    void MostrarLinhaAmbos(HelperId quem, HelperEmocao emocao, string texto)
    {
        Dialogo?.MostrarLinha(quem, emocao, texto);
        if (EcraVr != null && EcraVr.Dialogo != null && EcraVr.gameObject.activeInHierarchy)
            EcraVr.Dialogo.MostrarLinha(quem, emocao, texto);
    }

    // ── Instruções (guião: Patrick → braço direito, Jane → esquerdo) ──
    void MostrarInstrucao()
    {
        switch (_estado)
        {
            case EstadoCalibracao.BracoEstendidoDireito:
                MostrarLinhaAmbos(HelperId.Patrick, HelperEmocao.Pleased,
                    "Primeiro com o braço direito. Estica-o para a frente e quando estiveres no teu máximo faz pressão no Ommo.");
                break;
            case EstadoCalibracao.OmbroDireito:
                MostrarLinhaAmbos(HelperId.Patrick, HelperEmocao.Pleased,
                    "Agora encosta o Ommo ao ombro e faz pressão quando estiver no sítio correto.");
                break;
            case EstadoCalibracao.BracoEstendidoEsquerdo:
                MostrarLinhaAmbos(HelperId.Jane, HelperEmocao.Pleased,
                    "Agora com o braço esquerdo. Estica-o para a frente e quando estiveres no teu máximo faz pressão no Ommo.");
                break;
            case EstadoCalibracao.OmbroEsquerdo:
                MostrarLinhaAmbos(HelperId.Jane, HelperEmocao.Pleased,
                    "Agora encosta o Ommo ao ombro e faz pressão quando estiver no sítio correto.");
                break;
        }
    }

    bool EmCaptura() =>
        _estado == EstadoCalibracao.BracoEstendidoDireito  ||
        _estado == EstadoCalibracao.OmbroDireito           ||
        _estado == EstadoCalibracao.BracoEstendidoEsquerdo ||
        _estado == EstadoCalibracao.OmbroEsquerdo;

    public EstadoCalibracao Estado => _estado;
    public bool Calibrado => _estado == EstadoCalibracao.Completo;
}
