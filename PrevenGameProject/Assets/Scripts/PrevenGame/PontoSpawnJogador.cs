using UnityEngine;

/// <summary>
/// PontoSpawnJogador — Marcador de spawn colocado À MÃO na cena de minijogo:
/// posiciona-o onde o jogador deve "estar" no mundo e roda-o para onde ele
/// deve olhar (a seta azul/forward do gizmo). O <see cref="PosicionadorMundoJogo"/>
/// dá-lhe PRIORIDADE: o mundo é rodado até o forward do spawn ficar paralelo
/// ao face-forward dos óculos e transladado para o spawn cair no jogador.
///
/// Convém ser FILHO da raiz do mundo (FBX) para acompanhar qualquer ajuste
/// manual da cena.
/// </summary>
public class PontoSpawnJogador : MonoBehaviour
{
    [Tooltip("True = o marcador está no CHÃO onde o jogador fica de pé (alinha x/z à cabeça e y ao chão " +
             "do tracking). False = o marcador está à altura dos OLHOS (alinha 3D à cabeça).")]
    public bool NoChao = true;

    void OnDrawGizmos()
    {
        // Esfera no ponto + seta do forward (para onde o jogador vai olhar).
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
        Gizmos.DrawSphere(transform.position, 0.08f);

        Vector3 origem = transform.position + Vector3.up * 0.05f;
        Vector3 ponta  = origem + transform.forward * 0.6f;
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
        Gizmos.DrawLine(origem, ponta);
        Gizmos.DrawLine(ponta, ponta - transform.forward * 0.15f + transform.right * 0.08f);
        Gizmos.DrawLine(ponta, ponta - transform.forward * 0.15f - transform.right * 0.08f);

        if (NoChao)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.65f, 0.1f); // cabeça aproximada
        }
    }
}
