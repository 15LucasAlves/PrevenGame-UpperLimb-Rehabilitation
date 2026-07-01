using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainMenuController — Menu principal. Dois botões que carregam os modos de jogo.
/// O <see cref="OmmoBootstrap"/> (na mesma cena) inicializa o Ommo uma vez; como é
/// DontDestroyOnLoad, a ligação persiste ao trocar para qualquer cena de jogo.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Botões")]
    public Button BotaoClinicalTrial;
    public Button BotaoGamification;

    [Header("Nomes das cenas (Build Settings)")]
    public string CenaClinicalTrial = "ClinicalTrial";
    public string CenaGamification  = "Gamification";

    void Start()
    {
        BotaoClinicalTrial?.onClick.AddListener(() => SceneManager.LoadScene(CenaClinicalTrial));
        BotaoGamification?.onClick.AddListener(() => SceneManager.LoadScene(CenaGamification));
    }
}
