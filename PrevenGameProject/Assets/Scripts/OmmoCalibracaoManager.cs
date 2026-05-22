using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// OmmoCalibracaoManager — Máquina de estados da calibração do esqueleto.
///
/// Fluxo:
///   AguardarSensores → AtribuirSensores → BracoEstendido → Peito → Cabeca → Completo
///
/// Atribuição de sensores (determinística por nome/UUID):
///   OmmoDevice com nome menor alfabeticamente → Sensor A (palma)
///   OmmoDevice com nome maior alfabeticamente → Sensor B (ombro)
///
/// Captura:
///   - Auto: janela deslizante de 10 amostras; variação < 4 cm por 1 s → captura.
///   - Manual: premir Enter/Return captura a posição imediatamente.
/// </summary>
public class OmmoCalibracaoManager : MonoBehaviour
{
    // ── Estados ───────────────────────────────────────────────────────
    public enum EstadoCalibracao
    {
        AguardarSensores,   // espera 2 OmmoDevices activos
        AtribuirSensores,   // instrução sobre onde colocar cada sensor
        BracoEstendido,     // Sensor A: captura palma estendida
        Peito,              // Sensor A: captura peito
        Cabeca,             // Sensor A: captura cabeça
        Completo
    }

    // ── Referências (ligadas pelo OmmoSceneBuilder) ───────────────────
    [Header("Referências")]
    public OmmoSensorManager SensorManager;
    public OmmoEsqueletoJogador Esqueleto;

    [Header("UI de Calibração")]
    public GameObject PainelCalibracao;
    public TextMeshProUGUI TextoInstrucao;
    public TextMeshProUGUI TextoPasso;
    public TextMeshProUGUI TextoSub;
    public Image BarraProgressoImagem;

    // ── Parâmetros de estabilidade ────────────────────────────────────
    [Header("Parâmetros")]
    [Tooltip("Tempo necessário de estabilidade para capturar (segundos).")]
    public float TempoEstabilidade = 1.0f;
    [Tooltip("Variação máxima permitida para considerar estável (Unity units). 0.04 = 4 cm.")]
    public float LimiarMovimento = 0.04f; // 4 cm — tolerante ao tremor natural
    [Tooltip("Número de amostras na janela deslizante.")]
    public int NumAmostras = 10;

    // ── Estado interno ────────────────────────────────────────────────
    private EstadoCalibracao _estado = EstadoCalibracao.AguardarSensores;
    private OmmoDevice _devicePalma;
    private OmmoDevice _deviceOmbro;

    private Queue<Vector3> _historicoPos = new Queue<Vector3>();
    private float _tempoEstavel = 0f;
    private bool _capturando = false;

    // ── Textos por estado ─────────────────────────────────────────────
    private static readonly string[] _instrucoes = new[]
    {
        "A aguardar 2 sensores...",          // AguardarSensores
        "Coloca o sensor 1 na palma\ne o sensor 2 no ombro",  // AtribuirSensores
        "Estica o braço para a frente\nparalelo ao chão",     // BracoEstendido
        "Toca no peito\ncom o sensor da palma",               // Peito
        "Toca na cabeça\ncom o sensor da palma",              // Cabeca
        "✅ Calibração concluída!"           // Completo
    };

    private static readonly string[] _subtextos = new[]
    {
        "Liga o sensor da palma e o sensor do ombro.",
        "Fica parado ou prime Enter quando estiveres pronto.",
        "Mantém estável ou prime Enter para capturar.",
        "Toca no peito e prime Enter, ou mantém quieto.",
        "Toca na cabeça e prime Enter, ou mantém quieto.",
        "O esqueleto está ativo e a seguir os teus movimentos."
    };

    private static readonly string[] _passos = new[]
    {
        "", "", "Passo 1 / 3", "Passo 2 / 3", "Passo 3 / 3", ""
    };

    // ── Unity ─────────────────────────────────────────────────────────

    void Start()
    {
        if (SensorManager == null)
            SensorManager = FindObjectOfType<OmmoSensorManager>();

        SensorManager.OnNumeroDeSensoresMudou += AoNumeroDeSensoresMudou;

        AtualizarUI();
    }

    void OnDestroy()
    {
        if (SensorManager)
            SensorManager.OnNumeroDeSensoresMudou -= AoNumeroDeSensoresMudou;
    }

