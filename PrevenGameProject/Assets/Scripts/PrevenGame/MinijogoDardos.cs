using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MinijogoDardos — O minijogo dos dardos (implementa <see cref="MinijogoBase"/>).
///
/// Cada repetição: o jogador percorre os waypoints do exercício em IDA E VOLTA
/// (a rep começa de braço esticado — os geradores do ExerciciosWaypoints já
/// começam na posição estendida) com um dardo NOVO na mão. A accuracy conta por
/// zonas de proximidade em cada alvo (passar rente vale mais). No fim, o dardo
/// é lançado a alta velocidade contra o alvo do bar e crava no aro
/// correspondente: ≥80 % → aro 1 (bullseye) … resto → aro 5. A conclusão da rep
/// só é emitida quando o dardo crava.
///
/// Os aros são os objetos "aro 1".."aro 5" do FBX (concêntricos; geometria lida
/// em runtime — o YAML tem nesting de escala ×100).
/// </summary>
public class MinijogoDardos : MinijogoBase
{
    [Header("Alvo (auto-encontrado por nome se vazio)")]
    [Tooltip("Aros do alvo, do mais pequeno para o maior: [aro 1 (bullseye) .. aro 5].")]
    public Transform[] Aros;

    [Header("Dardo")]
    [Tooltip("Modelo do dardo (ex.: o objeto \"dardo\" do FBX da cena — é clonado por rep; sem nada, placeholder).")]
    public GameObject ModeloDardo;
    [Tooltip("Rotação local do modelo dentro da mão (validado com o dardo do FBX: 0,90,0).")]
    public Vector3 RotacaoModeloEuler = new Vector3(0f, 90f, 0f);
    [Tooltip("Rotação MUNDO do dardo quando fica cravado no aro (validado: 0,0,0).")]
    public Vector3 RotacaoCravadoEuler = Vector3.zero;
    [Tooltip("Duração do voo (s) — curta = alta velocidade.")]
    public float DuracaoVoo = 0.15f;
    [Tooltip("Offset do ponto cravado ao longo da normal do alvo (m).")]
    public float OffsetCravado = 0.01f;
    [Tooltip("Separação mínima (m) entre dardos cravados — evita dardos uns em cima dos outros.")]
    public float SeparacaoDardos = 0.035f;

    [Header("Percurso")]
    [Tooltip("Raio (m) a que a mão conta como 'chegou' ao alvo e avança.")]
    public float RaioAvanco = 0.10f;
    [Tooltip("Esferas de DEBUG nos waypoints (no jogo são invisíveis).")]
    public bool MostrarEsferasDebug = false;
    [Tooltip("Escala das esferas de debug (m).")]
    public float EscalaEsfera = 0.08f;

    [Header("Waypoints dinâmicos (o exercício acompanha o jogador)")]
    [Tooltip("Velocidade máx. do ombro (m/s, suavizada) para a captura contar — acima disto os alvos estão a 'varrer' com o corpo e a captura suspende-se.")]
    public float VelocidadeOmbroMax = 0.4f;
    [Tooltip("Velocidade máx. do yaw de corpo (graus/s, suavizada) para a captura contar.")]
    public float VelocidadeYawMax = 45f;

    [Header("Arco guia (como o jogo antigo)")]
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

    // Zonas de accuracy por alvo (menor distância registada até avançar).
    const float ZONA_PERFEITA = 0.05f; // → 1.0
    const float ZONA_BOA      = 0.08f; // → 0.75
                                       // resto (≤ RaioAvanco) → 0.5

    // ── Geometria do alvo (calculada em runtime) ──────────────────────
    private Vector3 _centro;
    private Vector3 _normal, _eixoU, _eixoV;
    private float[] _raios;          // [aro1..aro5] crescente
    private bool _geometriaPronta;

    // ── Estado da repetição ───────────────────────────────────────────
    private ConfigRep _cfg;
    private readonly List<Vector3> _percurso = new List<Vector3>(); // ida e volta
    private readonly List<int> _percursoWaypoint = new List<int>(); // índice da esfera por passo
    private int   _alvoAtual;
    private float _menorDist;
    private readonly List<float> _scoresAlvos = new List<float>();
    private bool _emRep;

    // Gate de saída: depois de capturar um alvo, a mão tem de se AFASTAR do ponto
    // de captura antes de o próximo poder contar — impede capturas em cadeia
    // quando os waypoints distam menos que o RaioAvanco.
    private Vector3 _posUltimaCaptura;
    private bool    _aguardaSaida;

