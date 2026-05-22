using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// OmmoCalibracaoManager — Máquina de estados da calibração do esqueleto.
///
/// Modos:
///   1 Sensor (palma) — ombro capturado manualmente num passo extra
///   2 Sensores (palma + ombro) — ombro tracked em tempo real
///
/// Fluxo 1 sensor:
///   EscolherModo → AguardarSensores → AtribuirSensores
///     → Ombro → BracoEstendido → Peito → Cabeca → Completo
///
/// Fluxo 2 sensores:
///   EscolherModo → AguardarSensores → AtribuirSensores
///     → BracoEstendido → Peito → Cabeca → Completo
///
/// Captura:
///   Auto: janela deslizante de 10 amostras, variação < 4 cm por 1 s.
///   Manual: premir Enter/Return captura imediatamente.
/// </summary>
public class OmmoCalibracaoManager : MonoBehaviour
{
    // ── Estados ───────────────────────────────────────────────────────
    public enum EstadoCalibracao
    {
        EscolherModo,       // seleção 1/2 sensores (botões)
        AguardarSensores,   // espera 1 ou 2 OmmoDevices
        AtribuirSensores,   // instrução de colocação dos sensores
        Ombro,              // só modo 1 sensor: toca no ombro com a palma
        BracoEstendido,     // Sensor A: braço estendido (mede comprimento)
        Peito,              // Sensor A: toca no peito
        Cabeca,             // Sensor A: toca na cabeça
        Completo
    }

    // ── Referências (ligadas pelo OmmoSceneBuilder) ───────────────────
    [Header("Referências")]
    public OmmoSensorManager SensorManager;
    public OmmoEsqueletoJogador Esqueleto;

    [Header("UI de Calibração")]
    public GameObject        PainelCalibracao;
    public TextMeshProUGUI   TextoInstrucao;
    public TextMeshProUGUI   TextoPasso;
    public TextMeshProUGUI   TextoSub;
    public Image             BarraProgressoImagem;
    public GameObject        PainelModoSensor;   // painel com os 2 botões de seleção

    // ── Parâmetros de estabilidade ────────────────────────────────────
    [Header("Parâmetros")]
    [Tooltip("Tempo necessário de estabilidade para capturar (segundos).")]
    public float TempoEstabilidade = 1.0f;
    [Tooltip("Variação máxima permitida para considerar estável (Unity units). 0.04 = 4 cm.")]
    public float LimiarMovimento = 0.04f;
    [Tooltip("Número de amostras na janela deslizante.")]
    public int NumAmostras = 10;

    // ── Estado interno ────────────────────────────────────────────────
    private EstadoCalibracao _estado = EstadoCalibracao.EscolherModo;
    private bool _modoUmSensor = false;

    private OmmoDevice _devicePalma;
    private OmmoDevice _deviceOmbro; // null em modo 1 sensor

    private Queue<Vector3> _historicoPos = new Queue<Vector3>();
    private float _tempoEstavel = 0f;
    private bool  _capturando   = false;

    // ── Unity ─────────────────────────────────────────────────────────

    void Start()
    {
        if (SensorManager == null)
            SensorManager = FindObjectOfType<OmmoSensorManager>();

        SensorManager.OnNumeroDeSensoresMudou += AoNumeroDeSensoresMudou;

        // AddListener em editor scripts não é serializado na cena —
        // os botões precisam de ser ligados em runtime no Start().
        if (PainelModoSensor != null)
        {
            var btn1 = PainelModoSensor.transform.Find("Botao1Sensor")?.GetComponent<Button>();
            var btn2 = PainelModoSensor.transform.Find("Botao2Sensores")?.GetComponent<Button>();
            if (btn1) btn1.onClick.AddListener(EscolherModo1Sensor);
            if (btn2) btn2.onClick.AddListener(EscolherModo2Sensores);
        }

        AtualizarUI();
    }

    void OnDestroy()
    {
        if (SensorManager)
            SensorManager.OnNumeroDeSensoresMudou -= AoNumeroDeSensoresMudou;
    }

