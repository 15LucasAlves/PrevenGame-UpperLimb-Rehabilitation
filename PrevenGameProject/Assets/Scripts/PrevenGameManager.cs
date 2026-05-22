using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// PrevenGameManager — Orquestrador do jogo de reabilitação pós-calibração.
///
/// Exercício: Braço ao Lado
///   Palma parte do ombro, desce completamente ao lado do corpo e regressa.
///   Repete N vezes (configurável).
///
/// Waypoints gerados a partir da calibração:
///   WP0 → junto ao ombro (PosOmbroBase)
///   WP1 → meio do braço  (PosOmbroBase - Vector3.up * L * 0.5)
///   WP2 → braço estendido (PosOmbroBase - Vector3.up * L)
///   Volta percorre WP2 → WP1 → WP0
///
/// Linha guia (LineRenderer) liga todos os waypoints da direção atual.
/// HUD mostra repetição atual, cronómetro e contador de compensações.
/// Painel de conclusão mostra estatísticas finais.
/// </summary>
public class PrevenGameManager : MonoBehaviour
{
    // ── Referências (ligadas pelo OmmoSceneBuilder) ───────────────────
    [Header("Referências")]
    public OmmoEsqueletoJogador Esqueleto;
    public OmmoCalibracaoManager CalibracaoManager;

    [Header("UI de Jogo")]
    public Canvas       CanvasJogo;
    public GameObject   HUDJogo;
    public TextMeshProUGUI TextoRepeticao;
    public TextMeshProUGUI TextoTempo;
    public TextMeshProUGUI TextoCompensacao;
    public GameObject   PainelFim;
    public TextMeshProUGUI TextoResultado;
    public TextMeshProUGUI TextoEstatisticas;
    public Button       BotaoRepetir;

    // ── Configuração do exercício ──────────────────────────────────────
    [Header("Exercício — Abdução do Braço")]
    [Tooltip("Número de repetições completas (ida + volta = 1 rep).")]
    public int   NumRepeticoes  = 3;
    [Tooltip("Raio de deteção do toque nos waypoints (Unity units). 0.30 = 30 cm.")]
    public float RaioWaypoint   = 0.30f;
    [Tooltip("Escala visual da esfera-waypoint (diâmetro em Unity units).")]
    public float EscalaEsfera   = 0.45f;
    [Tooltip("Braço direito (+X) ou esquerdo (-X).")]
    public bool  BracoDireito   = true;

    [Header("Cores")]
    public Color CorLinha       = new Color(0.2f, 0.9f, 0.3f, 0.6f);

    // ── Estado interno ─────────────────────────────────────────────────
    private enum EstadoJogo { AguardarCalibracao, EmJogo, Concluido }
    private EstadoJogo _estado = EstadoJogo.AguardarCalibracao;

    // Waypoints da sequência atual
    private PrevenGameWaypoint[] _waypoints;
    private int  _wpAtual    = 0;
    private bool _emVolta    = false; // false = ida (topo→baixo), true = volta (baixo→topo)
    private int  _repAtual   = 0;     // 0-indexed

    // Estatísticas
    private float _tempoTotal       = 0f;
    private int   _contCompensacoes = 0;
    private bool  _ombroCompsLast   = false; // evita contar o mesmo evento várias vezes

    // Linha guia
    private LineRenderer _linhaGuia;

    // Posições dos waypoints (calculadas da calibração)
    // Arco de abdução: [0]=braço baixo … [4]=braço horizontal ao lado
    private Vector3[] _posicoes;

    // ── Unity ──────────────────────────────────────────────────────────

    void Start()
    {
        // Garante referências por Find se o builder não ligou
        if (CalibracaoManager == null)
            CalibracaoManager = FindObjectOfType<OmmoCalibracaoManager>();
        if (Esqueleto == null)
            Esqueleto = FindObjectOfType<OmmoEsqueletoJogador>();

        // AddListener em editor scripts não é serializado — ligar em runtime
        if (BotaoRepetir != null)
            BotaoRepetir.onClick.AddListener(RepetirExercicio);

        // Esconde UI de jogo até a calibração terminar
        if (HUDJogo)    HUDJogo.SetActive(false);
        if (PainelFim)  PainelFim.SetActive(false);
    }

    void Update()
    {
        switch (_estado)
        {
            case EstadoJogo.AguardarCalibracao:
                // Polling — inicia jogo quando calibração concluída
                if (CalibracaoManager != null && CalibracaoManager.Calibrado)
                    IniciarJogo();
                break;

            case EstadoJogo.EmJogo:
                AtualizarJogo();
                break;
        }
    }

    // ── Inicialização do jogo ──────────────────────────────────────────