    // Estabilidade do corpo (gate da captura com waypoints dinâmicos).
    private Vector3 _ombroAnterior;
    private float   _yawAnterior;
    private bool    _estabilidadeIniciada;
    private float   _velOmbroSuave;   // EMA — ignora picos do balanço natural da cabeça
    private float   _velYawSuave;

    // Arco guia + marcador do alvo atual.
    private LineRenderer _arcoGuia;
    private GameObject   _marcadorAlvo;
    private readonly List<Vector3> _pontosArco = new List<Vector3>();

    private readonly List<GameObject> _esferas = new List<GameObject>();
    private readonly List<GameObject> _dardosCravados = new List<GameObject>();
    private readonly List<Vector3> _pontosCravados = new List<Vector3>(); // p/ espalhar os dardos
    private DardoMinijogo _dardoAtivo;

    void Start()
    {
        EncontrarAros();
        var audio = GestorAudio.Instancia;
        if (audio != null) audio.TocarAmbiente(audio.AmbienteBarDardos);
    }

    // ── MinijogoBase ──────────────────────────────────────────────────
    public override void PrepararRep(ConfigRep cfg)
    {
        _cfg = cfg;
        if (!_geometriaPronta) CalcularGeometria();

        if (cfg.Waypoints == null || cfg.Waypoints.Length == 0)
        {
            Debug.LogWarning("[Dardos] Rep sem waypoints — 0 %.");
            EmitirRepConcluida(0f);
            return;
        }

        // Percurso ida e volta: [w0..wN-1, wN-2..w0] (sem repetir o extremo).
        _percurso.Clear();
        _percursoWaypoint.Clear();
        int n = cfg.Waypoints.Length;
        for (int i = 0; i < n; i++)          { _percurso.Add(cfg.Waypoints[i]); _percursoWaypoint.Add(i); }
        for (int i = n - 2; i >= 0; i--)     { _percurso.Add(cfg.Waypoints[i]); _percursoWaypoint.Add(i); }

        _alvoAtual  = 0;
        _menorDist  = float.MaxValue;
        _scoresAlvos.Clear();
        _estabilidadeIniciada = false;
        _velOmbroSuave = 0f;
        _velYawSuave   = 0f;
        _aguardaSaida  = false;

        if (MostrarEsferasDebug) CriarEsferas(cfg);
        CriarGuia(cfg);
        AcoplarNovoDardo(cfg);
        AtualizarVisual();
        AtualizarGuia();

        if (!cfg.Mao.Ativa)
            Debug.LogWarning("[Dardos] Mão inativa no arranque da rep — acorda o comando (gatilho) ou liga o sensor.");

        // Amplitude do percurso — se sair minúscula, a calibração capturou um braço curto.
        float amplitude = 0f;
        for (int i = 1; i < cfg.Waypoints.Length; i++)
            amplitude = Mathf.Max(amplitude, Vector3.Distance(cfg.Waypoints[0], cfg.Waypoints[i]));
        Debug.Log($"[Dardos] Rep {cfg.RepAtual}/{cfg.TotalReps} braço {(cfg.BracoDireito ? "dir" : "esq")}: " +
                  $"comprimento braço={cfg.ComprimentoBraco:F2} m, amplitude do percurso={amplitude:F2} m " +
                  $"(dinâmico={_cfg.Rastreador != null}).");
        if (amplitude < 0.25f)
            Debug.LogWarning("[Dardos] ⚠ Percurso muito pequeno — recalibra o braço (comprimento capturado curto).");

        _emRep = true;
    }

    public override void Terminar()
    {
        _emRep = false;
        LimparEsferas();
        EsconderGuia();
        foreach (var d in _dardosCravados) if (d != null) Destroy(d);
        _dardosCravados.Clear();
        _pontosCravados.Clear();
        if (_dardoAtivo != null) { Destroy(_dardoAtivo.gameObject); _dardoAtivo = null; }
    }