    void Update()
    {
        // Sem processamento enquanto aguarda modo/sensores ou após conclusão
        if (_estado == EstadoCalibracao.EscolherModo   ||
            _estado == EstadoCalibracao.AguardarSensores ||
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

    // ── Seleção de modo (chamada pelos botões da UI) ──────────────────

    /// <summary>Selecionado pelo botão "1 Sensor" na UI.</summary>
    public void EscolherModo1Sensor()
    {
        _modoUmSensor = true;
        Debug.Log("[OmmoCalibracao] Modo: 1 sensor");
        if (SensorManager) SensorManager.IniciarTracking(1);
        AvancarEstado(); // → AguardarSensores
    }

    /// <summary>Selecionado pelo botão "2 Sensores" na UI.</summary>
    public void EscolherModo2Sensores()
    {
        _modoUmSensor = false;
        Debug.Log("[OmmoCalibracao] Modo: 2 sensores");
        if (SensorManager) SensorManager.IniciarTracking(2);
        AvancarEstado(); // → AguardarSensores
    }

    // ── Deteção de sensores ───────────────────────────────────────────

    void AoNumeroDeSensoresMudou(int count)
    {
        if (_estado != EstadoCalibracao.AguardarSensores) return;

        int sensoresNecessarios = _modoUmSensor ? 1 : 2;
        if (count < sensoresNecessarios) return;

        var devices = new List<OmmoDevice>(FindObjectsOfType<OmmoDevice>());
        if (devices.Count < sensoresNecessarios) return;

        devices.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        _devicePalma = devices[0];
        _deviceOmbro = _modoUmSensor ? null : devices[1];

        if (_modoUmSensor)
            Debug.Log($"[OmmoCalibracao] 1 sensor | Palma → {_devicePalma.name}");
        else
            Debug.Log($"[OmmoCalibracao] 2 sensores | Palma → {_devicePalma.name} | Ombro → {_deviceOmbro.name}");

        if (Esqueleto)
            Esqueleto.Inicializar(_devicePalma, _deviceOmbro);

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
                // Modo 1 sensor: guarda posição fixa do ombro
                if (Esqueleto) Esqueleto.DefinirPosicaoFixa("Ombro", posicao);
                break;

            case EstadoCalibracao.BracoEstendido:
                // Mede comprimento real do braço (palma → ombro)
                if (Esqueleto != null)
                {
                    Vector3 posOmbro = _deviceOmbro != null
                        ? _deviceOmbro.ObterPosicaoSensor(0)
                        : Esqueleto.PosOmbroBase; // fixo do passo Ombro (1 sensor)
                    Esqueleto.DefinirComprimentoBraco(Vector3.Distance(posicao, posOmbro));
                }
                break;

            case EstadoCalibracao.Peito:
                if (Esqueleto) Esqueleto.DefinirPosicaoFixa("Peito", posicao);
                break;

            case EstadoCalibracao.Cabeca:
                if (Esqueleto) Esqueleto.DefinirPosicaoFixa("Cabeca", posicao);
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
            case EstadoCalibracao.EscolherModo:     return EstadoCalibracao.AguardarSensores;
            case EstadoCalibracao.AguardarSensores: return EstadoCalibracao.AtribuirSensores;
            case EstadoCalibracao.AtribuirSensores:
                return _modoUmSensor
                    ? EstadoCalibracao.Ombro
                    : EstadoCalibracao.BracoEstendido;
            case EstadoCalibracao.Ombro:            return EstadoCalibracao.BracoEstendido;
            case EstadoCalibracao.BracoEstendido:   return EstadoCalibracao.Peito;
            case EstadoCalibracao.Peito:            return EstadoCalibracao.Cabeca;
            case EstadoCalibracao.Cabeca:           return EstadoCalibracao.Completo;
            default:                                return EstadoCalibracao.Completo;
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
        // Painel de seleção de modo — só visível no estado inicial
        if (PainelModoSensor)
            PainelModoSensor.SetActive(_estado == EstadoCalibracao.EscolherModo);

        // Barra de progresso — escondida em estados sem captura
        if (BarraProgressoImagem)
        {
            bool mostrarBarra = _estado != EstadoCalibracao.EscolherModo    &&
                                _estado != EstadoCalibracao.AguardarSensores &&
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
            case EstadoCalibracao.EscolherModo:
                return "Quantos sensores tens?";
            case EstadoCalibracao.AguardarSensores:
                return _modoUmSensor
                    ? "A aguardar 1 sensor..."
                    : "A aguardar 2 sensores...";
            case EstadoCalibracao.AtribuirSensores:
                return _modoUmSensor
                    ? "Coloca o sensor na palma\nda mão"
                    : "Coloca o sensor 1 na palma\ne o sensor 2 no ombro";
            case EstadoCalibracao.Ombro:
                return "Toca no ombro\ncom o sensor da palma";
            case EstadoCalibracao.BracoEstendido:
                return "Estica o braço para a frente\nparalelo ao chão";
            case EstadoCalibracao.Peito:
                return "Toca no peito\ncom o sensor da palma";
            case EstadoCalibracao.Cabeca:
                return "Toca na cabeça\ncom o sensor da palma";
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
            case EstadoCalibracao.EscolherModo:
                return "Escolhe o modo de rastreamento.";
            case EstadoCalibracao.AguardarSensores:
                return _modoUmSensor
                    ? "Liga o sensor da palma."
                    : "Liga o sensor da palma e o sensor do ombro.";
            case EstadoCalibracao.AtribuirSensores:
                return "Fica parado ou prime Enter quando estiveres pronto.";
            case EstadoCalibracao.Ombro:
                return "Toca no ombro e prime Enter, ou mantém quieto.";
            case EstadoCalibracao.BracoEstendido:
                return "Mantém estável ou prime Enter para capturar.";
            case EstadoCalibracao.Peito:
                return "Toca no peito e prime Enter, ou mantém quieto.";
            case EstadoCalibracao.Cabeca:
                return "Toca na cabeça e prime Enter, ou mantém quieto.";
            case EstadoCalibracao.Completo:
                return "O esqueleto está ativo e a seguir os teus movimentos.";
            default:
                return "";
        }
    }

    string PassoParaEstado()
    {
        // Número total de passos varia com o modo
        int total = _modoUmSensor ? 4 : 3;
        switch (_estado)
        {
            case EstadoCalibracao.Ombro:         return $"Passo 1 / {total}";
            case EstadoCalibracao.BracoEstendido: return _modoUmSensor ? "Passo 2 / 4" : "Passo 1 / 3";
            case EstadoCalibracao.Peito:          return _modoUmSensor ? "Passo 3 / 4" : "Passo 2 / 3";
            case EstadoCalibracao.Cabeca:         return _modoUmSensor ? "Passo 4 / 4" : "Passo 3 / 3";
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

    /// <summary>True quando o modo de 1 sensor está ativo.</summary>
    public bool ModoUmSensor => _modoUmSensor;
}
