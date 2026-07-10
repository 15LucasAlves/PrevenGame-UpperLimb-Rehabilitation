using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// GamificationManager — Runner do minijogo dos dardos (uma cena de minijogo).
///
/// Recebe o exercício e as repetições L/R do <see cref="MinigameController"/> (que os lê do
/// <see cref="SessionManager"/>). Corre os blocos de repetições (braço esquerdo, depois direito),
/// gerando a trajetória com <see cref="ExerciciosWaypoints"/>; cada repetição completa
/// (estender→recuar) lança um dardo cuja precisão reflete a qualidade média da repetição.
/// No fim emite <see cref="OnConcluido"/> com a % média — sem UI de seleção/fim (isso é do hub).
/// </summary>
public class GamificationManager : MonoBehaviour
{
    // ── Referências ───────────────────────────────────────────────────
    [Header("Referências")]
    public OmmoEsqueletoJogador Esqueleto;
    public GamificationTarget   Alvo;
    [Tooltip("Prefab do dardo (opcional). Se null, é criado um placeholder.")]
    public GameObject           DardoPrefab;

    // ── UI — HUD ──────────────────────────────────────────────────────
    [Header("UI — HUD")]
    public GameObject      HUDJogo;
    public TextMeshProUGUI TextoPontuacao;
    public TextMeshProUGUI TextoDardos;

    // ── Configuração ──────────────────────────────────────────────────
    [Header("Configuração")]
    public float EscalaEsfera = 0.45f;
    [Tooltip("Raios das zonas de pontuação, interior→exterior (Unity units).")]
    public float[] RaiosZonas      = { 0.50f, 0.80f, 1.10f, 1.40f, 1.70f };
    [Tooltip("Score de cada zona, interior→exterior (0–1).")]
    public float[] PontuacoesZonas = { 1.00f, 0.75f, 0.50f, 0.25f, 0.10f };
    public Color   CorLinha        = new Color(0.2f, 0.9f, 0.3f, 0.9f);

    [Header("Dardo")]
    public Vector3 OffsetDardoLocal  = Vector3.zero;
    public Vector3 RotacaoDardoEuler = Vector3.zero;
    public float   EscalaDardo       = 1f;

    /// <summary>Emitido quando o minijogo termina, com a % média (0–100).</summary>
    public event System.Action<float> OnConcluido;

    // ── Estado do minijogo ────────────────────────────────────────────
    private struct Bloco { public bool BracoDireito; public int Reps; }
    private readonly List<Bloco> _blocos = new List<Bloco>();
    private int  _blocoAtual;
    private int  _repAtualBloco;
    private ExerciciosWaypoints.TipoExercicio _tipo;
    private bool _emJogo;
    private bool _terminado;

    // Waypoints / travessia
    private PrevenGameWaypoint[] _waypoints;
    private Vector3[]            _posicoes;
    private int   _wpAtual;
    private bool  _emVolta;
    private LineRenderer _linha;

    // Pontuação por repetição
    private float _scoreRep;
    private int   _wpRep;
    private float _somaPctDardos;
    private int   _dardosCravados;
    private int   _dardosLancados;
    private int   _totalReps;
    private int   _dardosEmVoo;

