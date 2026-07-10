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

    [Tooltip("Cena de minijogo a carregar (por agora todos os exercícios usam a cena dos dardos).")]
    public string CenaMinijogo = "MinijogoDardos";

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

    /// <summary>Mostra o ecrã e reproduz o tutorial dos helpers.</summary>
    public void Mostrar(HelperDialogueManager dialogo, List<HelperFala> tutorial)
    {
        Wire();
        gameObject.SetActive(true);
        if (dialogo != null && tutorial != null && tutorial.Count > 0)
            dialogo.Reproduzir(tutorial);
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
                    Cena  = CenaMinijogo,
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
