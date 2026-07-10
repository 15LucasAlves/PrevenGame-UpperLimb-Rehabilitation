using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// OmmoCalibracaoManager — Máquina de estados da calibração do esqueleto (1 sensor, palma).
///
/// No novo fluxo, a calibração é arrancada pelo <see cref="GameFlowManager"/> (após o splash)
/// via <see cref="IniciarCalibracao"/>. As instruções são mostradas pelo
/// <see cref="HelperDialogueManager"/> (personagem + balão) em vez de texto simples. Ao concluir,
/// grava os resultados no <see cref="SessionManager"/> e emite <see cref="OnCalibracaoConcluida"/>.
///
/// Fluxo: AguardarSensores → AtribuirSensores → Ombro → BracoEstendido → Completo.
/// Captura: auto (janela deslizante estável 3 s) ou manual (Enter).
/// </summary>
public class OmmoCalibracaoManager : MonoBehaviour
{
    public enum EstadoCalibracao
    {
        AguardarSensores, AtribuirSensores, Ombro, BracoEstendido, Completo
    }

    [Header("Referências")]
    public OmmoSensorManager    SensorManager;
    public OmmoEsqueletoJogador Esqueleto;

    [Header("Helpers")]
    [Tooltip("Se atribuído, as instruções aparecem no personagem + balão em vez do texto simples.")]
    public HelperDialogueManager Dialogo;
    public HelperId HelperCalibracao = HelperId.Jane;

    [Header("UI de Calibração (fallback sem helper)")]
    public GameObject      PainelCalibracao;
    public TextMeshProUGUI TextoInstrucao;
    public TextMeshProUGUI TextoPasso;
    public TextMeshProUGUI TextoSub;
    public Image           BarraProgressoImagem;

    [Header("Parâmetros")]
    public float TempoEstabilidade = 3.0f;
    public float LimiarMovimento   = 0.04f;
    public int   NumAmostras       = 10;

    [Tooltip("Arrancar a calibração automaticamente no Start (para testar a cena isolada).")]
    public bool AutoIniciar = false;

    /// <summary>Emitido quando a calibração termina (resultados já gravados no SessionManager).</summary>
    public event System.Action OnCalibracaoConcluida;

    private EstadoCalibracao _estado = EstadoCalibracao.AguardarSensores;
    private OmmoDevice _devicePalma;
    private bool _ativo;

