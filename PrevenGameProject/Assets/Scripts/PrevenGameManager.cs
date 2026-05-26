using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// PrevenGameManager — Orquestrador do jogo de reabilitação pós-calibração.
///
/// Fluxo geral:
///   1. AguardarCalibracao  — polling até CalibracaoManager.Calibrado ser true
///   2. AguardarSelecao     — mostra ecrã de seleção de exercícios
///   3. EmJogo              — executa a fila de exercícios em sequência
///   4. Concluido           — painel de resultados; "Nova Sessão" volta à seleção
///
/// Sistema de pontuação (anéis concêntricos):
///   A palma ENTRA na zona externa → rastreia a menor distância atingida.
///   Quando SAI da zona → score determinado pelo ponto mais próximo atingido.
///   Zona interior (100 %) → anel médio (75 %) → anel exterior (50 %).
/// </summary>
public class PrevenGameManager : MonoBehaviour
{
    // ── Tipos ─────────────────────────────────────────────────────────

    public enum TipoExercicio { FlexaoBraco = 0, Exercicio2 = 1, Exercicio3 = 2, Exercicio4 = 3 }

    [System.Serializable]
    public struct ExercicioConfig
    {
        public TipoExercicio Tipo;
        public int           NumRepeticoes;
    }

    // ── Referências (ligadas pelo OmmoSceneBuilder) ───────────────────
    [Header("Referências")]
    public OmmoEsqueletoJogador  Esqueleto;
    public OmmoCalibracaoManager CalibracaoManager;

    // ── UI de Jogo ────────────────────────────────────────────────────
    [Header("UI de Jogo")]
    public Canvas            CanvasJogo;
    public GameObject        HUDJogo;
    public TextMeshProUGUI   TextoRepeticao;
    public TextMeshProUGUI   TextoTempo;
    public TextMeshProUGUI   TextoCompensacao;
    public GameObject        PainelFim;
    public TextMeshProUGUI   TextoResultado;
    public TextMeshProUGUI   TextoEstatisticas;
    public Button            BotaoNovaSessao; // antigo BotaoRepetir → volta à seleção

    // ── UI — Seleção de Exercícios ────────────────────────────────────
    [Header("UI — Seleção de Exercícios")]
    public GameObject        PainelSelecaoExercicio;
    public Button[]          BotoesToggleExercicio;  // [4] toggle por exercício
    public TextMeshProUGUI[] TextosRepsExercicio;    // [4] contador de reps
    public Button[]          BotoesMenos;            // [4] botão "─"
    public Button[]          BotoesMais;             // [4] botão "+"
    public Button            BotaoIniciarSessao;

    // ── Configuração do exercício ──────────────────────────────────────
    [Header("Exercício")]
    [Tooltip("Número de repetições (sobrescrito pela seleção de exercícios).")]
    public int   NumRepeticoes = 3;
    [Tooltip("Escala visual da esfera-waypoint central (diâmetro em Unity units).")]
    public float EscalaEsfera  = 0.45f;

    // ── Zonas de Pontuação ────────────────────────────────────────────
    [Header("Zonas de Pontuação")]
    [Tooltip("Raios das zonas de pontuação, interior→exterior (Unity units).")]
    public float[] RaiosZonas      = { 0.20f, 0.35f, 0.50f };
    [Tooltip("Score de cada zona, interior→exterior (0–1).")]
    public float[] PontuacoesZonas = { 1.00f, 0.75f, 0.50f };
    [Tooltip("Cores das zonas (índice 0=interior=verde, …=exterior=laranja).")]
    public Color[] CoresZonas      =
    {
        new Color(0.2f, 0.9f, 0.3f),  // 100 % — verde
        new Color(1.0f, 0.85f, 0.0f), // 75 %  — amarelo
        new Color(1.0f, 0.4f,  0.1f)  // 50 %  — laranja
    };

    [Header("Cores")]
    public Color CorLinha = new Color(0.2f, 0.9f, 0.3f, 0.6f);