    void IniciarJogo()
    {
        _estado = EstadoJogo.EmJogo;
        _repAtual   = 0;
        _emVolta    = false;
        _wpAtual    = 0;
        _tempoTotal = 0f;
        _contCompensacoes = 0;

        GerarWaypointsBracoAoLado();
        CriarLinhaGuia();
        IniciarDirecao();

        if (HUDJogo)   HUDJogo.SetActive(true);
        if (PainelFim) PainelFim.SetActive(false);
        AtualizarHUD();

        Debug.Log($"[PrevenGame] Jogo iniciado | {NumRepeticoes} repetições | " +
                  $"ComprimentoBraco={Esqueleto.ComprimentoBraco:F2} units " +
                  $"({Esqueleto.ComprimentoBraco * 10f:F1} cm)");
    }

    // ── Cálculo de waypoints ───────────────────────────────────────────

    void GerarWaypointsBracoAoLado()
    {
        Vector3 posOmbro = Esqueleto.PosOmbroBase;
        float   L        = Esqueleto.ComprimentoBraco;

        if (L < 0.05f)
        {
            L = 0.44f;
            Debug.LogWarning("[PrevenGame] ComprimentoBraco = 0 — usando fallback 44 cm.");
        }

        // Abdução no plano frontal: arco de 0° (braço em baixo) a 90° (braço horizontal)
        // 5 waypoints a cada 22.5° — ida sobe, volta desce
        float lateral = BracoDireito ? 1f : -1f;
        float[] angulos = { 0f, 22.5f, 45f, 67.5f, 90f };

        _posicoes = new Vector3[angulos.Length];
        for (int i = 0; i < angulos.Length; i++)
        {
            float rad = angulos[i] * Mathf.Deg2Rad;
            _posicoes[i] = posOmbro
                + Vector3.right * (lateral * Mathf.Sin(rad) * L)
                + Vector3.down  * (Mathf.Cos(rad) * L);
        }

        // Cria (ou recria) os GameObjects dos waypoints
        if (_waypoints != null)
            foreach (var wp in _waypoints)
                if (wp != null) Destroy(wp.gameObject);

        _waypoints = new PrevenGameWaypoint[_posicoes.Length];
        for (int i = 0; i < _posicoes.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Waypoint_{i}";
            go.transform.position   = _posicoes[i];
            go.transform.localScale = Vector3.one * EscalaEsfera;
            Destroy(go.GetComponent<SphereCollider>());

            var wp = go.AddComponent<PrevenGameWaypoint>();
            wp.Raio = RaioWaypoint;
            _waypoints[i] = wp;
        }

        Debug.Log($"[PrevenGame] Waypoints gerados: " +
                  $"Ombro={_posicoes[0]:F2} | Meio={_posicoes[1]:F2} | Baixo={_posicoes[2]:F2}");
    }

    // ── Linha guia ─────────────────────────────────────────────────────

    void CriarLinhaGuia()
    {
        if (_linhaGuia == null)
        {
            var go = new GameObject("LinhaGuia");
            _linhaGuia = go.AddComponent<LineRenderer>();
            _linhaGuia.material            = new Material(Shader.Find("Sprites/Default"));
            _linhaGuia.startWidth          = 0.025f;
            _linhaGuia.endWidth            = 0.025f;
            _linhaGuia.useWorldSpace       = true;
            _linhaGuia.positionCount       = _posicoes.Length;
        }

        _linhaGuia.startColor = CorLinha;
        _linhaGuia.endColor   = CorLinha;
        AtualizarLinhaGuia();
    }

    void AtualizarLinhaGuia()
    {
        if (_linhaGuia == null || _posicoes == null) return;
        // Linha sempre exibe a sequência da direção atual (ida ou volta)
        _linhaGuia.positionCount = _posicoes.Length;
        if (_emVolta)
        {
            for (int i = 0; i < _posicoes.Length; i++)
                _linhaGuia.SetPosition(i, _posicoes[_posicoes.Length - 1 - i]);
        }
        else
        {
            for (int i = 0; i < _posicoes.Length; i++)
                _linhaGuia.SetPosition(i, _posicoes[i]);
        }
    }

    // ── Máquina de estado do exercício ────────────────────────────────

    /// <summary>Prepara a sequência de waypoints para a direção atual (ida ou volta).</summary>
    void IniciarDirecao()
    {
        _wpAtual = 0;

        // Repõe todos os waypoints
        foreach (var wp in _waypoints) wp.Repor();

        // Ativa o primeiro da direção atual
        int idxAtual = IndiceWaypointAtual();
        _waypoints[idxAtual].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);

        AtualizarLinhaGuia();

