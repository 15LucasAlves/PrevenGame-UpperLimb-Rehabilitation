using System.Collections;
using UnityEngine;

/// <summary>
/// MinijogoAviao — Jogo "Hangar" (tema sobre <see cref="MinijogoZonas"/>).
///
/// Exercício: elevação total. O jogador segura um LIGHTSTICK (bastão de
/// sinalização) e "manobra" o avião para fora do hangar: no fim de cada rep o
/// avião avança uma fração da distância total proporcional ao score —
/// <c>DistanciaSaidaTotal ÷ total de reps da cena × pct</c>. A saída ACUMULA:
/// só uma sessão perfeita o tira completamente do hangar.
///
/// O avião é um nó do FBX do hangar (auto-find por nome; corrigível no
/// Inspector); o bastão é o asset "bastão avião.fbx" (ligado pelo instalador).
/// </summary>
public class MinijogoAviao : MinijogoZonas
{
    [Header("Avião")]
    [Tooltip("Transform do avião no mundo (nó do FBX). Vazio = auto-find por nome (aviao/avionet/plane).")]
    public Transform Aviao;
    [Tooltip("O avião taxia EM DIREÇÃO ao jogador (horizontal). Desligado, usa a DirecaoSaidaLocal.")]
    public bool SairEmDirecaoAoJogador = true;
    [Tooltip("Só com SairEmDirecaoAoJogador desligado: direção LOCAL de saída do avião.")]
    public Vector3 DirecaoSaidaLocal = Vector3.forward;
    [Tooltip("Distância total (m) que o avião percorre numa sessão PERFEITA (todas as reps a 100 %) — curta: só sai um pouco do hangar.")]
    public float DistanciaSaidaTotal = 3f;
    [Tooltip("Duração (s) da animação de avanço no fim de cada rep — gradual, como um taxi.")]
    public float DuracaoAvanco = 1.5f;

    [Header("Lightstick")]
    [Tooltip("Modelo do bastão de sinalização (asset \"bastão avião\" — ligado pelo instalador; sem nada, placeholder).")]
    public GameObject ModeloBastao;
    [Tooltip("Rotação local do bastão na mão (holder Bastao_RepN) — afinar com o hardware, como o dardo.")]
    public Vector3 RotacaoBastaoEuler = Vector3.zero;
    [Tooltip("Comprimento REAL do bastão na mão (m) — o asset do Blender vem ×100 e é normalizado para isto.")]
    public float ComprimentoBastao = 0.35f;

    protected override string Etiqueta => "Hangar";

    private int _totalRepsCena = -1; // reps L+R do minijogo atual (para a fração por rep)

    void Start()
    {
        if (Aviao == null) AutoEncontrarAviao();
    }

    // ── Hooks do MinijogoZonas ────────────────────────────────────────
    protected override void AoPrepararRep(ConfigRep cfg)
    {
        if (_totalRepsCena < 0) _totalRepsCena = ObterTotalRepsCena(cfg);

        var modelo = ModeloBastao != null ? ModeloBastao : CriarBastaoPlaceholder();
        var holder = CriarHolderNaMao("Bastao_Rep" + cfg.RepAtual, modelo, RotacaoBastaoEuler);
        if (ModeloBastao == null) Destroy(modelo); // placeholder temporário — o clone ficou no holder
        NormalizarTamanhoObjetoMao(holder, ComprimentoBastao); // o asset do Blender vem ×100
    }

    protected override void AoConcluirRep(float pct)
    {
        float fracao = _totalRepsCena > 0 ? 1f / _totalRepsCena : 0.2f;
        float avanco = DistanciaSaidaTotal * fracao * (pct / 100f);

        if (Aviao == null || avanco <= 0.0001f)
        {
            if (Aviao == null)
                Debug.LogWarning("[Hangar] Sem Transform do avião — o avanço é ignorado (atribui Aviao no Inspector).");
            EmitirRepConcluida(pct);
            return;
        }

        Debug.Log($"[Hangar] ✈ Rep {pct:F0} % → avião avança {avanco:F2} m " +
                  $"(fração {fracao:P0} de {DistanciaSaidaTotal:F1} m).");
        StartCoroutine(AvancarAviao(avanco, pct));
    }

