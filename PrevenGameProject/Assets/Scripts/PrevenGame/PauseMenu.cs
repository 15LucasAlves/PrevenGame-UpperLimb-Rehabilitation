using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PauseMenu — Menu de pausa das cenas de minijogo.
///
/// ESC alterna a pausa: para o tempo (Time.timeScale = 0), faz fade-in de um overlay com os
/// botões Continue / Main Menu / Exit Game (assets fornecidos). Continue retoma; Main Menu
/// aborta a sessão e volta ao Menu; Exit Game fecha a aplicação.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Overlay")]
    [Tooltip("Raiz do overlay de pausa (fundo + botões). Escondido por omissão.")]
    public GameObject Overlay;
    public CanvasGroup OverlayGroup;   // opcional — para fade
    public float DuracaoFade = 0.25f;

    [Header("Botões")]
    public Button BotaoContinuar;
    public Button BotaoMainMenu;
    public Button BotaoExitGame;

    private bool _pausado;

    void Start()
    {
        BotaoContinuar?.onClick.AddListener(Retomar);
        BotaoMainMenu?.onClick.AddListener(IrParaMenu);
        BotaoExitGame?.onClick.AddListener(SairJogo);

        if (Overlay) Overlay.SetActive(false);
        _pausado = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_pausado) Retomar();
            else          Pausar();
        }
    }

    public void Pausar()
    {
        _pausado = true;
        Time.timeScale = 0f;
        if (Overlay) Overlay.SetActive(true);
        if (OverlayGroup) StartCoroutine(Fade(OverlayGroup, 0f, 1f));
    }

    public void Retomar()
    {
        _pausado = false;
        Time.timeScale = 1f;
        if (Overlay) Overlay.SetActive(false);
    }

    void IrParaMenu()
    {
        Time.timeScale = 1f;
        if (SessionManager.Instancia != null) SessionManager.Instancia.VoltarAoMenu();
        else UnityEngine.SceneManagement.SceneManager.LoadScene(SessionManager.CenaMenu);
    }

    void SairJogo()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    System.Collections.IEnumerator Fade(CanvasGroup cg, float de, float para)
    {
        cg.alpha = de;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, DuracaoFade);
            cg.alpha = Mathf.Lerp(de, para, Mathf.Clamp01(t));
            yield return null;
        }
        cg.alpha = para;
    }
}
