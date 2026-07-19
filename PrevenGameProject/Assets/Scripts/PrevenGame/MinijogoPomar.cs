using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MinijogoPomar — Colher fruta (tema sobre <see cref="MinijogoZonas"/>).
///
/// Exercício: flexão do cotovelo. O percurso é INVERTIDO: a rep começa com o
/// braço FLETIDO (mão junto ao corpo/cesta), estica para "apanhar" — no ponto
/// esticado (meio da rep) aparece uma fruta ALEATÓRIA na mão — e volta a
/// fletir para a coletar. No fim da rep a fruta voa para a cesta (ou, sem
/// cesta atribuída, encolhe e desaparece junto à anca) e o pct é emitido.
///
/// As frutas são objetos da CENA (montada à mão, como o bar dos dardos):
/// atribuir em <see cref="FrutasModelos"/> no Inspector, ou deixar o auto-find
/// por nomes comuns tratar disso. Os originais são clonados (ficam desativados).
/// </summary>
public class MinijogoPomar : MinijogoZonas
{
    [Header("Frutas (modelos da cena — clonados por rep)")]
    [Tooltip("Modelos de fruta da cena (um aleatório por rep). Vazio = auto-find por nomes comuns (fruta/maçã/laranja/pera/banana...).")]
    public List<GameObject> FrutasModelos = new List<GameObject>();
    [Tooltip("Rotação local da fruta na mão (holder Fruta_RepN).")]
    public Vector3 RotacaoFrutaEuler = Vector3.zero;

    [Header("Cesta")]
    [Tooltip("Cesta onde a fruta é coletada no fim da rep (o prefab virá depois). Vazio = a fruta encolhe e desaparece junto à anca.")]
    public Transform Cesta;
    [Tooltip("Duração (s) do voo da fruta para a cesta no fim da rep.")]
    public float DuracaoColeta = 0.4f;

    protected override string Etiqueta => "Pomar";
    protected override bool InverterPercurso => true; // rep começa FLETIDA

    // Nomes candidatos para o auto-find das frutas na cena.
    static readonly string[] NOMES_FRUTA =
        { "fruta", "maçã", "maça", "maca", "apple", "laranja", "orange", "pera", "pear", "banana", "morango", "uva" };

    private GameObject _frutaNaMao;   // holder atual (também é o ObjetoMao)
    private bool _frutaApanhada;

    void Start()
    {
        if (FrutasModelos.Count == 0) AutoEncontrarFrutas();
    }

    // ── Hooks do MinijogoZonas ────────────────────────────────────────
    protected override void AoPrepararRep(ConfigRep cfg)
    {
        _frutaNaMao    = null;
        _frutaApanhada = false;
        // A mão começa vazia — a fruta só aparece no ponto esticado.
    }

    protected override void AoCapturarPasso(int passo, int indiceWaypoint)
    {
        // Ponto esticado = último waypoint da ida (meio do percurso).
        if (_frutaApanhada || indiceWaypoint != NumWaypoints - 1) return;
        _frutaApanhada = true;

        var modelo = EscolherFruta();
        if (modelo == null)
        {
            Debug.LogWarning("[Pomar] Sem modelos de fruta na cena — rep segue sem fruta " +
                             "(atribui FrutasModelos no Inspector).");
            return;
        }

        _frutaNaMao = CriarHolderNaMao("Fruta_Rep" + Cfg.RepAtual, modelo, RotacaoFrutaEuler);
        Debug.Log($"[Pomar] 🍎 Fruta \"{modelo.name}\" apanhada!");
    }

    protected override void AoConcluirRep(float pct)
    {
        if (_frutaNaMao == null)
        {
            EmitirRepConcluida(pct);
            return;
        }

        // A fruta deixa a mão e voa para a cesta; o pct é emitido ao chegar.
        var fruta = _frutaNaMao;
        _frutaNaMao = null;
        ObjetoMao   = null; // a base deixa de gerir a visibilidade
        fruta.transform.SetParent(null, true);

        Vector3 destino = Cesta != null
            ? Cesta.position
            : ObterPontoAnca();
        StartCoroutine(VoarParaCesta(fruta, destino, pct));
    }

    protected override void AoTerminar()
    {
        _frutaNaMao = null; // o holder era o ObjetoMao — a base já o destruiu
    }

    // ── Internos ──────────────────────────────────────────────────────
    GameObject EscolherFruta()
    {
        for (int tentativa = 0; tentativa < 8 && FrutasModelos.Count > 0; tentativa++)
        {
            var m = FrutasModelos[Random.Range(0, FrutasModelos.Count)];
            if (m != null) return m;
        }
        return null;
    }

    Vector3 ObterPontoAnca()
    {
        // Sem cesta: um ponto junto à anca do lado do braço ativo.
        if (Cfg.Rastreador != null)
        {
            Vector3 ombro = Cfg.Rastreador.ObterOmbroAtual(Cfg.BracoDireito);
            return ombro + Vector3.down * 0.5f;
        }
        return transform.position;
    }

    IEnumerator VoarParaCesta(GameObject fruta, Vector3 destino, float pct)
    {
        Vector3 origem  = fruta.transform.position;
        Vector3 escala0 = fruta.transform.localScale;
        bool encolher   = Cesta == null; // sem cesta a fruta desaparece; com cesta fica lá dentro

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, DuracaoColeta);
            float k = Mathf.Clamp01(t);
            fruta.transform.position = Vector3.Lerp(origem, destino, k);
            if (encolher) fruta.transform.localScale = escala0 * (1f - k);
            yield return null;
        }

        if (encolher) Destroy(fruta);
        else fruta.transform.SetParent(Cesta, true);

        EmitirRepConcluida(pct);
    }

    /// <summary>Procura modelos de fruta na cena por nomes comuns (case-insensitive). Também usado pelo instalador.</summary>
    public void AutoEncontrarFrutas()
    {
        foreach (var t in FindObjectsOfType<Transform>(true))
        {
            if (t.GetComponent<Renderer>() == null && t.GetComponentInChildren<Renderer>(true) == null) continue;
            string nome = t.name.ToLowerInvariant();
            foreach (var candidato in NOMES_FRUTA)
            {
                if (!nome.Contains(candidato)) continue;
                // Evita apanhar filhos de uma fruta já registada.
                bool jaDentro = false;
                foreach (var f in FrutasModelos)
                    if (f != null && t.IsChildOf(f.transform)) { jaDentro = true; break; }
                if (!jaDentro) FrutasModelos.Add(t.gameObject);
                break;
            }
        }

        if (FrutasModelos.Count > 0)
            Debug.Log($"[Pomar] Auto-find: {FrutasModelos.Count} fruta(s) na cena — " +
                      string.Join(", ", FrutasModelos.ConvertAll(f => f != null ? f.name : "?")));
        else
            Debug.LogWarning("[Pomar] Nenhuma fruta encontrada por nome — atribui FrutasModelos no Inspector.");
    }
}
