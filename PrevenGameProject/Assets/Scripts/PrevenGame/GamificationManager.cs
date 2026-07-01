using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// GamificationManager — Orquestrador do modo Gamification (lançar dardos a um alvo).
///
/// Reutiliza a lógica do Exercício 1 (Flexão do Braço) e a pontuação por zonas do
/// <see cref="PrevenGameWaypoint"/> da Clinical Trial, mas:
///   • os waypoints são INVISÍVEIS (só zonas de pontuação) — ver ConfigurarZonas(..., visivel:false);
///   • o único guia visível é uma LINHA VERDE (trajeto ótimo);
///   • cada repetição completa (estender → recuar → estender) lança um dardo cuja precisão
///     (aro atingido) corresponde à qualidade média dessa repetição.
///
/// Fluxo: AguardarCalibracao → AguardarSelecao → EmJogo → Concluido.
/// </summary>
public class GamificationManager : MonoBehaviour
{
    // ── Referências ───────────────────────────────────────────────────
    [Header("Referências")]
    public OmmoEsqueletoJogador  Esqueleto;
    public OmmoCalibracaoManager CalibracaoManager;
    public GamificationTarget    Alvo;
    [Tooltip("Prefab do dardo (opcional). Se null, é criado um placeholder.")]
    public GameObject            DardoPrefab;

    // ── UI — Seleção (1 card) ─────────────────────────────────────────
    [Header("UI — Seleção")]
    public GameObject      PainelSelecao;
    public Button          BotaoBracoDireito;
    public Button          BotaoBracoEsquerdo;
    public Button          BotaoMenos;
    public Button          BotaoMais;
    public TextMeshProUGUI TextoReps;
    public Button          BotaoIniciar;
    public Image           ImagemExercicio;
    public Button          BotaoVoltarMenu;

    // ── UI — HUD ──────────────────────────────────────────────────────
    [Header("UI — HUD")]
    public GameObject      HUDJogo;
    public TextMeshProUGUI TextoPontuacao;
    public TextMeshProUGUI TextoDardos;

    // ── UI — Fim ──────────────────────────────────────────────────────
    [Header("UI — Fim")]
    public GameObject      PainelFim;
    public TextMeshProUGUI TextoResultado;
    public Button          BotaoNovaSessao;

    // ── Configuração ──────────────────────────────────────────────────
    [Header("Configuração")]
    public int   NumRepeticoes = 5;
    public float EscalaEsfera  = 0.45f;
    [Tooltip("Raios das zonas de pontuação, interior→exterior (Unity units).")]
    public float[] RaiosZonas      = { 0.50f, 0.80f, 1.10f, 1.40f, 1.70f };
    [Tooltip("Score de cada zona, interior→exterior (0–1).")]
    public float[] PontuacoesZonas = { 1.00f, 0.75f, 0.50f, 0.25f, 0.10f };
    public Color   CorLinha        = new Color(0.2f, 0.9f, 0.3f, 0.9f);

    [Header("Dardo")]
    [Tooltip("Pose do modelo do dardo dentro do holder — afina para a ponta apontar a +Z (sentido do voo).")]
    public Vector3 OffsetDardoLocal  = Vector3.zero;
    public Vector3 RotacaoDardoEuler = Vector3.zero;
    [Tooltip("Multiplicador de escala do modelo (1 = escala do prefab).")]
    public float   EscalaDardo       = 1f;

    // ── Estado ────────────────────────────────────────────────────────
    private enum Estado { AguardarCalibracao, AguardarSelecao, EmJogo, Concluido }
    private Estado _estado = Estado.AguardarCalibracao;

    // Seleção
    private bool _bracoDireito = true;
    private int  _reps         = 5;

    // Waypoints / travessia (mesmo padrão do PrevenGameManager)
    private PrevenGameWaypoint[] _waypoints;
    private Vector3[]            _posicoes;
    private int   _wpAtual  = 0;
    private bool  _emVolta  = false;
    private int   _repAtual = 0;
    private LineRenderer _linha;

    // Pontuação por repetição
    private float _scoreRep       = 0f;
    private int   _wpRep          = 0;
    private float _somaPctDardos  = 0f; // soma das percentagens dos dardos JÁ CRAVADOS (para a média)
    private int   _dardosLancados = 0;  // dardos lançados (contador "Dardos x/X")
    private int   _dardosCravados = 0;  // dardos que chegaram ao alvo (denominador da média)

    // Dardo / sensor
    private GamificationDart _dardoAtivo;
    private Transform        _sensorTransform;
    private Material         _matBrilho;

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
        if (CalibracaoManager == null) CalibracaoManager = FindObjectOfType<OmmoCalibracaoManager>();
        if (Esqueleto == null)         Esqueleto         = FindObjectOfType<OmmoEsqueletoJogador>();
        if (Alvo == null)              Alvo              = FindObjectOfType<GamificationTarget>();

