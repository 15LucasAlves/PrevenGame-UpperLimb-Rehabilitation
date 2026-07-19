using System.Collections;
using UnityEngine;

/// <summary>
/// DardoMinijogo — Voo do dardo: despega da mão, aponta o nariz à trajetória e
/// voa em linha reta a alta velocidade até ao ponto de destino, onde crava
/// (fica filho do alvo) e avisa o chamador.
/// </summary>
public class DardoMinijogo : MonoBehaviour
{
    [Tooltip("Duração do voo em segundos (curta = alta velocidade).")]
    public float DuracaoVoo = 0.15f;

    [Tooltip("Aplicar uma rotação MUNDO fixa ao cravar (em vez de manter a do voo).")]
    public bool UsarRotacaoCravado = false;
    [Tooltip("Rotação mundo final quando cravado.")]
    public Vector3 RotacaoCravadoEuler = Vector3.zero;

    public bool EmVoo   { get; private set; }
    public bool Cravado { get; private set; }

    /// <summary>Lança o dardo para o destino; ao cravar fica filho de paiAlvo e chama aoChegar.</summary>
    public void Lancar(Vector3 destino, Transform paiAlvo, System.Action aoChegar = null)
    {
        if (EmVoo || Cravado) return;
        transform.SetParent(null, true); // larga a mão, mantém a pose mundo
        StartCoroutine(Voar(destino, paiAlvo, aoChegar));
    }

    IEnumerator Voar(Vector3 destino, Transform paiAlvo, System.Action aoChegar)
    {
        EmVoo = true;

        Vector3 origem = transform.position;
        Vector3 dir    = destino - origem;
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
        if (paiAlvo != null) transform.SetParent(paiAlvo, true);
        if (UsarRotacaoCravado)
        {
            // Rotação LOCAL relativa ao aro, aplicada DEPOIS do SetParent — o
            // Inspector lê exatamente RotacaoCravadoEuler (0,0,0) e o dardo fica
            // direito mesmo com o mundo rodado à volta do jogador. O clone do
            // modelo (filho) também é zerado (perdia a pose de mão 0,90,0).
            transform.localRotation = Quaternion.Euler(RotacaoCravadoEuler);
            foreach (Transform filho in transform)
                filho.localRotation = Quaternion.identity;
        }
        aoChegar?.Invoke();
    }
}