        string dir = _emVolta ? "Volta" : "Ida";
        Debug.Log($"[PrevenGame] Rep {_repAtual + 1}/{NumRepeticoes} — {dir} — WP {idxAtual}");
    }

    /// <summary>Converte o índice lógico (_wpAtual) em índice real do array (considera inversão).</summary>
    int IndiceWaypointAtual()
    {
        return _emVolta
            ? (_posicoes.Length - 1 - _wpAtual)
            : _wpAtual;
    }

    // ── Update do jogo ─────────────────────────────────────────────────

    void AtualizarJogo()
    {
        _tempoTotal += Time.deltaTime;

        // Deteção de compensação postural (conta eventos, não frames)
        bool compensando = Esqueleto != null && Esqueleto.OmbroCompensando;
        if (compensando && !_ombroCompsLast)
            _contCompensacoes++;
        _ombroCompsLast = compensando;

        // Verifica toque no waypoint atual
        if (Esqueleto == null || _waypoints == null) return;
        Vector3 posPalma = Esqueleto.ObterPosPalmaAtual();

        int idx = IndiceWaypointAtual();
        if (_waypoints[idx].VerificarToque(posPalma))
            AvancarWaypoint();

        AtualizarHUD();
    }

    void AvancarWaypoint()
    {
        _wpAtual++;

        if (_wpAtual >= _posicoes.Length)
        {
            // Completou todos os waypoints desta direção
            ConcluirDirecao();
        }
        else
        {
            // Ativa próximo waypoint
            int idxProx = IndiceWaypointAtual();
            _waypoints[idxProx].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
            Debug.Log($"[PrevenGame] → WP {idxProx}");
        }
    }

    void ConcluirDirecao()
    {
        if (!_emVolta)
        {
            // Completou a ida → inicia volta
            _emVolta = true;
            _wpAtual = 0;
            foreach (var wp in _waypoints) wp.Repor();
            int idxInicio = IndiceWaypointAtual(); // último índice = topo da volta
            _waypoints[idxInicio].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
            AtualizarLinhaGuia();
            Debug.Log($"[PrevenGame] Ida concluída — a iniciar volta");
        }
        else
        {
            // Completou a volta → fim de repetição
            _repAtual++;
            Debug.Log($"[PrevenGame] Repetição {_repAtual}/{NumRepeticoes} concluída");

            if (_repAtual >= NumRepeticoes)
            {
                ConcluirExercicio();
            }
            else
            {
                // Nova repetição — começa de novo na ida
                _emVolta = false;
                _wpAtual = 0;
                IniciarDirecao();
            }
        }
    }

    // ── Conclusão ──────────────────────────────────────────────────────

    void ConcluirExercicio()
    {
        _estado = EstadoJogo.Concluido;

        // Esconde waypoints e linha
        foreach (var wp in _waypoints) wp.gameObject.SetActive(false);
        if (_linhaGuia) _linhaGuia.gameObject.SetActive(false);
        if (HUDJogo)    HUDJogo.SetActive(false);

        // Mostra painel de resultados
        if (PainelFim) PainelFim.SetActive(true);

        int   minutos   = Mathf.FloorToInt(_tempoTotal / 60f);
        int   segundos  = Mathf.FloorToInt(_tempoTotal % 60f);
        string tempoStr = minutos > 0
            ? $"{minutos}m {segundos:00}s"
            : $"{segundos}s";

        if (TextoResultado)
            TextoResultado.text = "✅ Exercício concluído!";

        if (TextoEstatisticas)
            TextoEstatisticas.text =
                $"Tempo: {tempoStr}   |   Repetições: {NumRepeticoes}   |   Compensações: {_contCompensacoes}";

        Debug.Log($"[PrevenGame] ✅ Exercício concluído! " +
                  $"Tempo={tempoStr} | Reps={NumRepeticoes} | Comps={_contCompensacoes}");
    }

    // ── Botão Repetir ──────────────────────────────────────────────────

    /// <summary>Chamado pelo botão "Repetir" no painel de conclusão.</summary>
    public void RepetirExercicio()
    {
        if (_linhaGuia) _linhaGuia.gameObject.SetActive(true);
        if (PainelFim)  PainelFim.SetActive(false);
        IniciarJogo();
    }

    // ── HUD ────────────────────────────────────────────────────────────

    void AtualizarHUD()
    {
        if (TextoRepeticao)
        {
            // Ida = braço sobe (abdução ↑), Volta = braço desce (adução ↓)
            string dir = _emVolta ? "↓ Descer" : "↑ Subir";
            TextoRepeticao.text = $"Rep {_repAtual + 1} / {NumRepeticoes}  {dir}";
        }

        if (TextoTempo)
        {
            int min = Mathf.FloorToInt(_tempoTotal / 60f);
            int seg = Mathf.FloorToInt(_tempoTotal % 60f);
            TextoTempo.text = min > 0 ? $"{min}:{seg:00}" : $"{seg:00}s";
        }

        if (TextoCompensacao)
            TextoCompensacao.text = $"Compensações: {_contCompensacoes}";
    }
}
