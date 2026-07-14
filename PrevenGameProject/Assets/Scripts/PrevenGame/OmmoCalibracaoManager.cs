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
    public OmmoSensorManager    SensorManager;
    public OmmoEsqueletoJogador Esqueleto;

    [Header("Diálogo")]
    [Tooltip("Personagens + balão que dão as instruções (Patrick braço direito, Jane esquerdo).")]
    public HelperDialogueManager Dialogo;
    [Tooltip("Conversa de introdução (avança por clique) antes das capturas.")]
    public DialogueSequence IntroSeq;

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
    private bool  _pressaoPendente;
    private float _ultimaCaptura = -999f;

    // Posições da mão estendida, capturadas antes do ombro respetivo (o comprimento do
    // braço e a direção frente só se calculam quando o ombro é conhecido).
    private Vector3 _posEstendidaDireita;
    private Vector3 _posEstendidaEsquerda;

    // ── Unity ─────────────────────────────────────────────────────────
    void Start()
    {
        if (SensorManager == null) SensorManager = FindObjectOfType<OmmoSensorManager>();
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

        if (Esqueleto) Esqueleto.Inicializar(_devicePalma);
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
            Dialogo?.MostrarLinha(HelperId.Patrick, HelperEmocao.Neutral, "A aguardar sensor...");
            return;
        }

        // Sensor presente: confirmação breve para o jogador perceber que o
        // dispositivo foi reconhecido, e só depois começam as capturas.
        if (_confirmacaoAgendada) return;
        _confirmacaoAgendada = true;
        Debug.Log("[Calibracao] Sensor ligado — confirmação antes das capturas.");
        Dialogo?.MostrarLinha(HelperId.Patrick, HelperEmocao.Pleased, "Sensor ligado!");
        Invoke(nameof(ComecarCapturas), 1.2f);
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
        Dialogo?.MostrarLinha(HelperId.Patrick, HelperEmocao.Pleased, "Sensor emparelhado!");
    }

    // ── Captura ───────────────────────────────────────────────────────
    void CapturarEstado(Vector3 pos)
    {
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

    /// <summary>Calcula o comprimento e a direção frente do braço e grava no SessionManager.</summary>
    void GuardarBraco(bool direito, Vector3 posOmbro, Vector3 posEstendida)
    {
        float comprimento = Vector3.Distance(posEstendida, posOmbro);

        // Direção frente: do ombro para a mão estendida, projetada no plano horizontal.
        Vector3 dir        = posEstendida - posOmbro;
        Vector3 horizontal = new Vector3(dir.x, 0f, dir.z);
        Vector3 direcao    = horizontal.magnitude > 0.05f ? horizontal.normalized : Vector3.forward;

        if (SessionManager.Instancia != null)
            SessionManager.Instancia.GuardarCalibracaoBraco(direito, posOmbro, comprimento, direcao);

        Debug.Log($"[Calibracao] Braço {(direito ? "direito" : "esquerdo")}: " +
                  $"comprimento={comprimento:F2}u, ombro={posOmbro}, frente={direcao}");
    }

    void ConcluirCalibracao()
    {
        // O esqueleto da cena Menu fica com o braço direito aplicado (cada minijogo
        // reidrata depois o braço do bloco que estiver a correr).
        if (Esqueleto != null && SessionManager.Instancia != null)
        {
            var d = SessionManager.Instancia.ObterBraco(true);
            if (d.Valido) Esqueleto.AplicarCalibracao(d.PosOmbro, d.ComprimentoBraco, d.DirecaoFrente);
            Esqueleto.AtivacaoEsqueleto(true);
        }

        if (Dialogo != null)
        {
            Dialogo.MostrarLinha(HelperId.Patrick, HelperEmocao.Neutral, "Calibração concluída. Boa sorte!");
            Dialogo.DefinirEmocao(HelperId.Jane, HelperEmocao.Neutral); // a Jane volta também a Neutral
        }

        Invoke(nameof(EmitirConclusao), 1.5f); // deixa ler a mensagem final
    }

    void EmitirConclusao()
    {
        _ativo = false;
        if (Pressao)     Pressao.OnPressao -= AoPressao;
        if (AutoPairing) AutoPairing.OnSiuEmparelhado -= AoSiuEmparelhado;
        OnCalibracaoConcluida?.Invoke();
    }

    // ── Instruções (guião: Patrick → braço direito, Jane → esquerdo) ──
    void MostrarInstrucao()
    {
        if (Dialogo == null) return;
        switch (_estado)
        {
            case EstadoCalibracao.BracoEstendidoDireito:
                Dialogo.MostrarLinha(HelperId.Patrick, HelperEmocao.Pleased,
                    "Primeiro com o braço direito. Estica-o para a frente e quando estiveres no teu máximo faz pressão no Ommo.");
                break;
            case EstadoCalibracao.OmbroDireito:
                Dialogo.MostrarLinha(HelperId.Patrick, HelperEmocao.Pleased,
                    "Agora encosta o Ommo ao ombro e faz pressão quando estiver no sítio correto.");
                break;
            case EstadoCalibracao.BracoEstendidoEsquerdo:
                Dialogo.MostrarLinha(HelperId.Jane, HelperEmocao.Pleased,
                    "Agora com o braço esquerdo. Estica-o para a frente e quando estiveres no teu máximo faz pressão no Ommo.");
                break;
            case EstadoCalibracao.OmbroEsquerdo:
                Dialogo.MostrarLinha(HelperId.Jane, HelperEmocao.Pleased,
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