    private Queue<Vector3> _historicoPos = new Queue<Vector3>();
    private float _tempoEstavel = 0f;
    private bool  _capturando   = false;

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
    }

    /// <summary>Arranca a calibração: mostra a 1ª instrução e inicia o tracking do sensor.</summary>
    public void IniciarCalibracao()
    {
        if (_ativo) return;
        _ativo  = true;
        _estado = EstadoCalibracao.AguardarSensores;
        if (SensorManager) SensorManager.IniciarTracking(1);
        AtualizarUI();
    }

    void Update()
    {
        if (!_ativo) return;
        if (_estado == EstadoCalibracao.AguardarSensores ||
            _estado == EstadoCalibracao.Completo) return;

        OmmoDevice deviceAtivo = SensorParaEstadoAtual();
        if (deviceAtivo == null || deviceAtivo.NumeroSensores == 0) return;

        Vector3 posAtual = deviceAtivo.ObterPosicaoSensor(0);

        if (!_capturando &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            _capturando = true;
            CapturarEstado(posAtual);
            return;
        }

        _historicoPos.Enqueue(posAtual);
        if (_historicoPos.Count > NumAmostras) _historicoPos.Dequeue();
        if (_historicoPos.Count < NumAmostras) return;

        float variacaoMax = 0f;
        foreach (var pos in _historicoPos)
            variacaoMax = Mathf.Max(variacaoMax, Vector3.Distance(pos, posAtual));

        if (variacaoMax < LimiarMovimento) _tempoEstavel += Time.deltaTime;
        else                                _tempoEstavel = 0f;

        if (BarraProgressoImagem)
            BarraProgressoImagem.fillAmount = Mathf.Clamp01(_tempoEstavel / TempoEstabilidade);

        if (!_capturando && _tempoEstavel >= TempoEstabilidade)
        {
            _capturando = true;
            CapturarEstado(posAtual);
        }
    }

    // ── Deteção de sensores ───────────────────────────────────────────
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
        AvancarEstado(); // → AtribuirSensores
    }

    // ── Captura ───────────────────────────────────────────────────────
    void CapturarEstado(Vector3 posicao)
    {
        switch (_estado)
        {
            case EstadoCalibracao.AtribuirSensores:
                break;
            case EstadoCalibracao.Ombro:
                if (Esqueleto) Esqueleto.DefinirPosicaoFixa("Ombro", posicao);
                break;
            case EstadoCalibracao.BracoEstendido:
                if (Esqueleto != null)
                {
                    Esqueleto.DefinirComprimentoBraco(Vector3.Distance(posicao, Esqueleto.PosOmbroBase));
                    Esqueleto.DefinirDirecaoFrente(posicao);
                }
                break;
        }
        AvancarEstado();
    }

    void AvancarEstado()
    {
        _historicoPos.Clear();
        _tempoEstavel = 0f;
        _capturando   = false;
        if (BarraProgressoImagem) BarraProgressoImagem.fillAmount = 0f;

        _estado = ProximoEstado();
        if (_estado == EstadoCalibracao.Completo) ConcluirCalibracao();
        AtualizarUI();
    }

    EstadoCalibracao ProximoEstado()
    {
        switch (_estado)
        {
            case EstadoCalibracao.AguardarSensores: return EstadoCalibracao.AtribuirSensores;
            case EstadoCalibracao.AtribuirSensores: return EstadoCalibracao.Ombro;
            case EstadoCalibracao.Ombro:            return EstadoCalibracao.BracoEstendido;
            case EstadoCalibracao.BracoEstendido:   return EstadoCalibracao.Completo;
            default:                                return EstadoCalibracao.Completo;
        }
    }

    void ConcluirCalibracao()
    {
        if (Esqueleto) Esqueleto.AtivacaoEsqueleto(true);

        // Grava a calibração no estado persistente entre cenas.
        if (SessionManager.Instancia != null && Esqueleto != null)
        {
            SessionManager.Instancia.GuardarCalibracao(
                Esqueleto.PosOmbroBase, Esqueleto.ComprimentoBraco, Esqueleto.DirecaoFrente);
            SessionManager.Instancia.HelperCalibracao = HelperCalibracao;
        }

        Invoke(nameof(EmitirConclusao), 1.5f); // deixa ver a mensagem "concluída"
    }

    void EmitirConclusao()
    {
        _ativo = false;
        OnCalibracaoConcluida?.Invoke();
    }

    // ── UI / instruções ───────────────────────────────────────────────
    void AtualizarUI()
    {
        if (BarraProgressoImagem)
        {
            bool mostrar = _estado != EstadoCalibracao.AguardarSensores &&
                           _estado != EstadoCalibracao.Completo;
            BarraProgressoImagem.gameObject.SetActive(mostrar);
        }

        string instr = InstrucaoParaEstado();
        string sub   = SubtextoParaEstado();
        string passo = PassoParaEstado();

        if (Dialogo != null)
        {
            // Instrução via personagem + balão.
            Dialogo.MostrarLinha(HelperCalibracao, EmocaoParaEstado(),
                string.IsNullOrEmpty(sub) ? instr : instr + "\n" + sub);
        }

        if (TextoInstrucao) TextoInstrucao.text = instr;
        if (TextoSub)       TextoSub.text       = sub;
        if (TextoPasso)     TextoPasso.text      = passo;
    }

    HelperEmocao EmocaoParaEstado()
    {
        switch (_estado)
        {
            case EstadoCalibracao.Completo: return HelperEmocao.Impressed;
            case EstadoCalibracao.AguardarSensores: return HelperEmocao.Neutral;
            default: return HelperEmocao.Pleased;
        }
    }

    string InstrucaoParaEstado()
    {
        switch (_estado)
        {
            case EstadoCalibracao.AguardarSensores: return "A aguardar sensor...";
            case EstadoCalibracao.AtribuirSensores: return "Coloca o sensor na palma da mão";
            case EstadoCalibracao.Ombro:            return "Toca no ombro com o sensor da palma";
            case EstadoCalibracao.BracoEstendido:   return "Estica o braço para a frente, paralelo ao chão";
            case EstadoCalibracao.Completo:         return "Calibração concluída!";
            default:                                return "";
        }
    }

    string SubtextoParaEstado()
    {
        switch (_estado)
        {
            case EstadoCalibracao.AguardarSensores: return "Liga o sensor da palma.";
            case EstadoCalibracao.AtribuirSensores: return "Fica parado ou prime Enter quando estiveres pronto.";
            case EstadoCalibracao.Ombro:            return "Toca no ombro e prime Enter, ou mantém quieto.";
            case EstadoCalibracao.BracoEstendido:   return "Mantém estável ou prime Enter para capturar.";
            case EstadoCalibracao.Completo:         return "O esqueleto está a seguir os teus movimentos.";
            default:                                return "";
        }
    }

    string PassoParaEstado()
    {
        switch (_estado)
        {
            case EstadoCalibracao.Ombro:          return "Passo 1 / 2";
            case EstadoCalibracao.BracoEstendido: return "Passo 2 / 2";
            default:                              return "";
        }
    }

    OmmoDevice SensorParaEstadoAtual()
    {
        switch (_estado)
        {
            case EstadoCalibracao.AtribuirSensores:
            case EstadoCalibracao.Ombro:
            case EstadoCalibracao.BracoEstendido:
                return _devicePalma;
            default:
                return null;
        }
    }

    public EstadoCalibracao Estado => _estado;
    public bool Calibrado => _estado == EstadoCalibracao.Completo;
}
