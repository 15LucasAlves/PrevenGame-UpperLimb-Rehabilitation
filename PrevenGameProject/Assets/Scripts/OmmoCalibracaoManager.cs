using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// OmmoCalibracaoManager — Máquina de estados da calibração do esqueleto.
///
/// Modo único: 1 sensor (palma). O tracking inicia automaticamente no Start().
///
/// Fluxo:
///   AguardarSensores → AtribuirSensores
///     → Ombro → BracoEstendido → Peito → Cabeca → Completo
///
/// Captura:
///   Auto: janela deslizante de 10 amostras, variação < 4 cm por 3 s.
///   Manual: premir Enter/Return captura imediatamente.
/// </summary>
public class OmmoCalibracaoManager : MonoBehaviour
{
    // ── Estados ───────────────────────────────────────────────────────
    public enum EstadoCalibracao
    {
        AguardarSensores,   // espera o OmmoDevice ligar
        AtribuirSensores,   // instrução de colocação do sensor
        Ombro,              // toca no ombro com a palma (define PosOmbroBase)
        BracoEstendido,     // braço estendido (mede comprimento + direção frontal)
        Completo
    }

    // ── Referências (ligadas pelo OmmoSceneBuilder) ───────────────────
    [Header("Referências")]
    public OmmoSensorManager     SensorManager;
    public OmmoEsqueletoJogador  Esqueleto;

    [Header("UI de Calibração")]
    public GameObject      PainelCalibracao;
    public TextMeshProUGUI TextoInstrucao;
    public TextMeshProUGUI TextoPasso;
    public TextMeshProUGUI TextoSub;
    public Image           BarraProgressoImagem;

    // ── Parâmetros de estabilidade ────────────────────────────────────
    [Header("Parâmetros")]
    [Tooltip("Tempo necessário de estabilidade para capturar (segundos).")]
    public float TempoEstabilidade = 3.0f;
    [Tooltip("Variação máxima permitida para considerar estável (Unity units). 0.04 = 4 cm.")]
    public float LimiarMovimento = 0.04f;
    [Tooltip("Número de amostras na janela deslizante.")]
    public int NumAmostras = 10;

    // ── Estado interno ────────────────────────────────────────────────
    private EstadoCalibracao _estado = EstadoCalibracao.AguardarSensores;
    // Sempre 1 sensor (palma); ombro nunca tracked em tempo real
    private readonly bool _modoUmSensor = true;

    private OmmoDevice _devicePalma;
    // _deviceOmbro é sempre null — mantido por compatibilidade com OmmoEsqueletoJogador
    private OmmoDevice _deviceOmbro = null;

    private Queue<Vector3> _historicoPos = new Queue<Vector3>();
    private float _tempoEstavel = 0f;
    private bool  _capturando   = false;

    // ── Unity ─────────────────────────────────────────────────────────

    void Start()
    {
        if (SensorManager == null)
            SensorManager = FindObjectOfType<OmmoSensorManager>();

        SensorManager.OnNumeroDeSensoresMudou += AoNumeroDeSensoresMudou;

        // Inicia tracking imediatamente — sempre 1 sensor, sem seleção de modo
        if (SensorManager) SensorManager.IniciarTracking(1);

        AtualizarUI();
    }

    void OnDestroy()
    {
        if (SensorManager)
            SensorManager.OnNumeroDeSensoresMudou -= AoNumeroDeSensoresMudou;
    }

    void Update()
    {
        // Sem processamento enquanto aguarda sensores ou após conclusão
        if (_estado == EstadoCalibracao.AguardarSensores ||
            _estado == EstadoCalibracao.Completo) return;

        OmmoDevice deviceAtivo = SensorParaEstadoAtual();
        if (deviceAtivo == null || deviceAtivo.NumeroSensores == 0) return;

        Vector3 posAtual = deviceAtivo.ObterPosicaoSensor(0);

        // ── Captura manual com Enter ──────────────────────────────────
        if (!_capturando &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            _capturando = true;
            Debug.Log("[OmmoCalibracao] Captura manual (Enter).");
            CapturarEstado(posAtual);
            return;
        }

        // ── Auto-captura por estabilidade ─────────────────────────────
        _historicoPos.Enqueue(posAtual);
        if (_historicoPos.Count > NumAmostras)
            _historicoPos.Dequeue();

        if (_historicoPos.Count < NumAmostras) return;

        float variacaoMax = 0f;
        foreach (var pos in _historicoPos)
            variacaoMax = Mathf.Max(variacaoMax, Vector3.Distance(pos, posAtual));

        if (variacaoMax < LimiarMovimento)
            _tempoEstavel += Time.deltaTime;
        else
            _tempoEstavel = 0f;

        float progresso = Mathf.Clamp01(_tempoEstavel / TempoEstabilidade);
        if (BarraProgressoImagem) BarraProgressoImagem.fillAmount = progresso;

        if (!_capturando && _tempoEstavel >= TempoEstabilidade)
        {
            _capturando = true;
            CapturarEstado(posAtual);
        }
    }

    // ── Deteção de sensores ───────────────────────────────────────────

    void AoNumeroDeSensoresMudou(int count)
    {
        if (_estado != EstadoCalibracao.AguardarSensores) return;
        if (count < 1) return;

        var devices = new List<OmmoDevice>(FindObjectsOfType<OmmoDevice>());
        if (devices.Count < 1) return;

        devices.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        _devicePalma = devices[0];
        _deviceOmbro = null; // sempre null em modo 1 sensor

        Debug.Log($"[OmmoCalibracao] 1 sensor | Palma → {_devicePalma.name}");

        if (Esqueleto)
            Esqueleto.Inicializar(_devicePalma, null);

        AvancarEstado(); // → AtribuirSensores
    }

