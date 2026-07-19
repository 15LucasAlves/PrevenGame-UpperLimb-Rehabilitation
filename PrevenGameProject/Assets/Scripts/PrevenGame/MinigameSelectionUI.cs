using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MinigameSelectionUI — Ecrã "SELECT MINI GAME": selection box com cards + START/EXIT.
///
/// Reúne os <see cref="SelectionCard"/> selecionados numa fila de minijogos e arranca a sessão
/// via <see cref="SessionManager"/>. O tutorial (helper que não calibrou) é reproduzido ao abrir.
/// </summary>
public class MinigameSelectionUI : MonoBehaviour
{
    [Header("Cards e botões")]
    public SelectionCard[] Cards;
    public Button BotaoStart;
    public Button BotaoExit;

    [Tooltip("CanvasGroup do painel — a interação fica bloqueada enquanto o tutorial fala.")]
    public CanvasGroup GrupoInteracao;

    [Tooltip("Cena de minijogo de FALLBACK (exercícios sem cena própria no mapa CenaParaTipo).")]
    public string CenaMinijogo = "MinijogoDardos";

    /// <summary>Cena de minijogo de cada exercício (fallback: <see cref="CenaMinijogo"/>).</summary>
    public string CenaParaTipo(ExerciciosWaypoints.TipoExercicio tipo)
    {
        switch (tipo)
        {
            case ExerciciosWaypoints.TipoExercicio.FlexaoBraco:    return "MinijogoDardos";
            case ExerciciosWaypoints.TipoExercicio.FlexaoCotovelo: return "MinijogoPomar";
            case ExerciciosWaypoints.TipoExercicio.ElevacaoTotal:  return "MinijogoAviao";
            default:                                               return CenaMinijogo;
        }
    }

    /// <summary>Ação do botão EXIT (definida pelo GameFlowManager; por omissão sai da app).</summary>
    public System.Action AoSair;

    private bool _wired;

    void Awake() => Wire();

    void Wire()
    {
        if (_wired) return;
        _wired = true;
        BotaoStart?.onClick.AddListener(Iniciar);
        BotaoExit?.onClick.AddListener(Sair);
    }

    /// <summary>Mostra o ecrã e reproduz o tutorial; a seleção só fica interativa no fim.</summary>
    public void Mostrar(HelperDialogueManager dialogo, List<HelperFala> tutorial)
    {
        Wire();
        gameObject.SetActive(true);

        if (dialogo != null && tutorial != null && tutorial.Count > 0)
        {
            DefinirInteracao(false); // sem cliques nos cards/botões enquanto o tutorial fala
            dialogo.Reproduzir(tutorial, () => DefinirInteracao(true));
        }
        else DefinirInteracao(true);
    }

    /// <summary>Liga/desliga a interação com todo o ecrã de seleção (cards, steppers, botões, scroll).</summary>
    void DefinirInteracao(bool ativa)
    {
        if (GrupoInteracao == null) return;
        GrupoInteracao.interactable   = ativa;
        GrupoInteracao.blocksRaycasts = ativa;
    }

    void Iniciar()
    {
        var fila = new List<SessionManager.Minijogo>();
        if (Cards != null)
            foreach (var c in Cards)
            {
                if (c == null || !c.Selecionado) continue;
                if (c.RepsL <= 0 && c.RepsR <= 0) continue; // nada a jogar
                fila.Add(new SessionManager.Minijogo
                {
                    Tipo  = c.Tipo,
                    RepsL = c.RepsL,
                    RepsR = c.RepsR,
                    Cena  = CenaParaTipo(c.Tipo),
                });
            }

        if (fila.Count == 0)
        {
            Debug.LogWarning("[Selecao] Nenhum minijogo selecionado (ou reps a 0).");
            return;
        }

        var sm = SessionManager.Instancia;
        if (sm == null) { Debug.LogError("[Selecao] SessionManager ausente."); return; }
        sm.IniciarSessao(fila);
        sm.CarregarProximo();
    }

    void Sair()
    {
        if (AoSair != null) { AoSair(); return; }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
