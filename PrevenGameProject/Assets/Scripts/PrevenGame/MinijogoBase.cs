using UnityEngine;

/// <summary>
/// MinijogoBase — Contrato que TODOS os minijogos implementam. O
/// <see cref="GestorMinijogo"/> é dono do loop (blocos por braço, contagem de
/// repetições, HUD, tabela de score, hand-off ao SessionManager); o minijogo só
/// trata do "mundo" de UMA repetição: recebe os waypoints e a mão do jogador,
/// e avisa quando a repetição termina com a percentagem alcançada.
///
/// Para criar um minijogo novo: nova cena + subclasse de MinijogoBase — o
/// gestor e a UI não mudam. (O rework dos dardos implementará este contrato.)
/// </summary>
public abstract class MinijogoBase : MonoBehaviour
{
    /// <summary>Tudo o que um minijogo precisa para correr UMA repetição.</summary>
    public struct ConfigRep
    {
        public ExerciciosWaypoints.TipoExercicio Tipo;
        public bool       BracoDireito;
        public int        RepAtual;    // 1-based
        public int        TotalReps;   // reps do bloco (braço) atual
        public Vector3[]  Waypoints;   // trajetória gerada com o ombro no início da rep
        public MaoJogador Mao;         // posição/rotação da mão (sensor Ommo/comando)

        // Para minijogos com waypoints DINÂMICOS (recalculados por frame a partir
        // do ombro live — o exercício acompanha o jogador quando ele se desloca).
        public RastreadorCorpoJogador Rastreador;
        public float ComprimentoBraco;
    }

    /// <summary>Emitido quando a repetição em curso termina, com a % (0–100) alcançada.</summary>
    public event System.Action<float> OnRepConcluida;

    /// <summary>Prepara e arranca UMA repetição (spawn de alvos/objetos para esta rep).</summary>
    public abstract void PrepararRep(ConfigRep cfg);

    /// <summary>Limpa o mundo do minijogo (fim da sessão ou aborto).</summary>
    public abstract void Terminar();

    /// <summary>Chamado pela subclasse quando a repetição termina.</summary>
    protected void EmitirRepConcluida(float pct)
        => OnRepConcluida?.Invoke(Mathf.Clamp(pct, 0f, 100f));
}
