using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MinijogoDardos — O minijogo dos dardos (tema sobre <see cref="MinijogoZonas"/>,
/// que trata das zonas de captura, waypoints dinâmicos, arco guia e feed de apoio).
///
/// Tema: um dardo NOVO na mão por repetição; no fim, o dardo é lançado a alta
/// velocidade contra o alvo do bar e crava no aro correspondente ao score:
/// ≥80 % → aro 1 (bullseye) … resto → aro 5. A conclusão da rep só é emitida
/// quando o dardo crava.
///
/// Os aros são os objetos "aro 1".."aro 5" do FBX (concêntricos; geometria lida
/// em runtime — o YAML tem nesting de escala ×100).
/// </summary>
public class MinijogoDardos : MinijogoZonas
{
    [Header("Alvo (auto-encontrado por nome se vazio)")]
    [Tooltip("Aros do alvo, do mais pequeno para o maior: [aro 1 (bullseye) .. aro 5].")]
    public Transform[] Aros;

    [Header("Dardo")]
    [Tooltip("Modelo do dardo (ex.: o objeto \"dardo\" do FBX da cena — é clonado por rep; sem nada, placeholder).")]
    public GameObject ModeloDardo;
    [Tooltip("Rotação local do dardo na mão — aplicada ao holder Dardo_RepN (validado com o dardo do FBX: 0,90,0).")]
    public Vector3 RotacaoModeloEuler = new Vector3(0f, 90f, 0f);
    [Tooltip("Rotação do dardo quando fica cravado no aro (LOCAL relativa ao aro; validado: 0,0,0).")]
    public Vector3 RotacaoCravadoEuler = Vector3.zero;
    [Tooltip("Duração do voo (s) — curta = alta velocidade.")]
    public float DuracaoVoo = 0.15f;
    [Tooltip("Offset do ponto cravado ao longo da normal do alvo (m).")]
    public float OffsetCravado = 0.01f;
    [Tooltip("Separação mínima (m) entre dardos cravados — evita dardos uns em cima dos outros.")]
    public float SeparacaoDardos = 0.035f;

    protected override string Etiqueta => "Dardos";

    // ── Geometria do alvo (recalculada a cada rep — o mundo pode ter sido
    //    posicionado à volta do jogador depois do Start) ────────────────
    private Vector3 _centro;
    private Vector3 _normal, _eixoU, _eixoV;
    private float[] _raios;          // [aro1..aro5] crescente

    private readonly List<GameObject> _dardosCravados = new List<GameObject>();
    private readonly List<Vector3> _pontosCravados = new List<Vector3>(); // p/ espalhar os dardos
    private DardoMinijogo _dardoAtivo;

    void Start()
    {
        EncontrarAros();
        var audio = GestorAudio.Instancia;
        if (audio != null) audio.TocarAmbiente(audio.AmbienteBarDardos);
    }

    // ── Hooks do MinijogoZonas ────────────────────────────────────────
    protected override void AoPrepararRep(ConfigRep cfg)
    {
        CalcularGeometria();

        var modelo = ModeloDardo != null ? ModeloDardo : CriarDardoPlaceholder();
        var holder = CriarHolderNaMao("Dardo_Rep" + cfg.RepAtual, modelo, RotacaoModeloEuler);
        if (ModeloDardo == null) Destroy(modelo); // placeholder temporário — o clone ficou no holder

        _dardoAtivo = holder != null ? holder.AddComponent<DardoMinijogo>() : null;
        if (_dardoAtivo != null) _dardoAtivo.DuracaoVoo = DuracaoVoo;
    }

    protected override void AoConcluirRep(float pct)
    {
        int aro = AroParaPct(pct);
        Debug.Log($"[Dardos] {pct:F0} % → aro {aro}.");

        if (_dardoAtivo == null)
        {
            EmitirRepConcluida(pct);
            return;
        }

        var audio = GestorAudio.Instancia;
        if (audio != null) audio.TocarSfx(audio.SfxLancamentoDardo);

        var dardo = _dardoAtivo;
        _dardoAtivo = null;
        ObjetoMao   = null; // o dardo deixa a mão — a base já não gere a visibilidade
        _dardosCravados.Add(dardo.gameObject);
        Vector3 ponto = PontoNoAro(aro);
        _pontosCravados.Add(ponto);
        dardo.DuracaoVoo = DuracaoVoo;
        dardo.RotacaoCravadoEuler    = RotacaoCravadoEuler;
        dardo.UsarRotacaoCravado     = true;
        dardo.Lancar(ponto, Aros[aro - 1], aoChegar: () => EmitirRepConcluida(pct));
    }

    protected override void AoTerminar()
    {
        foreach (var d in _dardosCravados) if (d != null) Destroy(d);
        _dardosCravados.Clear();
        _pontosCravados.Clear();
        _dardoAtivo = null; // o holder era o ObjetoMao — a base já o destruiu
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
}