    // ── Estado da máquina de jogo ──────────────────────────────────────
    private enum EstadoJogo { AguardarCalibracao, AguardarSelecao, EmJogo, Concluido }
    private EstadoJogo _estado = EstadoJogo.AguardarCalibracao;

    // ── Fila de exercícios ─────────────────────────────────────────────
    private Queue<ExercicioConfig> _filaExercicios = new Queue<ExercicioConfig>();

    // ── Seleção ────────────────────────────────────────────────────────
    private bool[] _selecionados   = { true, false, false, false }; // Exercício 1 pré-selecionado
    private int[]  _repsEscolhidas = { 3, 3, 3, 3 };

    // ── Waypoints da sequência atual ───────────────────────────────────
    private PrevenGameWaypoint[] _waypoints;
    private int  _wpAtual  = 0;
    private bool _emVolta  = false;
    private int  _repAtual = 0;

    // ── Estatísticas da sessão ─────────────────────────────────────────
    private float _tempoTotal          = 0f;
    private int   _contCompensacoes    = 0;
    private bool  _ombroCompsLast      = false;
    private float _pontuacaoAcumulada  = 0f;
    private int   _waypointsAtingidos  = 0;
    private int   _totalRepsRealizadas = 0;

    // ── Linha guia ─────────────────────────────────────────────────────
    private LineRenderer _linhaGuia;

    // ── Posições dos waypoints ─────────────────────────────────────────
    private Vector3[] _posicoes;

    // ─────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────

    void Start()
    {
        if (CalibracaoManager == null)
            CalibracaoManager = FindObjectOfType<OmmoCalibracaoManager>();
        if (Esqueleto == null)
            Esqueleto = FindObjectOfType<OmmoEsqueletoJogador>();

        // Wiring de todos os listeners em runtime (onClick não é serializado)
        BotaoNovaSessao?.onClick.AddListener(MostrarSelecao);
        BotaoIniciarSessao?.onClick.AddListener(IniciarSessaoSelecionada);

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            if (BotoesToggleExercicio != null && idx < BotoesToggleExercicio.Length)
                BotoesToggleExercicio[idx]?.onClick.AddListener(() => ToggleExercicio(idx));
            if (BotoesMenos != null && idx < BotoesMenos.Length)
                BotoesMenos[idx]?.onClick.AddListener(() => AlterarReps(idx, -1));
            if (BotoesMais != null && idx < BotoesMais.Length)
                BotoesMais[idx]?.onClick.AddListener(() => AlterarReps(idx, +1));
        }