    // ── Internos ──────────────────────────────────────────────────────
    IEnumerator AvancarAviao(float distancia, float pct)
    {
        // Direção de saída: para o JOGADOR (horizontal — taxia do hangar para
        // ele, nunca "para o nada"); fallback: direção local configurada.
        Vector3 dir;
        var xr = GestorXR.Instancia;
        if (SairEmDirecaoAoJogador && xr != null && xr.Cabeca != null)
        {
            dir = xr.Cabeca.position - Aviao.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized
                : Aviao.TransformDirection(DirecaoSaidaLocal.normalized);
        }
        else dir = Aviao.TransformDirection(DirecaoSaidaLocal.normalized);

        Vector3 origem  = Aviao.position;
        Vector3 destino = origem + dir * distancia;
        destino.y = origem.y; // ALTURA nunca muda — o avião taxia, não voa

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, DuracaoAvanco);
            // Suave nas pontas (ease-in-out) — parece rebocado, não teleportado.
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Aviao.position = Vector3.Lerp(origem, destino, k);
            yield return null;
        }
        Aviao.position = destino;

        EmitirRepConcluida(pct);
    }

    /// <summary>Reps L+R do minijogo atual na fila da sessão (fallback: reps do bloco).</summary>
    static int ObterTotalRepsCena(ConfigRep cfg)
    {
        var sm = SessionManager.Instancia;
        if (sm != null && sm.TemAtual)
        {
            int total = sm.Atual.RepsL + sm.Atual.RepsR;
            if (total > 0) return total;
        }
        return Mathf.Max(1, cfg.TotalReps);
    }

    /// <summary>Procura o avião na cena por nome (case-insensitive, ignora a raiz do mundo). Também usado pelo instalador.</summary>
    public void AutoEncontrarAviao()
    {
        string[] candidatos = { "aviao", "avião", "avionet", "plane", "aircraft" };
        foreach (var t in FindObjectsOfType<Transform>(true))
        {
            if (t.parent == null) continue; // a raiz do mundo chama-se "avioneta" — queremos o nó interior
            string nome = t.name.ToLowerInvariant();
            foreach (var c in candidatos)
                if (nome.Contains(c)) { Aviao = t; break; }
            if (Aviao != null) break;
        }

        if (Aviao != null) Debug.Log($"[Hangar] Auto-find: avião = \"{Aviao.name}\".");
        else Debug.LogWarning("[Hangar] Avião não encontrado por nome — atribui o campo Aviao no Inspector " +
                              "(nó do avião dentro do FBX do hangar).");
    }

    /// <summary>Bastão simples (~30 cm): cabo escuro + ponta luminosa laranja. Eixo ao longo de +Z.</summary>
    static GameObject CriarBastaoPlaceholder()
    {
        var raiz = new GameObject("BastaoPlaceholder");

        var cabo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(cabo.GetComponent<Collider>());
        cabo.transform.SetParent(raiz.transform, false);
        cabo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // eixo Y do cilindro → +Z
        cabo.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        cabo.transform.localScale    = new Vector3(0.025f, 0.05f, 0.025f);
        cabo.GetComponent<Renderer>().material.color = new Color(0.15f, 0.15f, 0.18f);

        var ponta = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ponta.GetComponent<Collider>());
        ponta.transform.SetParent(raiz.transform, false);
        ponta.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ponta.transform.localPosition = new Vector3(0f, 0f, 0.2f);
        ponta.transform.localScale    = new Vector3(0.035f, 0.1f, 0.035f);
        var mat = ponta.GetComponent<Renderer>().material;
        mat.color = new Color(1f, 0.45f, 0.05f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.02f));

        return raiz;
    }
}