    void Update()
    {
        if (!_emRep || _cfg.Mao == null) return;

        // Dardo visível só com a mão tracked (comando adormecido = dardo escondido).
        if (_dardoAtivo != null && _dardoAtivo.gameObject.activeSelf != _cfg.Mao.Ativa)
            _dardoAtivo.gameObject.SetActive(_cfg.Mao.Ativa);

        if (!_cfg.Mao.Ativa) return;

        // Waypoints DINÂMICOS: o percurso segue o ombro live todos os frames.
        bool corpoEstavel = AtualizarWaypointsDinamicos();

        float d = Vector3.Distance(_cfg.Mao.Posicao, _percurso[_alvoAtual]);

        if (!corpoEstavel)
        {
            // O corpo (e por isso os alvos) está em movimento — captura suspensa
            // e a menor-distância reposta, para o varrimento dos alvos pela mão
            // parada não avançar waypoints nem inflacionar a pontuação.
            _menorDist = d;
            return;
        }

        // Gate de saída: só depois de a mão se afastar do ponto da última captura
        // é que o próximo alvo pode contar (senão os alvos encadeavam-se sozinhos).
        if (_aguardaSaida)
        {
            if (Vector3.Distance(_cfg.Mao.Posicao, _posUltimaCaptura) < RaioAvanco) return;
            _aguardaSaida = false;
            _menorDist = d; // recomeça a medição da zona a partir daqui
        }

        if (d < _menorDist) _menorDist = d;
        if (d > RaioAvanco) return;

        // Chegou ao alvo — pontua pela menor distância registada e avança.
        _scoresAlvos.Add(_menorDist <= ZONA_PERFEITA ? 1f
                       : _menorDist <= ZONA_BOA      ? 0.75f
                       :                               0.5f);
        _posUltimaCaptura = _cfg.Mao.Posicao;
        _aguardaSaida     = true;
        _alvoAtual++;
        _menorDist = float.MaxValue;

        if (_alvoAtual >= _percurso.Count) { ConcluirRep(); return; }
        AtualizarVisual();
    }

