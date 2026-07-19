using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MinijogoZonas — Núcleo partilhado dos minijogos de exercício (dardos, pomar,
/// hangar). Trata de TODO o sistema de captura e guia; as subclasses só metem o
/// tema (dardo/fruta/avião) através dos hooks.
///
/// O que faz por cada repetição:
///   • percurso ida-e-volta sem o último ponto do regresso [w0..wN-1, wN-2..w1]
///     (opcionalmente invertido — <see cref="InverterPercurso"/>);
///   • zonas clínicas concêntricas por waypoint (<see cref="PrevenGameWaypoint"/>),
///     invisíveis, com captura em LISTA estrita: o ponto atual só é capturado
///     quando a mão ENTRA na zona do próximo (pontuado pela melhor distância ao
///     centro registada); o último ponto usa a captura clássica entrada→saída;
///   • waypoints DINÂMICOS rígidos (regenerados por frame a partir do ombro live,
///     sem suavização) + arco guia Catmull-Rom + marcador do alvo atual;
///   • feed da câmara de apoio lateral no HUD VR (<see cref="CamaraApoioExercicio"/>).
///
/// Hooks para as subclasses:
///   <see cref="AoPrepararRep"/>, <see cref="AoCapturarPasso"/>,
///   <see cref="AoConcluirRep"/> (default: emite logo), <see cref="AoTerminar"/>,
///   <see cref="InverterPercurso"/>, <see cref="ObjetoMao"/> (visibilidade
///   automática com o tracking da mão) e <see cref="CriarHolderNaMao"/>.
/// </summary>
public abstract class MinijogoZonas : MinijogoBase
{
    [Header("Waypoints (zonas clínicas de pontuação)")]
    [Tooltip("Raios das zonas concêntricas de cada waypoint (m), interior→exterior — só afetam a CAPTURA (o visual não muda).")]
    public float[] RaiosZonas = { 0.06f, 0.09f, 0.12f, 0.15f, 0.18f };
    [Tooltip("Pontuação de cada zona, interior→exterior.")]
    public float[] PontuacoesZonas = { 1.00f, 0.75f, 0.50f, 0.25f, 0.10f };
    [Tooltip("Escala (m) da esfera base de cada waypoint (só visível com o debug ligado).")]
    public float EscalaEsfera = 0.045f;
    [Tooltip("DEBUG: mostra as zonas dos waypoints (no jogo são invisíveis — só pontuam).")]
    public bool MostrarZonasDebug = false;
    [Tooltip("FIX temporário: empurra todos os waypoints para a FRENTE do jogador (m).")]
    public float OffsetFrenteWaypoints = 0.10f;

    [Header("Arco guia")]
    [Tooltip("Linha em arco pela trajetória do exercício — acompanha o jogador.")]
    public bool MostrarArcoGuia = true;
    [Tooltip("Largura do arco (m).")]
    public float LarguraArco = 0.012f;
    [Tooltip("Opacidade do arco guia (0–1). Baixa = discreto, só uma sugestão de trajetória.")]
    [Range(0f, 1f)] public float AlfaArco = 0.2f;
    [Tooltip("Marcador no alvo atual do percurso (esfera translúcida).")]
    public bool MostrarMarcadorAlvo = true;
    [Tooltip("Diâmetro do marcador do alvo atual (m).")]
    public float EscalaMarcadorAlvo = 0.07f;

    [Header("Câmara de apoio (vista lateral no HUD)")]
    [Tooltip("Feed de uma câmara lateral do exercício no canto superior esquerdo do HUD VR — ajuda a compreender o movimento.")]
    public bool MostrarCamaraApoio = true;

    // ── Estado da repetição ───────────────────────────────────────────
    private ConfigRep _cfg;
    private Vector3[] _wpsRep;   // waypoints da rep, já na ordem do percurso (invertida se pedido)
    private readonly List<Vector3> _percurso = new List<Vector3>(); // ida e volta
    private readonly List<int> _percursoWaypoint = new List<int>(); // índice da zona por passo
    private int  _alvoAtual;
    private readonly List<float> _scoresAlvos = new List<float>();
    private bool _emRep;

    private readonly List<PrevenGameWaypoint> _waypoints = new List<PrevenGameWaypoint>();

    private LineRenderer _arcoGuia;
    private GameObject   _marcadorAlvo;
    private readonly List<Vector3> _pontosArco = new List<Vector3>();
    private CamaraApoioExercicio _camApoio;

    // ── API para as subclasses ────────────────────────────────────────

    /// <summary>Config da rep em curso.</summary>
    protected ConfigRep Cfg => _cfg;

    /// <summary>Número de waypoints do exercício nesta rep.</summary>
    protected int NumWaypoints => _wpsRep != null ? _wpsRep.Length : 0;

    /// <summary>Prefixo dos logs (ex.: "Dardos").</summary>
    protected virtual string Etiqueta => GetType().Name;

    /// <summary>True → o percurso usa os waypoints por ordem INVERTIDA (a rep começa no último ponto do gerador).</summary>
    protected virtual bool InverterPercurso => false;

    /// <summary>Objeto na mão (dardo/bastão/fruta) — a visibilidade segue o tracking da mão automaticamente.</summary>
    protected GameObject ObjetoMao { get; set; }

    /// <summary>Chamado no fim do PrepararRep — spawn de objetos da rep (dardo na mão, etc.).</summary>
    protected virtual void AoPrepararRep(ConfigRep cfg) { }

    /// <summary>Chamado quando um passo do percurso é capturado (antes de ativar o próximo).</summary>
    /// <param name="passo">Índice no percurso (0-based).</param>
    /// <param name="indiceWaypoint">Índice do waypoint do exercício correspondente.</param>
    protected virtual void AoCapturarPasso(int passo, int indiceWaypoint) { }

    /// <summary>
    /// Chamado quando a rep termina, com o pct (0–100). O default emite logo;
    /// subclasses podem animar primeiro (lançar o dardo, mover o avião...) e
    /// chamar <see cref="MinijogoBase.EmitirRepConcluida"/> no fim.
    /// </summary>
    protected virtual void AoConcluirRep(float pct) => EmitirRepConcluida(pct);

    /// <summary>Chamado no Terminar — limpezas específicas do jogo.</summary>
    protected virtual void AoTerminar() { }

    // ── MinijogoBase ──────────────────────────────────────────────────
    public override void PrepararRep(ConfigRep cfg)
    {
        _cfg = cfg;

        if (cfg.Waypoints == null || cfg.Waypoints.Length == 0)
        {
            Debug.LogWarning($"[{Etiqueta}] Rep sem waypoints — 0 %.");
            EmitirRepConcluida(0f);
            return;
        }

        _wpsRep = OrdenarWaypoints(cfg.Waypoints);

        // Percurso ida e volta SEM o último ponto do regresso: [w0..wN-1, wN-2..w1].
        // O ponto final não tem captura fiável (sem "próximo" para o avanço por
        // proximidade; a mão tende a ficar dentro da zona sem sair) — a rep
        // conclui ao capturar o w1 do regresso.
        _percurso.Clear();
        _percursoWaypoint.Clear();
        int n = _wpsRep.Length;
        for (int i = 0; i < n; i++)          { _percurso.Add(_wpsRep[i]); _percursoWaypoint.Add(i); }
        for (int i = n - 2; i >= 1; i--)     { _percurso.Add(_wpsRep[i]); _percursoWaypoint.Add(i); }

        _alvoAtual = 0;
        _scoresAlvos.Clear();

        CriarWaypoints();
        AtivarWaypointAtual();
        CriarGuia(cfg);
        AoPrepararRep(cfg);
        AtualizarGuia();

        // Feed lateral do exercício no canto superior esquerdo do HUD VR.
        if (MostrarCamaraApoio)
        {
            if (_camApoio == null) _camApoio = CamaraApoioExercicio.ObterOuCriar();
            var hud = HudVR.ObterOuCriar();
            if (hud != null) hud.DefinirFeedApoio(_camApoio.Textura);
        }

        if (!cfg.Mao.Ativa)
            Debug.LogWarning($"[{Etiqueta}] Mão inativa no arranque da rep — acorda o comando (gatilho) ou liga o sensor.");

        // Amplitude do percurso — se sair minúscula, a calibração capturou um braço curto.
        float amplitude = 0f;
        for (int i = 1; i < _wpsRep.Length; i++)
            amplitude = Mathf.Max(amplitude, Vector3.Distance(_wpsRep[0], _wpsRep[i]));
        Debug.Log($"[{Etiqueta}] Rep {cfg.RepAtual}/{cfg.TotalReps} braço {(cfg.BracoDireito ? "dir" : "esq")}: " +
                  $"comprimento braço={cfg.ComprimentoBraco:F2} m, amplitude do percurso={amplitude:F2} m " +
                  $"(dinâmico={_cfg.Rastreador != null}, invertido={InverterPercurso}).");
        if (amplitude < 0.25f)
            Debug.LogWarning($"[{Etiqueta}] ⚠ Percurso muito pequeno — recalibra o braço (comprimento capturado curto).");

        _emRep = true;
    }

    public override void Terminar()
    {
        _emRep = false;
        LimparWaypoints();
        EsconderGuia();
        if (_camApoio != null) _camApoio.Mostrar(false);
        var hud = HudVR.ObterOuCriar();
        if (hud != null) hud.MostrarFeedApoio(false);
        if (ObjetoMao != null) { Destroy(ObjetoMao); ObjetoMao = null; }
        AoTerminar();
    }

    protected virtual void Update()
    {
        if (!_emRep || _cfg.Mao == null) return;

        // Objeto na mão visível só com a mão tracked.
        if (ObjetoMao != null && ObjetoMao.activeSelf != _cfg.Mao.Ativa)
            ObjetoMao.SetActive(_cfg.Mao.Ativa);

        if (!_cfg.Mao.Ativa) return;

        // Waypoints DINÂMICOS rígidos (regenerados por frame, sem suavização).
        AtualizarWaypointsDinamicos();

        // Câmara de apoio: enquadra o arco da ida visto do lado do braço ativo.
        if (_camApoio != null && _wpsRep != null)
        {
            Vector3 frenteApoio = _cfg.Rastreador != null
                ? _cfg.Rastreador.ObterDirecaoFrenteAtual(_cfg.BracoDireito)
                : Vector3.forward;
            _camApoio.Enquadrar(_percurso, _wpsRep.Length, frenteApoio, _cfg.BracoDireito);
        }

        var wp = _waypoints[_percursoWaypoint[_alvoAtual]];
        if (wp == null) return;

        // Captura em LISTA estrita: o ponto atual só é capturado quando a mão
        // ENTRA na zona do PRÓXIMO — pontuado pela melhor distância registada.
        // O último ponto do percurso não tem "próximo" → entrada→saída clássica.
        bool capturado;
        if (_alvoAtual + 1 < _percurso.Count)
        {
            wp.RegistarPosicao(_cfg.Mao.Posicao);
            float raioMax = RaiosZonas != null && RaiosZonas.Length > 0
                ? RaiosZonas[RaiosZonas.Length - 1] : 0.18f;
            capturado = Vector3.Distance(_cfg.Mao.Posicao, _percurso[_alvoAtual + 1]) <= raioMax
                     && wp.ForcarCaptura(_cfg.Mao.Posicao);
        }
        else capturado = wp.VerificarToque(_cfg.Mao.Posicao);

        if (!capturado) return;

        _scoresAlvos.Add(wp.UltimaPontuacao);
        int passoCapturado = _alvoAtual;
        _alvoAtual++;
        AoCapturarPasso(passoCapturado, _percursoWaypoint[passoCapturado]);

        if (_alvoAtual >= _percurso.Count) { ConcluirRep(); return; }
        AtivarWaypointAtual();
    }

    // ── Conclusão ─────────────────────────────────────────────────────
    void ConcluirRep()
    {
        _emRep = false;

        float soma = 0f;
        foreach (var s in _scoresAlvos) soma += s;
        float pct = _scoresAlvos.Count > 0 ? 100f * soma / _scoresAlvos.Count : 0f;

        LimparWaypoints();
        EsconderGuia();

        Debug.Log($"[{Etiqueta}] Rep concluída: {pct:F0} %.");
        AoConcluirRep(pct);
    }

    // ── Waypoints dinâmicos ───────────────────────────────────────────
    /// <summary>
    /// Recalcula o percurso (ida e volta) a partir do ombro live e cola as zonas
    /// às novas posições — o exercício fica ancorado ao jogador mesmo que ele se
    /// desloque/rode. Sem lerp: seguimento rígido.
    /// </summary>
    void AtualizarWaypointsDinamicos()
    {
        if (_cfg.Rastreador == null) return; // sem rastreador: percurso fixo

        Vector3 ombro  = _cfg.Rastreador.ObterOmbroAtual(_cfg.BracoDireito);
        Vector3 frente = _cfg.Rastreador.ObterDirecaoFrenteAtual(_cfg.BracoDireito);

        var wps = OrdenarWaypoints(
            ExerciciosWaypoints.Gerar(_cfg.Tipo, ombro, _cfg.ComprimentoBraco, frente, _cfg.BracoDireito));
        int n = wps.Length;

        // Fix temporário: percurso inteiro empurrado para a frente do jogador.
        if (Mathf.Abs(OffsetFrenteWaypoints) > 0.0001f)
        {
            Vector3 off = frente.normalized * OffsetFrenteWaypoints;
            for (int i = 0; i < n; i++) wps[i] += off;
        }

        _wpsRep = wps;
        for (int i = 0; i < n && i < _percurso.Count; i++)               _percurso[i] = wps[i];
        for (int i = n - 2, j = n; i >= 0 && j < _percurso.Count; i--, j++) _percurso[j] = wps[i];

        for (int i = 0; i < _waypoints.Count && i < n; i++)
            if (_waypoints[i] != null) _waypoints[i].transform.position = wps[i];

        AtualizarGuia();
    }

    Vector3[] OrdenarWaypoints(Vector3[] wps)
    {
        if (!InverterPercurso) return wps;
        var inv = new Vector3[wps.Length];
        for (int i = 0; i < wps.Length; i++) inv[i] = wps[wps.Length - 1 - i];
        return inv;
    }

    // ── Zonas de captura (PrevenGameWaypoint — sistema clínico) ───────
    void CriarWaypoints()
    {
        LimparWaypoints();
        for (int i = 0; i < _wpsRep.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"WaypointExercicio_{i + 1}";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, true);
            go.transform.position   = _wpsRep[i];
            go.transform.localScale = Vector3.one * EscalaEsfera;

            var wp = go.AddComponent<PrevenGameWaypoint>();
            wp.ConfigurarZonas(RaiosZonas, PontuacoesZonas, null, EscalaEsfera, MostrarZonasDebug);
            _waypoints.Add(wp);
        }
    }

    /// <summary>Ativa a zona do passo atual (na volta, o waypoint já usado na ida é reposto).</summary>
    void AtivarWaypointAtual()
    {
        if (_alvoAtual >= _percursoWaypoint.Count) return;
        var wp = _waypoints[_percursoWaypoint[_alvoAtual]];
        if (wp == null) return;
        wp.Repor();
        wp.SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
    }

    void LimparWaypoints()
    {
        foreach (var wp in _waypoints) if (wp != null) Destroy(wp.gameObject);
        _waypoints.Clear();
    }

    // ── Objeto na mão (holder partilhado) ─────────────────────────────
    /// <summary>
    /// Cria um holder filho da mão com o modelo dado: o HOLDER leva a rotação de
    /// mão (é o que se inspeciona), o clone do modelo fica a identidade e
    /// preserva o tamanho MUNDO do original (lossyScale — nesting do FBX).
    /// Substitui o <see cref="ObjetoMao"/> anterior.
    /// </summary>
    protected GameObject CriarHolderNaMao(string nome, GameObject modelo, Vector3 rotacaoLocalEuler)
    {
        if (ObjetoMao != null) { Destroy(ObjetoMao); ObjetoMao = null; }
        if (_cfg.Mao == null || modelo == null) return null;

        var holder = new GameObject(nome);
        holder.transform.SetParent(_cfg.Mao.transform, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.Euler(rotacaoLocalEuler);

        var clone = Instantiate(modelo);
        clone.SetActive(true);
        clone.transform.SetParent(holder.transform, false);
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale    = modelo.transform.lossyScale;

        // FBX do Blender trazem por vezes Câmara/Luz/AudioListener embutidos
        // (ex.: o "bastão avião") — na mão seriam desastrosos: a câmara fica
        // ATIVA (rouba o render do jogo) e a luz/listener duplicam os da cena.
        foreach (var cam in clone.GetComponentsInChildren<Camera>(true))      Destroy(cam);
        foreach (var al  in clone.GetComponentsInChildren<AudioListener>(true)) Destroy(al);
        foreach (var luz in clone.GetComponentsInChildren<Light>(true))       Destroy(luz);

        ObjetoMao = holder;
        return holder;
    }

    /// <summary>
    /// Escala o conteúdo do holder para a maior dimensão ficar ≈ tamanhoAlvo (m).
    /// Necessário para modelos vindos de ASSETS do Blender (chegam ×100 — um
    /// bastão de 30 m colado à mão envolve a cabeça e parece um bug visual);
    /// objetos da CENA já normalizada não precisam (o lossyScale trata deles).
    /// </summary>
    protected static void NormalizarTamanhoObjetoMao(GameObject holder, float tamanhoAlvo)
    {
        if (holder == null || tamanhoAlvo <= 0.001f) return;

        var rends = holder.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        float dimMax = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (dimMax <= 0.0001f) return;
        if (Mathf.Abs(dimMax - tamanhoAlvo) / tamanhoAlvo < 0.15f) return; // já está ao tamanho certo

        float fator = tamanhoAlvo / dimMax;
        foreach (Transform filho in holder.transform)
            filho.localScale *= fator;
        Debug.Log($"[MinijogoZonas] Objeto da mão \"{holder.name}\" normalizado: {dimMax:F2} m → " +
                  $"{tamanhoAlvo:F2} m (fator {fator:E2}).");
    }

    // ── Arco guia + marcador do alvo atual ────────────────────────────
    void CriarGuia(ConfigRep cfg)
    {
        Color corBase = cfg.BracoDireito ? new Color(1f, 0.75f, 0.2f) : new Color(0.2f, 0.7f, 1f);

        if (MostrarArcoGuia)
        {
            if (_arcoGuia == null)
            {
                var go = new GameObject("ArcoGuia");
                go.transform.SetParent(transform, false);
                _arcoGuia = go.AddComponent<LineRenderer>();
                _arcoGuia.material      = new Material(Shader.Find("Sprites/Default"));
                _arcoGuia.textureMode   = LineTextureMode.Stretch;
                _arcoGuia.numCapVertices = 4;
            }
            _arcoGuia.widthMultiplier = LarguraArco;
            var corArco = new Color(corBase.r, corBase.g, corBase.b, AlfaArco);
            _arcoGuia.startColor = _arcoGuia.endColor = corArco;
            _arcoGuia.gameObject.SetActive(true);
        }

        if (MostrarMarcadorAlvo)
        {
            if (_marcadorAlvo == null)
            {
                _marcadorAlvo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _marcadorAlvo.name = "MarcadorAlvo";
                Destroy(_marcadorAlvo.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Sprites/Default"));
                _marcadorAlvo.GetComponent<Renderer>().material = mat;
            }
            _marcadorAlvo.transform.localScale = Vector3.one * EscalaMarcadorAlvo;
            // Um pouco mais visível que o arco, mas ainda discreto.
            _marcadorAlvo.GetComponent<Renderer>().material.color =
                new Color(corBase.r, corBase.g, corBase.b, Mathf.Clamp01(AlfaArco + 0.15f));
            _marcadorAlvo.SetActive(true);
        }
    }

    /// <summary>Redesenha o arco (Catmull-Rom pelos waypoints) e move o marcador do alvo atual.</summary>
    void AtualizarGuia()
    {
        int n = Mathf.Min(_wpsRep != null ? _wpsRep.Length : 0, _percurso.Count);
        if (n < 2) return;

        if (_arcoGuia != null && _arcoGuia.gameObject.activeSelf)
        {
            _pontosArco.Clear();
            const int porSegmento = 8;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = _percurso[Mathf.Max(i - 1, 0)];
                Vector3 p1 = _percurso[i];
                Vector3 p2 = _percurso[i + 1];
                Vector3 p3 = _percurso[Mathf.Min(i + 2, n - 1)];
                for (int s = 0; s < porSegmento; s++)
                    _pontosArco.Add(CatmullRom(p0, p1, p2, p3, s / (float)porSegmento));
            }
            _pontosArco.Add(_percurso[n - 1]);

            _arcoGuia.positionCount = _pontosArco.Count;
            for (int i = 0; i < _pontosArco.Count; i++)
                _arcoGuia.SetPosition(i, _pontosArco[i]);
        }

        if (_marcadorAlvo != null && _marcadorAlvo.activeSelf && _alvoAtual < _percurso.Count)
            _marcadorAlvo.transform.position = _percurso[_alvoAtual];
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    void EsconderGuia()
    {
        if (_arcoGuia != null) _arcoGuia.gameObject.SetActive(false);
        if (_marcadorAlvo != null) _marcadorAlvo.SetActive(false);
    }
}
