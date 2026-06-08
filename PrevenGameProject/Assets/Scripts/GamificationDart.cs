using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GamificationDart — Dardo do modo Gamification (componente no HOLDER do dardo).
///
/// O <see cref="GamificationManager"/> cria um holder acoplado ao transform do sensor Ommo e
/// coloca o modelo do dardo como filho (com offset/rotação/escala configuráveis). Este componente
/// vive no holder: enquanto controlado, o dardo brilha (RimGlow aplicado aos renderers do modelo);
/// ao completar a repetição o manager chama <see cref="Lancar"/> — o holder desacopla, voa em
/// linha reta até ao alvo e, ao cravar, o brilho desliga-se (materiais originais repostos).
/// </summary>
public class GamificationDart : MonoBehaviour
{
    [Tooltip("Duração do voo até ao alvo (segundos).")]
    public float DuracaoVoo = 0.3f;

    public bool EmVoo   { get; private set; }
    public bool Cravado { get; private set; }

    // Brilho
    private readonly List<Renderer> _renderers = new List<Renderer>();
    private Material[][] _materiaisOriginais;
    private bool _brilhoAtivo;

    /// <summary>Aplica o material de brilho a todos os renderers do modelo (guarda os originais).</summary>
    public void AtivarBrilho(Material rim)
    {
        if (rim == null) return;
        CacheRenderers();

        if (_materiaisOriginais == null)
        {
            _materiaisOriginais = new Material[_renderers.Count][];
            for (int i = 0; i < _renderers.Count; i++)
                _materiaisOriginais[i] = _renderers[i].sharedMaterials;
        }

        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] == null) continue;
            int n = Mathf.Max(1, _renderers[i].sharedMaterials.Length);
            var arr = new Material[n];
            for (int j = 0; j < n; j++) arr[j] = rim;
            _renderers[i].materials = arr;
        }
        _brilhoAtivo = true;
    }

    /// <summary>Repõe os materiais originais — o dardo deixa de brilhar.</summary>
    public void DesativarBrilho()
    {
        if (!_brilhoAtivo || _materiaisOriginais == null) return;
        for (int i = 0; i < _renderers.Count && i < _materiaisOriginais.Length; i++)
            if (_renderers[i] != null) _renderers[i].materials = _materiaisOriginais[i];
        _brilhoAtivo = false;
    }

    void CacheRenderers()
    {
        if (_renderers.Count > 0) return;
        _renderers.AddRange(GetComponentsInChildren<Renderer>(true));
    }

    /// <summary>Lança o dardo em linha reta até <paramref name="destino"/> e crava-o lá.</summary>
    public void Lancar(Vector3 destino, Transform paiAlvo)
    {
        if (EmVoo || Cravado) return;
        transform.SetParent(null, true); // desacopla do sensor, mantém pose mundial
        StartCoroutine(Voar(destino, paiAlvo));
    }

    IEnumerator Voar(Vector3 destino, Transform paiAlvo)
    {
        EmVoo = true;
        Vector3 origem = transform.position;

        Vector3 dir = destino - origem;
        if (dir.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, DuracaoVoo);
            transform.position = Vector3.Lerp(origem, destino, Mathf.Clamp01(t));
            yield return null;
        }

        transform.position = destino;
        EmVoo   = false;
        Cravado = true;

        DesativarBrilho();                                 // cravado → sem brilho
        if (paiAlvo != null) transform.SetParent(paiAlvo, true);
    }
}
