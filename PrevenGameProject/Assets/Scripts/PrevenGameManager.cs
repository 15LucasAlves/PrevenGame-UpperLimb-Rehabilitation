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

    public enum TipoExercicio { FlexaoBraco = 0, ElevacaoTotal = 1, AbducaoLateral = 2, FlexaoCotovelo = 3 }

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
    public Button            BotaoBracoDireito;
    public Button            BotaoBracoEsquerdo;
    public Image[]           ImagensExercicio;       // [4] imagem dentro de cada card

    // ── Configuração do exercício ──────────────────────────────────────
    [Header("Exercício")]
    [Tooltip("Número de repetições (sobrescrito pela seleção de exercícios).")]
    public int   NumRepeticoes = 1;
    [Tooltip("Escala visual da esfera-waypoint central (diâmetro em Unity units).")]
    public float EscalaEsfera  = 0.45f;

    // ── Zonas de Pontuação ────────────────────────────────────────────
    [Header("Zonas de Pontuação")]
    [Tooltip("Raios das zonas de pontuação, interior→exterior (Unity units). 1 zona interior + 4 anéis.")]
    public float[] RaiosZonas      = { 0.50f, 0.80f, 1.10f, 1.40f, 1.70f };
    [Tooltip("Score de cada zona, interior→exterior (0–1).")]
    public float[] PontuacoesZonas = { 1.00f, 0.75f, 0.50f, 0.25f, 0.10f };
    [Tooltip("Cores das zonas (índice 0=interior=verde, …=exterior=vermelho).")]
    public Color[] CoresZonas      =
    {
        new Color(0.15f, 0.90f, 0.20f),  // 100 % — verde
        new Color(0.90f, 0.85f, 0.00f),  // 75 %  — amarelo
        new Color(1.00f, 0.50f, 0.00f),  // 50 %  — laranja
        new Color(1.00f, 0.15f, 0.05f),  // 25 %  — vermelho
        new Color(0.55f, 0.00f, 0.00f),  // 10 %  — vermelho escuro
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
    private int[]  _repsEscolhidas = { 1, 1, 1, 1 };

    // ── Waypoints da sequência atual ───────────────────────────────────
    private PrevenGameWaypoint[] _waypoints;
    private int           _wpAtual      = 0;
    private bool          _emVolta      = false;
    private int           _repAtual     = 0;
    private TipoExercicio _tipoAtual    = TipoExercicio.FlexaoBraco;
    private bool          _bracoDireito = true;

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
        BotaoBracoDireito?.onClick.AddListener(() => ToggleBraco(true));
        BotaoBracoEsquerdo?.onClick.AddListener(() => ToggleBraco(false));

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
                // Aguarda que o painel de calibração esteja escondido (Invoke 3 s)
                // para evitar sobreposição com o ecrã de seleção.
                if (CalibracaoManager != null && CalibracaoManager.Calibrado &&
                    (CalibracaoManager.PainelCalibracao == null ||
                     !CalibracaoManager.PainelCalibracao.activeSelf))
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
        _selecionados[idx] = !_selecionados[idx];
        AtualizarUISelecao();
    }

    void ToggleBraco(bool direito)
    {
        _bracoDireito = direito;

        if (BotaoBracoDireito)
            BotaoBracoDireito.GetComponent<Image>().color =
                direito ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.35f, 0.35f, 0.35f);
        if (BotaoBracoEsquerdo)
            BotaoBracoEsquerdo.GetComponent<Image>().color =
                direito ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.2f, 0.7f, 0.3f);

        // Espelhar imagens dos cards
        if (ImagensExercicio != null)
            foreach (var img in ImagensExercicio)
                if (img != null)
                {
                    var s = img.rectTransform.localScale;
                    s.x = direito ? 1f : -1f;
                    img.rectTransform.localScale = s;
                }
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
        _tipoAtual    = config.Tipo;
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

        switch (_tipoAtual)
        {
            case TipoExercicio.FlexaoBraco:    GerarWaypointsBracoAoLado();    break;
            case TipoExercicio.ElevacaoTotal:  GerarWaypointsElevacaoTotal();  break;
            case TipoExercicio.AbducaoLateral: GerarWaypointsAbducaoLateral(); break;
            case TipoExercicio.FlexaoCotovelo: GerarWaypointsFlexaoCotovelo(); break;
        }
        CriarLinhaGuia();
        IniciarDirecao();

        if (HUDJogo)   HUDJogo.SetActive(true);
        if (PainelFim) PainelFim.SetActive(false);
        AtualizarHUD();

        Debug.Log($"[PrevenGame] Jogo iniciado | {NumRepeticoes} reps | " +
                  $"Braço={Esqueleto.ComprimentoBraco:F2} u ({Esqueleto.ComprimentoBraco * 10f:F1} cm)");
    }

    // ─────────────────────────────────────────────────────────────────
    // Cálculo de waypoints
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Direção lateral ajustada ao braço escolhido.
    /// Direito → Cross(up, frente) = direita; Esquerdo → invertido.
    /// </summary>
    Vector3 ObterDirLateral(Vector3 dirFrente)
    {
        Vector3 d = Vector3.Cross(Vector3.up, dirFrente).normalized;
        return _bracoDireito ? d : -d;
    }

    /// <summary>EX1 — Flexão do Braço: bicep curl com bícep horizontal para a frente (0°→144°).</summary>
    void GerarWaypointsBracoAoLado()
    {
        Vector3 posOmbro  = Esqueleto.ObterPosOmbroAtual();
        float   L         = Esqueleto.ComprimentoBraco > 0.05f ? Esqueleto.ComprimentoBraco : 0.44f;
        Vector3 dirFrente = Esqueleto.DirecaoFrente == Vector3.zero ? Vector3.forward : Esqueleto.DirecaoFrente;

        float Lu = L * (18.6f / 44.0f); // braço superior
        float Lf = L * (14.6f / 44.0f); // antebraço

        // bícep horizontal para a frente; cotovelo flete de 0° (estendido) a 144° (palm perto do ombro)
        float[] angulos = { 0f, 36f, 72f, 108f, 144f };
        _posicoes = new Vector3[angulos.Length];
        for (int i = 0; i < angulos.Length; i++)
        {
            float rad = angulos[i] * Mathf.Deg2Rad;
            _posicoes[i] = posOmbro
                + dirFrente  * (Lu + Lf * Mathf.Cos(rad))
                + Vector3.up * (Lf * Mathf.Sin(rad));
        }

        InstanciarWaypoints();
        Debug.Log($"[PrevenGame] EX1 bicep curl frontal | WP[0]={_posicoes[0]:F2} WP[4]={_posicoes[4]:F2}");
    }

    /// <summary>EX2 — Elevação Total: plano sagital 15° → 165° (amplitude completa).</summary>
    void GerarWaypointsElevacaoTotal()
    {
        Vector3 posOmbro  = Esqueleto.ObterPosOmbroAtual();
        float   L         = Esqueleto.ComprimentoBraco > 0.05f ? Esqueleto.ComprimentoBraco : 0.44f;
        Vector3 dirFrente = Esqueleto.DirecaoFrente == Vector3.zero ? Vector3.forward : Esqueleto.DirecaoFrente;

        // 15° ≈ braço quase em baixo → 90° = horizontal → 180° = braço vertical acima
        float[] angulos = { 15f, 56f, 90f, 135f, 180f };
        _posicoes = new Vector3[angulos.Length];
        for (int i = 0; i < angulos.Length; i++)
        {
            float rad = angulos[i] * Mathf.Deg2Rad;
            _posicoes[i] = posOmbro
                + dirFrente    * (Mathf.Sin(rad) * L)
                + Vector3.down * (Mathf.Cos(rad) * L);
        }

        InstanciarWaypoints();
        Debug.Log($"[PrevenGame] EX2 elevação total | WP[0]={_posicoes[0]:F2} WP[4]={_posicoes[4]:F2}");
    }

    /// <summary>
    /// EX3 — Abdução Lateral + Flexão do Cotovelo:
    ///   Fase 1 (WP 0-2): abdução lateral 0°→90° (braço sobe para o lado até horizontal).
    ///   Fase 2 (WP 3-4): cotovelo flete 45°→90° com bícep fixo horizontal lateral.
    /// </summary>
    void GerarWaypointsAbducaoLateral()
    {
        Vector3 posOmbro   = Esqueleto.ObterPosOmbroAtual();
        float   L          = Esqueleto.ComprimentoBraco > 0.05f ? Esqueleto.ComprimentoBraco : 0.44f;
        Vector3 dirFrente  = Esqueleto.DirecaoFrente == Vector3.zero ? Vector3.forward : Esqueleto.DirecaoFrente;
        Vector3 dirLateral = ObterDirLateral(dirFrente);

        float Lu = L * (18.6f / 44.0f);
        float Lf = L * (14.6f / 44.0f);

        _posicoes = new Vector3[5];

        // Fase 1 — abdução 0°→90°
        float[] angulosOmbro = { 0f, 45f, 90f };
        for (int i = 0; i < 3; i++)
        {
            float rad = angulosOmbro[i] * Mathf.Deg2Rad;
            _posicoes[i] = posOmbro
                + dirLateral   * (Mathf.Sin(rad) * L)
                + Vector3.down * (Mathf.Cos(rad) * L);
        }

        // Fase 2 — cotovelo flete para cima (bícep fixo horizontal lateral), igual a EX1 WP2/WP3
        float[] angulosCotovelo = { 72f, 108f };
        for (int i = 0; i < 2; i++)
        {
            float rad = angulosCotovelo[i] * Mathf.Deg2Rad;
            _posicoes[3 + i] = posOmbro
                + dirLateral * (Lu + Lf * Mathf.Cos(rad))
                + Vector3.up * (Lf * Mathf.Sin(rad));
        }

        InstanciarWaypoints();
        Debug.Log($"[PrevenGame] EX3 abdução+cotovelo | WP[0]={_posicoes[0]:F2} WP[4]={_posicoes[4]:F2}");
    }

    /// <summary>EX4 — Flexão do Cotovelo Lateral: bicep curl com bícep horizontal para o lado (0°→144°).</summary>
    void GerarWaypointsFlexaoCotovelo()
    {
        Vector3 posOmbro   = Esqueleto.ObterPosOmbroAtual();
        float   L          = Esqueleto.ComprimentoBraco > 0.05f ? Esqueleto.ComprimentoBraco : 0.44f;
        Vector3 dirFrente  = Esqueleto.DirecaoFrente == Vector3.zero ? Vector3.forward : Esqueleto.DirecaoFrente;
        Vector3 dirLateral = ObterDirLateral(dirFrente);

        float Lu = L * (18.6f / 44.0f);
        float Lf = L * (14.6f / 44.0f);

        // bícep e antebraço sempre horizontais; cotovelo flete de 0° (estendido) a 144° no plano horizontal
        float[] angulos = { 0f, 36f, 72f, 108f, 144f };
        _posicoes = new Vector3[angulos.Length];
        for (int i = 0; i < angulos.Length; i++)
        {
            float rad = angulos[i] * Mathf.Deg2Rad;
            _posicoes[i] = posOmbro
                + dirLateral * (Lu + Lf * Mathf.Cos(rad))
                + dirFrente  * (Lf * Mathf.Sin(rad));
        }

        InstanciarWaypoints();
        Debug.Log($"[PrevenGame] EX4 bicep curl lateral | WP[0]={_posicoes[0]:F2} WP[4]={_posicoes[4]:F2}");
    }

    /// <summary>Cria (ou recria) os GameObjects dos waypoints a partir de <see cref="_posicoes"/>.</summary>
    void InstanciarWaypoints()
    {
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
            // IDA concluída → VOLTA começa no penúltimo WP (salta o último visitado)
            _emVolta = true;
            _wpAtual = 1; // IndiceWaypointAtual() = (Length-1) - 1 = Length-2
            foreach (var wp in _waypoints) wp.Repor();
            int idxInicio = IndiceWaypointAtual();
            _waypoints[idxInicio].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
            AtualizarLinhaGuia();
            Debug.Log("[PrevenGame] Ida concluída → volta a partir do WP " + idxInicio);
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
                // VOLTA concluída → nova IDA começa no WP 1 (salta o WP 0 recém-visitado)
                _emVolta = false;
                _wpAtual = 1; // salta WP[0] que foi o último waypoint da volta
                foreach (var wp in _waypoints) wp.Repor();
                int idxInicio = IndiceWaypointAtual(); // = 1
                _waypoints[idxInicio].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
                AtualizarLinhaGuia();
                Debug.Log($"[PrevenGame] Rep {_repAtual + 1}/{NumRepeticoes} — Ida a partir do WP {idxInicio}");
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