    /// <summary>
    /// Recalcula o percurso (ida e volta) a partir do ombro live — o exercício
    /// fica ancorado ao jogador mesmo que ele se desloque/rode. Devolve true se
    /// o corpo está estável (captura permitida).
    /// </summary>
    bool AtualizarWaypointsDinamicos()
    {
        if (_cfg.Rastreador == null) return true; // sem rastreador: percurso fixo

        Vector3 ombro  = _cfg.Rastreador.ObterOmbroAtual(_cfg.BracoDireito);
        Vector3 frente = _cfg.Rastreador.ObterDirecaoFrenteAtual(_cfg.BracoDireito);
        float   yaw    = _cfg.Rastreador.YawCorpo.eulerAngles.y;

        var wps = ExerciciosWaypoints.Gerar(_cfg.Tipo, ombro, _cfg.ComprimentoBraco, frente, _cfg.BracoDireito);
        int n = wps.Length;
        for (int i = 0; i < n && i < _percurso.Count; i++)               _percurso[i] = wps[i];
        for (int i = n - 2, j = n; i >= 0 && j < _percurso.Count; i--, j++) _percurso[j] = wps[i];

        if (MostrarEsferasDebug)
            for (int i = 0; i < _esferas.Count && i < n; i++)
                if (_esferas[i] != null) _esferas[i].transform.position = wps[i];

        AtualizarGuia();

        // Estabilidade: velocidades SUAVIZADAS (EMA) do ombro e do yaw de corpo —
        // o balanço natural da cabeça dá picos instantâneos que não devem
        // suspender a captura; só movimento sustentado do corpo suspende.
        bool estavel = true;
        if (_estabilidadeIniciada && Time.deltaTime > 0f)
        {
            float velOmbro = Vector3.Distance(ombro, _ombroAnterior) / Time.deltaTime;
            float velYaw   = Mathf.Abs(Mathf.DeltaAngle(_yawAnterior, yaw)) / Time.deltaTime;

            float alfa = 1f - Mathf.Exp(-Time.deltaTime / 0.15f); // meia-vida ~0.1 s
            _velOmbroSuave = Mathf.Lerp(_velOmbroSuave, velOmbro, alfa);
            _velYawSuave   = Mathf.Lerp(_velYawSuave,   velYaw,   alfa);

            estavel = _velOmbroSuave <= VelocidadeOmbroMax && _velYawSuave <= VelocidadeYawMax;
        }
        _ombroAnterior = ombro;
        _yawAnterior   = yaw;
        _estabilidadeIniciada = true;
        return estavel;
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
        int n = Mathf.Min(_cfg.Waypoints != null ? _cfg.Waypoints.Length : 0, _percurso.Count);
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

    // ── Conclusão da rep + lançamento ─────────────────────────────────
    void ConcluirRep()
    {
        _emRep = false;

        float soma = 0f;
        foreach (var s in _scoresAlvos) soma += s;
        float pct = _scoresAlvos.Count > 0 ? 100f * soma / _scoresAlvos.Count : 0f;

        LimparEsferas();
        EsconderGuia();

        int aro = AroParaPct(pct);
        Debug.Log($"[Dardos] Rep concluída: {pct:F0} % → aro {aro}.");

        if (_dardoAtivo == null)
        {
            EmitirRepConcluida(pct);
            return;
        }

        var audio = GestorAudio.Instancia;
        if (audio != null) audio.TocarSfx(audio.SfxLancamentoDardo);

        var dardo = _dardoAtivo;
        _dardoAtivo = null;
        _dardosCravados.Add(dardo.gameObject);
        Vector3 ponto = PontoNoAro(aro);
        _pontosCravados.Add(ponto);
        dardo.DuracaoVoo = DuracaoVoo;
        dardo.RotacaoCravadoEuler    = RotacaoCravadoEuler;
        dardo.UsarRotacaoCravado     = true;
        dardo.Lancar(ponto, Aros[aro - 1], aoChegar: () => EmitirRepConcluida(pct));
    }

    /// <summary>≥80→1 (bullseye), ≥60→2, ≥40→3, ≥20→4, resto→5.</summary>
    static int AroParaPct(float pct)
    {
        if (pct >= 80f) return 1;
        if (pct >= 60f) return 2;
        if (pct >= 40f) return 3;
        if (pct >= 20f) return 4;
        return 5;
    }

    // ── Alvo (aros) ───────────────────────────────────────────────────
    void EncontrarAros()
    {
        if (Aros != null && Aros.Length == 5 && Aros[0] != null) return;
        Aros = new Transform[5];
        for (int i = 1; i <= 5; i++)
        {
            var go = GameObject.Find($"aro {i}");
            if (go == null) { Debug.LogError($"[Dardos] Objeto \"aro {i}\" não encontrado na cena."); return; }
            Aros[i - 1] = go.transform;
        }
    }

    void CalcularGeometria()
    {
        if (Aros == null || Aros.Length < 5 || Aros[0] == null) EncontrarAros();
        if (Aros == null || Aros[0] == null) return;

        var refT = Aros[0];
        _centro = refT.position;
        _normal = refT.forward.normalized;  // eixo Z local do aro = normal do alvo
        _eixoU  = refT.right.normalized;
        _eixoV  = refT.up.normalized;

        _raios = new float[Aros.Length];
        for (int i = 0; i < Aros.Length; i++)
        {
            var rend = Aros[i].GetComponent<Renderer>();
            _raios[i] = rend != null
                ? Mathf.Max(ExtensaoAoLongoDe(rend.bounds, _eixoU), ExtensaoAoLongoDe(rend.bounds, _eixoV))
                : 0.05f * (i + 1);
        }
        System.Array.Sort(_raios); // garante crescente (aro 1 → aro 5)
        _geometriaPronta = true;

        Debug.Log($"[Dardos] Alvo: centro=({_centro.x:F2}, {_centro.y:F2}, {_centro.z:F2}) " +
                  $"raios=[{string.Join(", ", System.Array.ConvertAll(_raios, r => r.ToString("F3")))}] m");
    }

    /// <summary>Extensão de uma AABB ao longo de uma direção (função de suporte).</summary>
    static float ExtensaoAoLongoDe(Bounds b, Vector3 dir)
    {
        Vector3 e = b.extents;
        return e.x * Mathf.Abs(dir.x) + e.y * Mathf.Abs(dir.y) + e.z * Mathf.Abs(dir.z);
    }

    /// <summary>
    /// Ponto aleatório na coroa do aro pedido (1..5), à superfície do alvo.
    /// Uniforme por ÁREA (sem tendência para o centro) e afastado dos dardos já
    /// cravados (tenta várias amostras; fica com a mais espaçada).
    /// </summary>
    Vector3 PontoNoAro(int aro)
    {
        aro = Mathf.Clamp(aro, 1, _raios.Length);
        float rExt = _raios[aro - 1] * 0.9f;                    // margem para não cair no limite
        float rInt = aro >= 2 ? _raios[aro - 2] * 1.05f : 0f;   // fora do aro interior
        if (rInt > rExt) rInt = rExt * 0.5f;

        Vector3 melhor = _centro;
        float melhorSeparacao = -1f;
        for (int tentativa = 0; tentativa < 12; tentativa++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            // sqrt-lerp dos quadrados = distribuição uniforme por área na coroa
            float r = Mathf.Sqrt(Mathf.Lerp(rInt * rInt, rExt * rExt, Random.value));
            Vector3 p = _centro + (Mathf.Cos(ang) * _eixoU + Mathf.Sin(ang) * _eixoV) * r + _normal * OffsetCravado;

            float sep = float.MaxValue;
            foreach (var cravado in _pontosCravados)
                sep = Mathf.Min(sep, Vector3.Distance(p, cravado));

            if (sep >= SeparacaoDardos) return p;       // longe o suficiente de todos
            if (sep > melhorSeparacao) { melhorSeparacao = sep; melhor = p; }
        }
        return melhor; // aro cheio: usa a amostra mais espaçada que se arranjou
    }

    // ── Dardo na mão ──────────────────────────────────────────────────
    void AcoplarNovoDardo(ConfigRep cfg)
    {
        if (_dardoAtivo != null) { Destroy(_dardoAtivo.gameObject); _dardoAtivo = null; }
        if (cfg.Mao == null) return;

        var holder = new GameObject("Dardo_Rep" + cfg.RepAtual);
        holder.transform.SetParent(cfg.Mao.transform, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;

        var modelo = ModeloDardo != null ? Instantiate(ModeloDardo) : CriarDardoPlaceholder();
        modelo.SetActive(true);
        modelo.transform.SetParent(holder.transform, false);
        modelo.transform.localPosition = Vector3.zero;
        modelo.transform.localRotation = Quaternion.Euler(RotacaoModeloEuler);
        // Modelo vindo da CENA (ex.: o "dardo" do FBX): preservar o tamanho MUNDO
        // original (o lossyScale inclui o nesting de escala do FBX).
        if (ModeloDardo != null)
            modelo.transform.localScale = ModeloDardo.transform.lossyScale;

        _dardoAtivo = holder.AddComponent<DardoMinijogo>();
        _dardoAtivo.DuracaoVoo = DuracaoVoo;
    }

    /// <summary>Dardo simples (~20 cm): corpo cilíndrico, ponta e penas. Nariz para +Z.</summary>
    static GameObject CriarDardoPlaceholder()
    {
        var raiz = new GameObject("DardoPlaceholder");

        var corpo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(corpo.GetComponent<Collider>());
        corpo.transform.SetParent(raiz.transform, false);
        corpo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // eixo Y do cilindro → +Z
        corpo.transform.localScale    = new Vector3(0.012f, 0.08f, 0.012f);
        corpo.GetComponent<Renderer>().material.color = new Color(0.85f, 0.15f, 0.15f);

        var ponta = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(ponta.GetComponent<Collider>());
        ponta.transform.SetParent(raiz.transform, false);
        ponta.transform.localPosition = new Vector3(0f, 0f, 0.085f);
        ponta.transform.localScale    = Vector3.one * 0.018f;
        ponta.GetComponent<Renderer>().material.color = new Color(0.25f, 0.25f, 0.28f);

        var penas = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(penas.GetComponent<Collider>());
        penas.transform.SetParent(raiz.transform, false);
        penas.transform.localPosition = new Vector3(0f, 0f, -0.085f);
        penas.transform.localScale    = new Vector3(0.035f, 0.035f, 0.03f);
        penas.GetComponent<Renderer>().material.color = new Color(0.95f, 0.95f, 0.95f);

        return raiz;
    }

    // ── Esferas dos waypoints do EXERCÍCIO (não confundir com os aros do
    //    alvo dos dardos — esses são os "aro 1..5" do FBX, só lidos) ─────
    void CriarEsferas(ConfigRep cfg)
    {
        LimparEsferas();
        Color cor = cfg.BracoDireito ? new Color(1f, 0.75f, 0.2f) : new Color(0.2f, 0.7f, 1f);
        for (int i = 0; i < cfg.Waypoints.Length; i++)
            _esferas.Add(CriarEsfera($"WaypointExercicio_{i + 1}", cfg.Waypoints[i], cor));
    }

    void AtualizarVisual()
    {
        if (!MostrarEsferasDebug || _esferas.Count == 0) return;
        if (_alvoAtual >= _percursoWaypoint.Count) return;
        int esferaAtiva = _percursoWaypoint[_alvoAtual];
        for (int i = 0; i < _esferas.Count; i++)
        {
            if (_esferas[i] == null) continue;
            bool ativo = i == esferaAtiva;
            _esferas[i].transform.localScale = Vector3.one * (ativo ? EscalaEsfera * 1.6f : EscalaEsfera);
            var r = _esferas[i].GetComponent<Renderer>();
            var c = r.material.color;
            c.a = ativo ? 1f : 0.35f;
            r.material.color = c;
        }
    }

    GameObject CriarEsfera(string nome, Vector3 pos, Color cor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = nome;
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * EscalaEsfera;
        Destroy(go.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Standard")) { color = cor };
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        go.GetComponent<Renderer>().material = mat;
        return go;
    }

    void LimparEsferas()
    {
        foreach (var e in _esferas) if (e != null) Destroy(e);
        _esferas.Clear();
    }
}