    // ── Captura de posições ───────────────────────────────────────────

    void CapturarEstado(Vector3 posicao)
    {
        Debug.Log($"[OmmoCalibracao] Capturado {_estado}: {posicao}");

        switch (_estado)
        {
            case EstadoCalibracao.AtribuirSensores:
                break; // apenas confirmação de posicionamento

            case EstadoCalibracao.Ombro:
                if (Esqueleto) Esqueleto.DefinirPosicaoFixa("Ombro", posicao);
                break;

            case EstadoCalibracao.BracoEstendido:
                if (Esqueleto != null)
                {
                    Esqueleto.DefinirComprimentoBraco(
                        Vector3.Distance(posicao, Esqueleto.PosOmbroBase));
                    Esqueleto.DefinirDirecaoFrente(posicao);
                }
                break;
        }

        AvancarEstado();
    }

    // ── Máquina de estados (transições explícitas) ────────────────────

    void AvancarEstado()
    {
        _historicoPos.Clear();
        _tempoEstavel = 0f;
        _capturando   = false;

        if (BarraProgressoImagem) BarraProgressoImagem.fillAmount = 0f;

        _estado = ProximoEstado();

        if (_estado == EstadoCalibracao.Completo)
            ConcluirCalibracao();

        AtualizarUI();
        Debug.Log($"[OmmoCalibracao] → {_estado}");
    }

    EstadoCalibracao ProximoEstado()
    {
        switch (_estado)
        {
            case EstadoCalibracao.AguardarSensores:  return EstadoCalibracao.AtribuirSensores;
            case EstadoCalibracao.AtribuirSensores:  return EstadoCalibracao.Ombro;
            case EstadoCalibracao.Ombro:             return EstadoCalibracao.BracoEstendido;
            case EstadoCalibracao.BracoEstendido:    return EstadoCalibracao.Completo;
            default:                                 return EstadoCalibracao.Completo;
        }
    }

    void ConcluirCalibracao()
    {
        if (Esqueleto) Esqueleto.AtivacaoEsqueleto(true);
        Invoke(nameof(EsconderPainel), 3f);
        Debug.Log("[OmmoCalibracao] ✅ Calibração concluída — esqueleto ativo.");
    }

    void EsconderPainel()
    {
        if (PainelCalibracao) PainelCalibracao.SetActive(false);
    }

    // ── UI ────────────────────────────────────────────────────────────

    void AtualizarUI()
    {
        // Barra de progresso — escondida em estados sem captura automática
        if (BarraProgressoImagem)
        {
            bool mostrarBarra = _estado != EstadoCalibracao.AguardarSensores &&
                                _estado != EstadoCalibracao.Completo;
            BarraProgressoImagem.gameObject.SetActive(mostrarBarra);
        }

        if (TextoInstrucao) TextoInstrucao.text = InstrucaoParaEstado();
        if (TextoSub)       TextoSub.text       = SubtextoParaEstado();
        if (TextoPasso)     TextoPasso.text      = PassoParaEstado();
    }

    string InstrucaoParaEstado()
    {
        switch (_estado)
        {
            case EstadoCalibracao.AguardarSensores:
                return "A aguardar sensor...";
            case EstadoCalibracao.AtribuirSensores:
                return "Coloca o sensor na palma\nda mão";
            case EstadoCalibracao.Ombro:
                return "Toca no ombro\ncom o sensor da palma";
            case EstadoCalibracao.BracoEstendido:
                return "Estica o braço para a frente\nparalelo ao chão";
            case EstadoCalibracao.Completo:
                return "✅ Calibração concluída!";
            default:
                return "";
        }
    }

    string SubtextoParaEstado()
    {
        switch (_estado)
        {
            case EstadoCalibracao.AguardarSensores:
                return "Liga o sensor da palma.";
            case EstadoCalibracao.AtribuirSensores:
                return "Fica parado ou prime Enter quando estiveres pronto.";
            case EstadoCalibracao.Ombro:
                return "Toca no ombro e prime Enter, ou mantém quieto.";
            case EstadoCalibracao.BracoEstendido:
                return "Mantém estável ou prime Enter para capturar.";
            case EstadoCalibracao.Completo:
                return "O esqueleto está ativo e a seguir os teus movimentos.";
            default:
                return "";
        }
    }

    string PassoParaEstado()
    {
        // 2 passos: Ombro, BracoEstendido
        switch (_estado)
        {
            case EstadoCalibracao.Ombro:          return "Passo 1 / 2";
            case EstadoCalibracao.BracoEstendido: return "Passo 2 / 2";
            default:                              return "";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    OmmoDevice SensorParaEstadoAtual()
    {
        switch (_estado)
        {
            case EstadoCalibracao.AtribuirSensores:
            case EstadoCalibracao.Ombro:
            case EstadoCalibracao.BracoEstendido:
            case EstadoCalibracao.Peito:
            case EstadoCalibracao.Cabeca:
                return _devicePalma;
            default:
                return null;
        }
    }

    // ── Propriedades públicas ─────────────────────────────────────────

    /// <summary>Estado atual da calibração.</summary>
    public EstadoCalibracao Estado => _estado;

    /// <summary>True quando a calibração está completa.</summary>
    public bool Calibrado => _estado == EstadoCalibracao.Completo;
}