        if (HUDJogo)                HUDJogo.SetActive(false);
        if (PainelFim)              PainelFim.SetActive(false);
        if (PainelSelecaoExercicio) PainelSelecaoExercicio.SetActive(false);
    }

    void Update()
    {
        switch (_estado)
        {
            case EstadoJogo.AguardarCalibracao:
                if (CalibracaoManager != null && CalibracaoManager.Calibrado)
                    MostrarSelecao();
                break;

            case EstadoJogo.EmJogo:
                AtualizarJogo();
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Ecrã de seleção de exercícios
    // ─────────────────────────────────────────────────────────────────

    void MostrarSelecao()
    {
        _estado = EstadoJogo.AguardarSelecao;

        if (HUDJogo)                HUDJogo.SetActive(false);
        if (PainelFim)              PainelFim.SetActive(false);
        if (PainelSelecaoExercicio) PainelSelecaoExercicio.SetActive(true);

        // Limpa waypoints e linha da sessão anterior (se existirem)
        if (_waypoints != null)
        {
            foreach (var wp in _waypoints)
                if (wp != null) Destroy(wp.gameObject);
            _waypoints = null;
        }
        if (_linhaGuia) _linhaGuia.gameObject.SetActive(false);

        AtualizarUISelecao();
        Debug.Log("[PrevenGame] Ecrã de seleção ativo.");
    }

    void AtualizarUISelecao()
    {
        for (int i = 0; i < 4; i++)
        {
            bool sel = _selecionados[i];

            if (BotoesToggleExercicio != null && i < BotoesToggleExercicio.Length
                && BotoesToggleExercicio[i] != null)
            {
                var img = BotoesToggleExercicio[i].GetComponent<Image>();
                if (img)
                    img.color = sel
                        ? new Color(0.2f, 0.7f, 0.3f)
                        : new Color(0.35f, 0.35f, 0.35f);
            }

            if (TextosRepsExercicio != null && i < TextosRepsExercicio.Length
                && TextosRepsExercicio[i] != null)
                TextosRepsExercicio[i].text = _repsEscolhidas[i].ToString();
        }
    }

    void ToggleExercicio(int idx)
    {
        // Apenas os exercícios implementados (índice 0) podem ser toggleados
        if (idx != 0) return;
        _selecionados[idx] = !_selecionados[idx];
        AtualizarUISelecao();
    }

    void AlterarReps(int idx, int delta)
    {
        if (!_selecionados[idx]) return;
        _repsEscolhidas[idx] = Mathf.Clamp(_repsEscolhidas[idx] + delta, 1, 20);
        AtualizarUISelecao();
    }

    void IniciarSessaoSelecionada()
    {
        bool algumSelecionado = false;
        for (int i = 0; i < 4; i++)
            if (_selecionados[i]) { algumSelecionado = true; break; }

        if (!algumSelecionado)
        {
            Debug.LogWarning("[PrevenGame] Nenhum exercício selecionado.");
            return;
        }

        _filaExercicios.Clear();
        for (int i = 0; i < 4; i++)
        {
            if (_selecionados[i])
                _filaExercicios.Enqueue(new ExercicioConfig
                {
                    Tipo          = (TipoExercicio)i,
                    NumRepeticoes = _repsEscolhidas[i]
                });
        }

        // Reseta estatísticas de sessão
        _pontuacaoAcumulada  = 0f;
        _waypointsAtingidos  = 0;
        _totalRepsRealizadas = 0;
        _tempoTotal          = 0f;
        _contCompensacoes    = 0;
        _ombroCompsLast      = false;

        if (PainelSelecaoExercicio) PainelSelecaoExercicio.SetActive(false);

        AvancarFila();
    }

    void AvancarFila()
    {
        if (_filaExercicios.Count > 0)
        {
            var config = _filaExercicios.Dequeue();
            IniciarExercicio(config);
        }
        else
        {
            ConcluirSessao();
        }
    }

    void IniciarExercicio(ExercicioConfig config)
    {
        NumRepeticoes = config.NumRepeticoes;
        Debug.Log($"[PrevenGame] Iniciar: {config.Tipo} × {config.NumRepeticoes} reps");
        IniciarJogo();
    }

    // ─────────────────────────────────────────────────────────────────
    // Inicialização do exercício corrente
    // ─────────────────────────────────────────────────────────────────

    void IniciarJogo()
    {
        _estado   = EstadoJogo.EmJogo;
        _repAtual = 0;
        _emVolta  = false;
        _wpAtual  = 0;

        GerarWaypointsBracoAoLado();
        CriarLinhaGuia();
        IniciarDirecao();

        if (HUDJogo)   HUDJogo.SetActive(true);
        if (PainelFim) PainelFim.SetActive(false);
        AtualizarHUD();

        Debug.Log($"[PrevenGame] Jogo iniciado | {NumRepeticoes} reps | " +
                  $"Braço={Esqueleto.ComprimentoBraco:F2} u ({Esqueleto.ComprimentoBraco * 10f:F1} cm)");
    }

    // ─────────────────────────────────────────────────────────────────
    // Cálculo de waypoints — arco sagital
    // ─────────────────────────────────────────────────────────────────

    void GerarWaypointsBracoAoLado()
    {
        Vector3 posOmbro = Esqueleto.PosOmbroBase;
        float   L        = Esqueleto.ComprimentoBraco;

        if (L < 0.05f)
        {
            L = 0.44f;
            Debug.LogWarning("[PrevenGame] ComprimentoBraco = 0 — usando fallback 44 cm.");
        }

        Vector3 dirFrente = Esqueleto.DirecaoFrente;
        if (dirFrente == Vector3.zero) dirFrente = Vector3.forward;

        // 5 waypoints: braço em baixo (0°) → braço horizontal à frente (90°)
        float[] angulos = { 0f, 22.5f, 45f, 67.5f, 90f };

        _posicoes = new Vector3[angulos.Length];
        for (int i = 0; i < angulos.Length; i++)
        {
            float rad = angulos[i] * Mathf.Deg2Rad;
            _posicoes[i] = posOmbro
                + dirFrente    * (Mathf.Sin(rad) * L)
                + Vector3.down * (Mathf.Cos(rad) * L);
        }

        // Cria (ou recria) GameObjects dos waypoints
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
            wp.ConfigurarZonas(RaiosZonas, PontuacoesZonas, CoresZonas, EscalaEsfera);
            _waypoints[i] = wp;
        }

        Debug.Log($"[PrevenGame] DirFrente={dirFrente} | WP[0]={_posicoes[0]:F2} WP[4]={_posicoes[4]:F2}");
    }

    // ─────────────────────────────────────────────────────────────────
    // Linha guia
    // ─────────────────────────────────────────────────────────────────

    void CriarLinhaGuia()
    {
        if (_linhaGuia == null)
        {
            var go = new GameObject("LinhaGuia");
            _linhaGuia = go.AddComponent<LineRenderer>();
            _linhaGuia.material      = new Material(Shader.Find("Sprites/Default"));
            _linhaGuia.startWidth    = 0.025f;
            _linhaGuia.endWidth      = 0.025f;
            _linhaGuia.useWorldSpace = true;
        }

        _linhaGuia.gameObject.SetActive(true);
        _linhaGuia.startColor = CorLinha;
        _linhaGuia.endColor   = CorLinha;
        AtualizarLinhaGuia();
    }

    void AtualizarLinhaGuia()
    {
        if (_linhaGuia == null || _posicoes == null) return;
        _linhaGuia.positionCount = _posicoes.Length;
        if (_emVolta)
            for (int i = 0; i < _posicoes.Length; i++)
                _linhaGuia.SetPosition(i, _posicoes[_posicoes.Length - 1 - i]);
        else
            for (int i = 0; i < _posicoes.Length; i++)
                _linhaGuia.SetPosition(i, _posicoes[i]);
    }

    // ─────────────────────────────────────────────────────────────────
    // Máquina de estado do exercício
    // ─────────────────────────────────────────────────────────────────

    void IniciarDirecao()
    {
        _wpAtual = 0;
        foreach (var wp in _waypoints) wp.Repor();

        int idxAtual = IndiceWaypointAtual();
        _waypoints[idxAtual].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
        AtualizarLinhaGuia();

        string dir = _emVolta ? "Volta" : "Ida";
        Debug.Log($"[PrevenGame] Rep {_repAtual + 1}/{NumRepeticoes} — {dir} — WP {idxAtual}");
    }

    int IndiceWaypointAtual()
        => _emVolta ? (_posicoes.Length - 1 - _wpAtual) : _wpAtual;

    // ─────────────────────────────────────────────────────────────────
    // Update do jogo
    // ─────────────────────────────────────────────────────────────────

    void AtualizarJogo()
    {
        _tempoTotal += Time.deltaTime;

        bool compensando = Esqueleto != null && Esqueleto.OmbroCompensando;
        if (compensando && !_ombroCompsLast) _contCompensacoes++;
        _ombroCompsLast = compensando;

        if (Esqueleto == null || _waypoints == null) return;

        Vector3 posPalma = Esqueleto.ObterPosPalmaAtual();
        int     idx      = IndiceWaypointAtual();

        if (_waypoints[idx].VerificarToque(posPalma))
        {
            _pontuacaoAcumulada += _waypoints[idx].UltimaPontuacao;
            _waypointsAtingidos++;

            float pctWp = _waypoints[idx].UltimaPontuacao * 100f;
            Debug.Log($"[PrevenGame] WP {idx} concluído — {pctWp:F0} %");

            AvancarWaypoint();
        }

        AtualizarHUD();
    }

    void AvancarWaypoint()
    {
        _wpAtual++;

        if (_wpAtual >= _posicoes.Length)
        {
            ConcluirDirecao();
        }
        else
        {
            int idxProx = IndiceWaypointAtual();
            _waypoints[idxProx].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
            Debug.Log($"[PrevenGame] → WP {idxProx}");
        }
    }

    void ConcluirDirecao()
    {
        if (!_emVolta)
        {
            _emVolta = true;
            _wpAtual = 0;
            foreach (var wp in _waypoints) wp.Repor();
            int idxInicio = IndiceWaypointAtual();
            _waypoints[idxInicio].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
            AtualizarLinhaGuia();
            Debug.Log("[PrevenGame] Ida concluída → a iniciar volta");
        }
        else
        {
            _repAtual++;
            _totalRepsRealizadas++;
            Debug.Log($"[PrevenGame] Rep {_repAtual}/{NumRepeticoes} concluída");

            if (_repAtual >= NumRepeticoes)
            {
                EsconderWaypointsAtivos();

                if (_filaExercicios.Count > 0)
                {
                    Debug.Log("[PrevenGame] → Próximo exercício na fila…");
                    AvancarFila();
                }
                else
                {
                    ConcluirSessao();
                }
            }
            else
            {
                _emVolta = false;
                _wpAtual = 0;
                IniciarDirecao();
            }
        }
    }

    void EsconderWaypointsAtivos()
    {
        if (_waypoints != null)
            foreach (var wp in _waypoints)
                if (wp != null) wp.gameObject.SetActive(false);
        if (_linhaGuia) _linhaGuia.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────
    // Conclusão da sessão completa
    // ─────────────────────────────────────────────────────────────────

    void ConcluirSessao()
    {
        _estado = EstadoJogo.Concluido;

        EsconderWaypointsAtivos();
        if (HUDJogo)   HUDJogo.SetActive(false);
        if (PainelFim) PainelFim.SetActive(true);

        int    min      = Mathf.FloorToInt(_tempoTotal / 60f);
        int    seg      = Mathf.FloorToInt(_tempoTotal % 60f);
        string tempoStr = min > 0 ? $"{min}m {seg:00}s" : $"{seg}s";

        float pct = _waypointsAtingidos > 0
            ? (_pontuacaoAcumulada / _waypointsAtingidos) * 100f
            : 0f;

        if (TextoResultado)
            TextoResultado.text = "✅ Sessão concluída!";

        if (TextoEstatisticas)
            TextoEstatisticas.text =
                $"Tempo: {tempoStr}   |   Reps: {_totalRepsRealizadas}   |   " +
                $"Compensações: {_contCompensacoes}   |   Score: {pct:F0} %";

        Debug.Log($"[PrevenGame] ✅ Sessão concluída! Tempo={tempoStr} | " +
                  $"Reps={_totalRepsRealizadas} | Comps={_contCompensacoes} | Score={pct:F0}%");
    }

    // ─────────────────────────────────────────────────────────────────
    // HUD
    // ─────────────────────────────────────────────────────────────────

    void AtualizarHUD()
    {
        if (TextoRepeticao)
        {
            string dir = _emVolta ? "↓ Descer" : "↑ Subir";
            TextoRepeticao.text = $"Rep {_repAtual + 1} / {NumRepeticoes}  {dir}";
        }

        if (TextoTempo)
        {
            int m = Mathf.FloorToInt(_tempoTotal / 60f);
            int s = Mathf.FloorToInt(_tempoTotal % 60f);
            TextoTempo.text = m > 0 ? $"{m}:{s:00}" : $"{s:00}s";
        }

        if (TextoCompensacao)
            TextoCompensacao.text = $"Compensações: {_contCompensacoes}";
    }
}