    void Update()
    {
        if (_estado == EstadoCalibracao.AguardarSensores ||
            _estado == EstadoCalibracao.Completo) return;

        // Sensor ativo para o estado atual
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

        // Janela deslizante de posições
        _historicoPos.Enqueue(posAtual);
        if (_historicoPos.Count > NumAmostras)
            _historicoPos.Dequeue();

        if (_historicoPos.Count < NumAmostras) return;

        // Calcula variação máxima na janela
        float variacaoMax = 0f;
        foreach (var pos in _historicoPos)
            variacaoMax = Mathf.Max(variacaoMax, Vector3.Distance(pos, posAtual));

        if (variacaoMax < LimiarMovimento)
            _tempoEstavel += Time.deltaTime;
        else
            _tempoEstavel = 0f;

        // Atualiza barra de progresso
        float progresso = Mathf.Clamp01(_tempoEstavel / TempoEstabilidade);
        if (BarraProgressoImagem) BarraProgressoImagem.fillAmount = progresso;

        // Captura quando estável tempo suficiente
        if (!_capturando && _tempoEstavel >= TempoEstabilidade)
        {
            _capturando = true;
            CapturarEstado(posAtual);
        }
    }

    // ── Deteção de sensores ───────────────────────────────────────────

    void AoNumeroDeSensoresMudou(int count)
    {
        if (count >= 2 && _estado == EstadoCalibracao.AguardarSensores)
        {
            // Ordena por nome (que contém UUID) → atribuição determinística
            var devices = new List<OmmoDevice>(FindObjectsOfType<OmmoDevice>());
            if (devices.Count < 2) return;

            devices.Sort((a, b) => string.Compare(a.name, b.name,
                System.StringComparison.Ordinal));

            _devicePalma = devices[0];
            _deviceOmbro = devices[1];

            Debug.Log($"[OmmoCalibracao] Sensores atribuídos:" +
                      $"\n  Palma → {_devicePalma.name}" +
                      $"\n  Ombro → {_deviceOmbro.name}");

            if (Esqueleto)
                Esqueleto.Inicializar(_devicePalma, _deviceOmbro);

            AvancarEstado(); // → AtribuirSensores
        }
    }

    // ── Captura de posições ───────────────────────────────────────────

    void CapturarEstado(Vector3 posicao)
    {
        Debug.Log($"[OmmoCalibracao] Capturado {_estado}: {posicao}");

        switch (_estado)
        {
            case EstadoCalibracao.AtribuirSensores:
                // Sem captura de posição — apenas confirma posicionamento
                break;

            case EstadoCalibracao.BracoEstendido:
                // A posição da palma estendida fica guardada no Esqueleto
                // via live tracking — não é necessário guardar aqui
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

    // ── Máquina de estados ────────────────────────────────────────────

    void AvancarEstado()
    {
        _historicoPos.Clear();
        _tempoEstavel = 0f;
        _capturando   = false;

        if (BarraProgressoImagem) BarraProgressoImagem.fillAmount = 0f;

        _estado = (EstadoCalibracao)((int)_estado + 1);

        if (_estado == EstadoCalibracao.Completo)
            ConcluirCalibracao();

        AtualizarUI();
        Debug.Log($"[OmmoCalibracao] → {_estado}");
    }

    void ConcluirCalibracao()
    {
        if (Esqueleto) Esqueleto.AtivacaoEsqueleto(true);

        // Esconde o painel após 3 segundos
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
        int idx = (int)_estado;
        if (idx < 0 || idx >= _instrucoes.Length) return;

        if (TextoInstrucao) TextoInstrucao.text = _instrucoes[idx];
        if (TextoSub)       TextoSub.text       = _subtextos[idx];
        if (TextoPasso)     TextoPasso.text      = _passos[idx];
        if (BarraProgressoImagem)
        {
            // Esconde a barra nos estados sem captura
            bool mostrarBarra = _estado != EstadoCalibracao.AguardarSensores &&
                                _estado != EstadoCalibracao.Completo;
            BarraProgressoImagem.gameObject.SetActive(mostrarBarra);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>Devolve o sensor ativo para o estado atual de calibração.</summary>
    OmmoDevice SensorParaEstadoAtual()
    {
        switch (_estado)
        {
            case EstadoCalibracao.AtribuirSensores:
                // Ambos os sensores têm de estar parados → usar palma como referência
                return _devicePalma;
            case EstadoCalibracao.BracoEstendido:
            case EstadoCalibracao.Peito:
            case EstadoCalibracao.Cabeca:
                return _devicePalma;
            default:
                return null;
        }
    }

    // ── Propriedade pública ───────────────────────────────────────────

    /// <summary>Estado atual da calibração.</summary>
    public EstadoCalibracao Estado => _estado;

    /// <summary>True quando a calibração está completa.</summary>
    public bool Calibrado => _estado == EstadoCalibracao.Completo;
}