        _reps = Mathf.Max(1, NumRepeticoes);

        BotaoIniciar?.onClick.AddListener(IniciarSessao);
        BotaoNovaSessao?.onClick.AddListener(MostrarSelecao);
        BotaoVoltarMenu?.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
        BotaoBracoDireito?.onClick.AddListener(() => ToggleBraco(true));
        BotaoBracoEsquerdo?.onClick.AddListener(() => ToggleBraco(false));
        BotaoMenos?.onClick.AddListener(() => AlterarReps(-1));
        BotaoMais?.onClick.AddListener(() => AlterarReps(+1));

        if (HUDJogo)       HUDJogo.SetActive(false);
        if (PainelFim)     PainelFim.SetActive(false);
        if (PainelSelecao) PainelSelecao.SetActive(false);
    }

    void Update()
    {
        switch (_estado)
        {
            case Estado.AguardarCalibracao:
                if (CalibracaoManager != null && CalibracaoManager.Calibrado &&
                    (CalibracaoManager.PainelCalibracao == null ||
                     !CalibracaoManager.PainelCalibracao.activeSelf))
                    MostrarSelecao();
                break;

            case Estado.EmJogo:
                AtualizarJogo();
                break;
        }
    }

    // ── Seleção ───────────────────────────────────────────────────────
    void MostrarSelecao()
    {
        _estado = Estado.AguardarSelecao;

        // Esconde o esqueleto — no Gamification o visual é simplificado.
        if (Esqueleto) Esqueleto.AtivacaoEsqueleto(false);

        if (HUDJogo)       HUDJogo.SetActive(false);
        if (PainelFim)     PainelFim.SetActive(false);
        if (PainelSelecao) PainelSelecao.SetActive(true);

        LimparSessao();
        AtualizarUISelecao();
    }

    void AtualizarUISelecao()
    {
        if (TextoReps) TextoReps.text = _reps.ToString();

        if (BotaoBracoDireito)
            BotaoBracoDireito.GetComponent<Image>().color =
                _bracoDireito ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.35f, 0.35f, 0.35f);
        if (BotaoBracoEsquerdo)
            BotaoBracoEsquerdo.GetComponent<Image>().color =
                _bracoDireito ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.2f, 0.7f, 0.3f);
    }

    void ToggleBraco(bool direito)
    {
        _bracoDireito = direito;
        if (ImagemExercicio != null)
        {
            var s = ImagemExercicio.rectTransform.localScale;
            s.x = direito ? 1f : -1f;
            ImagemExercicio.rectTransform.localScale = s;
        }
        AtualizarUISelecao();
    }

    void AlterarReps(int delta)
    {
        _reps = Mathf.Clamp(_reps + delta, 1, 20);
        AtualizarUISelecao();
    }

    void IniciarSessao()
    {
        NumRepeticoes   = _reps;
        _somaPctDardos  = 0f;
        _dardosLancados = 0;
        _dardosCravados = 0;
        _scoreRep       = 0f;
        _wpRep          = 0;

        if (PainelSelecao) PainelSelecao.SetActive(false);

        PrepararSensorVisual();
        IniciarJogo();
    }

    // ── Jogo ──────────────────────────────────────────────────────────
    void IniciarJogo()
    {
        _estado   = Estado.EmJogo;
        _repAtual = 0;
        _emVolta  = false;
        _wpAtual  = 0;

        GerarWaypointsEX1();
        CriarLinha();
        IniciarDirecao();
        AcoplarNovoDardo();

        if (HUDJogo)   HUDJogo.SetActive(true);
        if (PainelFim) PainelFim.SetActive(false);
        AtualizarHUD();
    }

    /// <summary>EX1 — Flexão do Braço: bícep horizontal para a frente, cotovelo 0°→144°.</summary>
    void GerarWaypointsEX1()
    {
        Vector3 posOmbro  = Esqueleto.ObterPosOmbroAtual();
        float   L         = Esqueleto.ComprimentoBraco > 0.05f ? Esqueleto.ComprimentoBraco : 0.44f;
        Vector3 dirFrente = Esqueleto.DirecaoFrente == Vector3.zero ? Vector3.forward : Esqueleto.DirecaoFrente;

        float Lu = L * (18.6f / 44.0f); // braço superior
        float Lf = L * (14.6f / 44.0f); // antebraço

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
    }

    void InstanciarWaypoints()
    {
        DestruirWaypoints();
        _waypoints = new PrevenGameWaypoint[_posicoes.Length];
        for (int i = 0; i < _posicoes.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"WaypointGamif_{i}";
            go.transform.position   = _posicoes[i];
            go.transform.localScale = Vector3.one * EscalaEsfera;
            Destroy(go.GetComponent<SphereCollider>());

            var wp = go.AddComponent<PrevenGameWaypoint>();
            // Modo invisível: as zonas só pontuam, sem visual.
            wp.ConfigurarZonas(RaiosZonas, PontuacoesZonas, null, EscalaEsfera, false);
            _waypoints[i] = wp;
        }
    }

    void DestruirWaypoints()
    {
        if (_waypoints == null) return;
        foreach (var wp in _waypoints)
            if (wp != null) Destroy(wp.gameObject);
        _waypoints = null;
    }

    // ── Linha verde (único guia visível) ──────────────────────────────
    void CriarLinha()
    {
        if (_linha == null)
        {
            var go = new GameObject("LinhaGuiaGamif");
            _linha = go.AddComponent<LineRenderer>();
            _linha.material      = new Material(Shader.Find("Sprites/Default"));
            _linha.startWidth    = 0.03f;
            _linha.endWidth      = 0.03f;
            _linha.useWorldSpace = true;
        }
        _linha.gameObject.SetActive(true);
        _linha.startColor = CorLinha;
        _linha.endColor   = CorLinha;
        AtualizarLinha();
    }

    void AtualizarLinha()
    {
        if (_linha == null || _posicoes == null) return;
        _linha.positionCount = _posicoes.Length;
        for (int i = 0; i < _posicoes.Length; i++)
        {
            int idx = _emVolta ? _posicoes.Length - 1 - i : i;
            _linha.SetPosition(i, _posicoes[idx]);
        }
    }

    // ── Travessia ida-volta ───────────────────────────────────────────
    void IniciarDirecao()
    {
        _wpAtual = 0;
        foreach (var wp in _waypoints) wp.Repor();
        _waypoints[IndiceWaypointAtual()].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
        AtualizarLinha();
    }

    int IndiceWaypointAtual()
        => _emVolta ? (_posicoes.Length - 1 - _wpAtual) : _wpAtual;

    void AtualizarJogo()
    {
        if (Esqueleto == null || _waypoints == null) return;

        Vector3 posPalma = Esqueleto.ObterPosPalmaAtual();
        int     idx      = IndiceWaypointAtual();

        if (_waypoints[idx].VerificarToque(posPalma))
        {
            _scoreRep += _waypoints[idx].UltimaPontuacao;
            _wpRep++;
            AvancarWaypoint();
        }
    }

    void AvancarWaypoint()
    {
        _wpAtual++;
        if (_wpAtual >= _posicoes.Length)
            ConcluirDirecao();
        else
            _waypoints[IndiceWaypointAtual()].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
    }

    void ConcluirDirecao()
    {
        if (!_emVolta)
        {
            // IDA concluída → VOLTA a partir do penúltimo (salta o extremo recém-visitado).
            _emVolta = true;
            _wpAtual = 1;
            foreach (var wp in _waypoints) wp.Repor();
            _waypoints[IndiceWaypointAtual()].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
            AtualizarLinha();
        }
        else
        {
            // VOLTA concluída → repetição completa: pontua e lança o dardo.
            float pct = _wpRep > 0 ? (_scoreRep / _wpRep) * 100f : 0f;
            _scoreRep = 0f;
            _wpRep    = 0;
            LancarDardo(pct);

            _repAtual++;
            if (_repAtual >= NumRepeticoes)
            {
                ConcluirSessao();
            }
            else
            {
                _emVolta = false;
                _wpAtual = 1;
                foreach (var wp in _waypoints) wp.Repor();
                _waypoints[IndiceWaypointAtual()].SetEstado(PrevenGameWaypoint.EstadoWaypoint.Ativo);
                AtualizarLinha();
                AcoplarNovoDardo();
            }
        }
    }

    // ── Dardos ────────────────────────────────────────────────────────
    void PrepararSensorVisual()
    {
        var device = FindObjectOfType<OmmoDevice>();
        if (device == null || device.NumeroSensores == 0) return;

        _sensorTransform = device.ObterTransformSensor(0);
        if (_sensorTransform != null)
        {
            // Esconde o cubo do sensor — o visual controlado passa a ser o dardo.
            foreach (var r in _sensorTransform.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
        }
    }

    void AcoplarNovoDardo()
    {
        if (_sensorTransform == null) return;

        // Holder controlado pela mão; o modelo do dardo é filho com pose configurável.
        var holder = new GameObject("DardoHolder");
        holder.transform.SetParent(_sensorTransform, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;

        GameObject modelo = DardoPrefab != null ? Instantiate(DardoPrefab) : CriarDardoPlaceholder();
        modelo.transform.SetParent(holder.transform, false);
        modelo.transform.localPosition = OffsetDardoLocal;
        modelo.transform.localRotation = Quaternion.Euler(RotacaoDardoEuler);
        if (EscalaDardo > 0f && !Mathf.Approximately(EscalaDardo, 1f))
            modelo.transform.localScale = modelo.transform.localScale * EscalaDardo;

        _dardoAtivo = holder.AddComponent<GamificationDart>();
        _dardoAtivo.DuracaoVoo = 0.3f;
        _dardoAtivo.AtivarBrilho(MaterialBrilho()); // brilha enquanto controlado e em voo
    }

    Material MaterialBrilho()
    {
        if (_matBrilho == null)
        {
            var sh = Shader.Find("PrevenGame/RimGlow") ?? Shader.Find("Standard");
            _matBrilho = new Material(sh) { color = new Color(0.9f, 0.95f, 1f) };
            if (_matBrilho.HasProperty("_RimColor"))
            {
                _matBrilho.SetColor("_RimColor", Color.white);
                _matBrilho.SetFloat("_RimPower", 2.5f);
                _matBrilho.SetFloat("_RimIntensity", 2.5f);
            }
        }
        return _matBrilho;
    }

    GameObject CriarDardoPlaceholder()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Dardo";
        Destroy(go.GetComponent<Collider>());
        go.transform.localScale = new Vector3(0.05f, 0.18f, 0.05f); // fino e comprido

        var shader = Shader.Find("PrevenGame/RimGlow") ?? Shader.Find("Standard");
        var mat = new Material(shader) { color = new Color(0.9f, 0.9f, 0.95f) };
        if (mat.HasProperty("_RimColor"))
        {
            mat.SetColor("_RimColor", Color.white);
            mat.SetFloat("_RimPower", 3f);
            mat.SetFloat("_RimIntensity", 2.2f);
        }
        go.GetComponent<Renderer>().material = mat;
        return go;
    }

    void LancarDardo(float pct)
    {
        // A % desta repetição (pct) decide o aro AGORA — independente da média acumulada.
        int aro = AroParaPercentagem(pct);
        _dardosLancados++;

        if (_dardoAtivo != null && Alvo != null)
            _dardoAtivo.Lancar(Alvo.PontoNoAro(aro), Alvo.transform,
                aoChegar: () =>
                {
                    _dardosCravados++;
                    _somaPctDardos += pct;
                    AtualizarHUD();
                    // Se a sessão já terminou (último dardo ainda em voo), corrige o resultado final.
                    if (_estado == Estado.Concluido) MostrarResultadoFinal();
                });

        _dardoAtivo = null;
        AtualizarHUD(); // atualiza "Dardos x/X" já; a média só sobe quando o dardo crava
    }

    /// <summary>Média das percentagens dos dardos que já CRAVARAM (0 se ainda nenhum).</summary>
    float MediaPercentagem()
        => _dardosCravados > 0 ? _somaPctDardos / _dardosCravados : 0f;

    /// <summary>Mapeia a qualidade da repetição (0–100%) ao aro atingido (1 exterior … 5 bullseye).</summary>
    static int AroParaPercentagem(float pct)
    {
        if (pct >= 80f) return 5;
        if (pct >= 60f) return 4;
        if (pct >= 40f) return 3;
        if (pct >= 20f) return 2;
        return 1; // <20% (inclui <10%) → aro exterior
    }

    // ── Fim ───────────────────────────────────────────────────────────
    void ConcluirSessao()
    {
        _estado = Estado.Concluido;
        LimparWaypointsELinha();

        if (HUDJogo)   HUDJogo.SetActive(false);
        if (PainelFim) PainelFim.SetActive(true);
        MostrarResultadoFinal();
    }

    /// <summary>Atualiza o texto do painel final com a média atual (corrige-se quando o último dardo crava).</summary>
    void MostrarResultadoFinal()
    {
        if (TextoResultado)
            TextoResultado.text = $"🎯 Sessão concluída!\nPontuação: {MediaPercentagem():F0} %";
    }

    // ── HUD ───────────────────────────────────────────────────────────
    void AtualizarHUD()
    {
        if (TextoPontuacao) TextoPontuacao.text = $"{MediaPercentagem():F0} %";
        if (TextoDardos)    TextoDardos.text    = $"{NumRepeticoes - _dardosLancados}/{NumRepeticoes}";
    }

    // ── Limpeza ───────────────────────────────────────────────────────
    void LimparWaypointsELinha()
    {
        DestruirWaypoints();
        if (_linha) _linha.gameObject.SetActive(false);
    }

    void LimparSessao()
    {
        LimparWaypointsELinha();
        if (_dardoAtivo != null) { Destroy(_dardoAtivo.gameObject); _dardoAtivo = null; }
    }
}
