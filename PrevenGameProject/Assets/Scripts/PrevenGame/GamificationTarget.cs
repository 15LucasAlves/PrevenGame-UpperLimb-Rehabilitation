using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GamificationTarget — Alvo de 5 aros concêntricos do modo Gamification.
///
/// Os aros são numerados 1 (exterior) … 5 (bullseye). A qualidade da repetição
/// determina o aro; <see cref="PontoNoAro"/> devolve um ponto aleatório dentro da
/// faixa radial desse aro, no plano do alvo.
///
/// Fonte do visual (por ordem de prioridade):
///   1. <see cref="AroPrefabs"/> — os 5 prefabs reais (Alvo1=exterior … Alvo5=bullseye),
///      instanciados concêntricos. Os raios de pontuação derivam das suas escalas/bounds.
///   2. <see cref="ModeloVisual"/> — um único modelo; pontuação paramétrica via RaioExterior.
///   3. Placeholder paramétrico (discos), se nada for atribuído.
/// </summary>
public class GamificationTarget : MonoBehaviour
{
    [Header("Aros reais (Alvo1=exterior … Alvo5=bullseye)")]
    [Tooltip("Os 5 prefabs dos aros, do exterior para o bullseye. Instanciados concêntricos.")]
    public GameObject[] AroPrefabs;

    [Header("Geometria (fallback paramétrico)")]
    [Tooltip("Raio exterior usado só quando não há AroPrefabs (placeholder/ModeloVisual).")]
    public float RaioExterior = 1.5f;

    [Header("Aparência (fallback)")]
    [Tooltip("Modelo único do alvo (usado se não houver AroPrefabs).")]
    public GameObject ModeloVisual;
    [Tooltip("Cria discos placeholder quando não há AroPrefabs nem ModeloVisual.")]
    public bool CriarVisualPlaceholder = true;

    // Cores dos aros 1→5 (exterior→bullseye) — só para o placeholder.
    private static readonly Color[] CoresAro =
    {
        new Color(0.20f, 0.20f, 0.20f),
        new Color(0.25f, 0.45f, 0.95f),
        new Color(0.95f, 0.85f, 0.10f),
        new Color(0.95f, 0.45f, 0.10f),
        new Color(0.95f, 0.15f, 0.10f),
    };

    // Zonas de pontuação calculadas: _raios[0]=exterior … decrescente.
    private float[] _raios;
    private Vector3 _centro;

    void Awake()
    {
        if (AroPrefabs != null && AroPrefabs.Length > 0)
            MontarArosReais();
        else if (ModeloVisual != null)
        {
            var inst = Instantiate(ModeloVisual, transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            CalcularZonasParametricas();
        }
        else
        {
            if (CriarVisualPlaceholder) ConstruirPlaceholder();
            CalcularZonasParametricas();
        }
    }

    /// <summary>
    /// Ponto-mundo aleatório dentro da faixa radial do aro (1=exterior … 5=bullseye).
    /// </summary>
    public Vector3 PontoNoAro(int aro)
    {
        if (_raios == null || _raios.Length == 0) CalcularZonasParametricas();
        aro = Mathf.Clamp(aro, 1, _raios.Length);

        int   k    = aro - 1;                                   // 0 = exterior
        float rOut = _raios[k];
        float rIn  = (k + 1 < _raios.Length) ? _raios[k + 1] : 0f;

        float ang = Random.Range(0f, Mathf.PI * 2f);
        float r   = Random.Range(rIn, rOut);

        // Plano do alvo a partir do transform do root (afina-se a rotação no editor).
        Vector3 offset = (Mathf.Cos(ang) * transform.right + Mathf.Sin(ang) * transform.up) * r;
        return _centro + offset;
    }

    // ── Montagem dos aros reais ───────────────────────────────────────

    void MontarArosReais()
    {
        var instancias = new List<GameObject>();
        foreach (var p in AroPrefabs)
        {
            if (p == null) continue;
            var inst = Instantiate(p, transform);
            // Todos os prefabs partilham a mesma pose autorada → concêntricos.
            // Zeramos a posição local (mesma para todos) e mantemos rotação/escala.
            inst.transform.localPosition = Vector3.zero;
            instancias.Add(inst);
        }

        if (instancias.Count == 0) { CalcularZonasParametricas(); return; }

        // Ordena do maior para o menor pela escala radial (proxy fiável: localScale.x).
        instancias.Sort((a, b) => b.transform.localScale.x.CompareTo(a.transform.localScale.x));

        float raioExteriorMundo = RaioMundo(instancias[0]);
        float escalaExterior    = Mathf.Max(1e-4f, instancias[0].transform.localScale.x);

        _raios = new float[instancias.Count];
        for (int i = 0; i < instancias.Count; i++)
            _raios[i] = raioExteriorMundo * (instancias[i].transform.localScale.x / escalaExterior);

        _centro = CentroMundo(instancias[0]);
    }

    static float RaioMundo(GameObject g)
    {
        var r = g.GetComponentInChildren<Renderer>();
        if (r == null) return 1f;
        Vector3 e = r.bounds.extents;
        return Mathf.Max(e.x, Mathf.Max(e.y, e.z));
    }

    static Vector3 CentroMundo(GameObject g)
    {
        var r = g.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : g.transform.position;
    }

    // ── Fallback paramétrico ──────────────────────────────────────────

    void CalcularZonasParametricas()
    {
        // [R, 0.8R, 0.6R, 0.4R, 0.2R] — exterior → bullseye.
        _raios = new float[5];
        for (int i = 0; i < 5; i++) _raios[i] = RaioExterior * (5 - i) / 5f;
        _centro = transform.position;
    }

    void ConstruirPlaceholder()
    {
        Vector3 n = Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(n, Vector3.up);

        for (int aro = 1; aro <= 5; aro++)
        {
            int   faixaIdx = 5 - aro;
            float raio     = (faixaIdx + 1) * (RaioExterior / 5f);

            var disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disco.name = $"Aro_{aro}";
            Destroy(disco.GetComponent<Collider>());
            disco.transform.SetParent(transform, false);
            disco.transform.localRotation = rot * Quaternion.Euler(90f, 0f, 0f);
            disco.transform.localScale    = new Vector3(raio * 2f, 0.01f, raio * 2f);
            disco.transform.localPosition = -n * (0.002f * aro);

            var mat = new Material(Shader.Find("Standard")) { color = CoresAro[aro - 1] };
            mat.SetFloat("_Glossiness", 0.1f);
            disco.GetComponent<Renderer>().material = mat;
        }
    }
}
