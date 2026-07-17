using System.Collections;
using UnityEngine;

/// <summary>
/// PontoDePartidaJogador — Coloca a vista do jogador na pose inicial da cena de
/// minijogo: **x/z do marcador** (a Camera importada do FBX, desativada) e
/// **altura da referência** (por defeito o "aro 1" — a cabeça fica ao nível do
/// centro do alvo dos dardos).
///
/// Como o rig é persistente e tracked, isto é um teleporte do RIG (não da
/// câmara). O MESMO delta é aplicado à BaseStation do Ommo da cena — senão a
/// mão (que vive no referencial da base, ancorado ao mundo real pelo QR)
/// ficaria para trás. Depois do teleporte, o <see cref="AlinhadorOmmoQr"/> da
/// cena é desativado (um re-alinhamento a meio faria a base saltar de volta).
/// Em Modo Comando o delta da base é irrelevante mas inofensivo.
/// </summary>
public class PontoDePartidaJogador : MonoBehaviour
{
    [Tooltip("Marcador da posição/yaw inicial da cabeça (ex.: a Camera do FBX, desativada).")]
    public Transform Ponto;

    [Tooltip("Referência de ALTURA da cabeça (ex.: o \"aro 1\" — centro do alvo). " +
             "Se vazio, tenta encontrar o \"aro 1\"; sem nada, usa a altura do Ponto.")]
    public Transform ReferenciaAltura;

    [Tooltip("Virar o jogador para a ReferenciaAltura (o aro 1/alvo) em vez de usar o yaw do marcador — imune a câmaras do Blender rodadas.")]
    public bool OlharParaAlvo = true;

    [Tooltip("Correção de yaw (graus) aplicada ao yaw final (marcador ou olhar-para-alvo).")]
    public float YawExtraGraus = 0f;

    IEnumerator Start()
    {
        if (ReferenciaAltura == null)
        {
            var aro1 = GameObject.Find("aro 1");
            if (aro1 != null) ReferenciaAltura = aro1.transform;
        }
        // Sem marcador NEM referência não há nada para fazer; só com a referência,
        // ainda dá para VIRAR o jogador para o alvo (fica onde está).
        if (Ponto == null && ReferenciaAltura == null) yield break;

        var xr = GestorXR.ObterOuCriar();

        // Espera pelo VR + primeira pose válida da cabeça.
        float timeout = Time.unscaledTime + 10f;
        while (Time.unscaledTime < timeout &&
               (xr.Cabeca == null || xr.Cabeca.position.y < 0.3f))
            yield return null;
        if (xr.Cabeca == null || xr.Cabeca.position.y < 0.3f)
        {
            Debug.LogWarning("[PontoDePartida] Cabeça sem tracking válido — teleporte cancelado (headset posto?).");
            yield break;
        }

        // +1 frame: deixa o AlinhadorOmmoQr aplicar a pose persistida à BaseStation.
        yield return null;

        // Posição alvo: x/z do marcador (sem marcador: fica onde está); altura da referência (aro 1).
        Vector3 posAlvo = Ponto != null ? Ponto.position : xr.Cabeca.position;
        if (ReferenciaAltura != null) posAlvo.y = ReferenciaAltura.position.y;

        // Yaw: virado para o alvo (garante os aros logo à frente ao entrar).
        Quaternion rotAlvo;
        if (OlharParaAlvo && ReferenciaAltura != null)
        {
            Vector3 paraAlvo = ReferenciaAltura.position - posAlvo;
            paraAlvo.y = 0f;
            rotAlvo = paraAlvo.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(paraAlvo.normalized, Vector3.up)
                : (Ponto != null ? Ponto.rotation : xr.Cabeca.rotation);
        }
        else rotAlvo = Ponto != null ? Ponto.rotation : xr.Cabeca.rotation;

        var alvo = new Pose(posAlvo, rotAlvo * Quaternion.Euler(0f, YawExtraGraus, 0f));
        if (!xr.TeleportarCabecaPara(alvo, out var pivot, out var deltaRot, out var deltaPos,
                                     manterAlturaReal: false)) yield break;

        // A base Ommo acompanha o teleporte para a mão ficar colada ao jogador.
        var dm = FindObjectOfType<OmmoDeviceManager>();
        if (dm != null && dm.BaseStation != null)
        {
            var bt = dm.BaseStation.transform;
            bt.SetPositionAndRotation(
                pivot + deltaRot * (bt.position - pivot) + deltaPos,
                deltaRot * bt.rotation);
        }

        // Sem re-alinhamentos QR nesta cena (fariam a base saltar de volta ao mundo real).
        var alinhador = FindObjectOfType<AlinhadorOmmoQr>();
        if (alinhador != null) alinhador.enabled = false;

        Debug.Log($"[PontoDePartida] Jogador colocado em ({posAlvo.x:F2}, {posAlvo.y:F2}, {posAlvo.z:F2}) " +
                  $"(x/z do marcador \"{Ponto.name}\", altura de \"{(ReferenciaAltura != null ? ReferenciaAltura.name : Ponto.name)}\").");
    }
}