    // Dardo / sensor
    private GamificationDart _dardoAtivo;
    private Transform        _sensorTransform;
    private Material         _matBrilho;

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
        if (Esqueleto == null) Esqueleto = FindObjectOfType<OmmoEsqueletoJogador>();
        if (Alvo == null)      Alvo      = FindObjectOfType<GamificationTarget>();
        if (HUDJogo) HUDJogo.SetActive(false);
    }

    void Update()
    {
        if (_emJogo) AtualizarJogo();
    }

    // ── Arranque ──────────────────────────────────────────────────────
    /// <summary>Inicia o minijogo: corre RepsL no braço esquerdo e RepsR no direito.</summary>
    public void StartMinijogo(ExerciciosWaypoints.TipoExercicio tipo, int repsL, int repsR)
    {
        _tipo = tipo;
        _blocos.Clear();
        if (repsL > 0) _blocos.Add(new Bloco { BracoDireito = false, Reps = repsL });
        if (repsR > 0) _blocos.Add(new Bloco { BracoDireito = true,  Reps = repsR });
        if (_blocos.Count == 0) _blocos.Add(new Bloco { BracoDireito = true, Reps = 1 });

        _totalReps = 0;
        foreach (var b in _blocos) _totalReps += b.Reps;

        _somaPctDardos  = 0f;
        _dardosCravados = 0;
        _dardosLancados = 0;
        _dardosEmVoo    = 0;
        _terminado      = false;

        PrepararSensorVisual();

        if (HUDJogo) HUDJogo.SetActive(true);
        _emJogo = true;
        IniciarBloco(0);
        AtualizarHUD();
    }

    void IniciarBloco(int i)
    {
        _blocoAtual    = i;
        _repAtualBloco = 0;
        _scoreRep      = 0f;
        _wpRep         = 0;
        _emVolta       = false;
        _wpAtual       = 0;

        GerarWaypoints(_blocos[i].BracoDireito);
        CriarLinha();
        IniciarDirecao();
        AcoplarNovoDardo();
    }

    // ── Waypoints ─────────────────────────────────────────────────────
    void GerarWaypoints(bool bracoDireito)
    {
        Vector3 posOmbro  = Esqueleto.ObterPosOmbroAtual();
        float   L         = Esqueleto.ComprimentoBraco;
        Vector3 dirFrente = Esqueleto.DirecaoFrente;
        _posicoes = ExerciciosWaypoints.Gerar(_tipo, posOmbro, L, dirFrente, bracoDireito);
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
            wp.ConfigurarZonas(RaiosZonas, PontuacoesZonas, null, EscalaEsfera, false); // invisível
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

    // ── Linha verde (guia) ────────────────────────────────────────────
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

            _repAtualBloco++;
            if (_repAtualBloco >= _blocos[_blocoAtual].Reps)
            {
                _blocoAtual++;
                if (_blocoAtual >= _blocos.Count) ConcluirMinijogo();
                else IniciarBloco(_blocoAtual);
            }
            else
            {
                // Próxima repetição, mesmo braço.
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
            foreach (var r in _sensorTransform.GetComponentsInChildren<Renderer>(true))
                r.enabled = false; // esconde o cubo do sensor — o visual é o dardo
    }

    void AcoplarNovoDardo()
    {
        if (_sensorTransform == null) return;

        var holder = new GameObject("DardoHolder");
        holder.transform.SetParent(_sensorTransform, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;
        // Compensa a lossyScale do pai (CuboSensor a 0.3) → o dardo mantém a sua escala real.
        Vector3 ls = _sensorTransform.lossyScale;
        holder.transform.localScale = new Vector3(
            Mathf.Approximately(ls.x, 0f) ? 1f : 1f / ls.x,
            Mathf.Approximately(ls.y, 0f) ? 1f : 1f / ls.y,
            Mathf.Approximately(ls.z, 0f) ? 1f : 1f / ls.z);

        GameObject modelo = DardoPrefab != null ? Instantiate(DardoPrefab) : CriarDardoPlaceholder();
        modelo.transform.SetParent(holder.transform, false);
        modelo.transform.localPosition = OffsetDardoLocal;
        modelo.transform.localRotation = Quaternion.Euler(RotacaoDardoEuler);
        if (EscalaDardo > 0f && !Mathf.Approximately(EscalaDardo, 1f))
            modelo.transform.localScale = modelo.transform.localScale * EscalaDardo;

        _dardoAtivo = holder.AddComponent<GamificationDart>();
        _dardoAtivo.DuracaoVoo = 0.3f;
        _dardoAtivo.AtivarBrilho(MaterialBrilho());
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
        go.transform.localScale = new Vector3(0.05f, 0.18f, 0.05f);

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
        int aro = AroParaPercentagem(pct);
        _dardosLancados++;

        if (_dardoAtivo != null && Alvo != null)
        {
            _dardosEmVoo++;
            _dardoAtivo.Lancar(Alvo.PontoNoAro(aro), Alvo.transform,
                aoChegar: () =>
                {
                    _dardosCravados++;
                    _somaPctDardos += pct;
                    _dardosEmVoo--;
                    AtualizarHUD();
                    if (_terminado && _dardosEmVoo <= 0) EmitirConclusao();
                });
        }

        _dardoAtivo = null;
        AtualizarHUD();
    }

    float MediaPercentagem()
        => _dardosCravados > 0 ? _somaPctDardos / _dardosCravados : 0f;

    static int AroParaPercentagem(float pct)
    {
        if (pct >= 80f) return 5;
        if (pct >= 60f) return 4;
        if (pct >= 40f) return 3;
        if (pct >= 20f) return 2;
        return 1;
    }

    // ── Conclusão ─────────────────────────────────────────────────────
    void ConcluirMinijogo()
    {
        _emJogo = false;
        _terminado = true;
        LimparWaypointsELinha();
        if (_dardosEmVoo <= 0) EmitirConclusao(); // senão espera o último dardo cravar
    }

    private bool _conclusaoEmitida;
    void EmitirConclusao()
    {
        if (_conclusaoEmitida) return;
        _conclusaoEmitida = true;
        if (HUDJogo) HUDJogo.SetActive(false);
        OnConcluido?.Invoke(MediaPercentagem());
    }

    // ── HUD ───────────────────────────────────────────────────────────
    void AtualizarHUD()
    {
        if (TextoPontuacao) TextoPontuacao.text = $"{MediaPercentagem():F0} %";
        if (TextoDardos)    TextoDardos.text    = $"{_totalReps - _dardosLancados}/{_totalReps}";
    }

    void LimparWaypointsELinha()
    {
        DestruirWaypoints();
        if (_linha) _linha.gameObject.SetActive(false);
    }
}
