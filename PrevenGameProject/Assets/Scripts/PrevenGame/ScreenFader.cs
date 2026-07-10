using System.Collections;
using UnityEngine;

/// <summary>
/// ScreenFader — Fade in/out de um ecrã inteiro através de um CanvasGroup preto.
///
/// Reutilizável por todas as transições (splash → calibração → seleção → minijogo → score).
/// O CanvasGroup deve cobrir o ecrã (Image preta) num Canvas de sortingOrder alto. Métodos
/// devolvem coroutines para se poder aguardar (yield) a transição.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    [Tooltip("Duração por omissão dos fades (segundos).")]
    public float DuracaoPadrao = 0.4f;

    [Tooltip("Começar opaco (preto) e fazer fade-in ao arrancar a cena.")]
    public bool FadeInAoIniciar = true;

    private CanvasGroup _cg;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        _cg.blocksRaycasts = false;
    }

    void Start()
    {
        if (FadeInAoIniciar)
        {
            _cg.alpha = 1f;
            StartCoroutine(Fade(1f, 0f, DuracaoPadrao, null));
        }
        else
        {
            _cg.alpha = 0f;
        }
    }

    /// <summary>Escurece o ecrã (0→1). Ao terminar chama <paramref name="aoTerminar"/>.</summary>
    public Coroutine FadeOut(System.Action aoTerminar = null, float? duracao = null)
        => StartCoroutine(Fade(_cg.alpha, 1f, duracao ?? DuracaoPadrao, aoTerminar));

    /// <summary>Clareia o ecrã (1→0).</summary>
    public Coroutine FadeIn(System.Action aoTerminar = null, float? duracao = null)
        => StartCoroutine(Fade(_cg.alpha, 0f, duracao ?? DuracaoPadrao, aoTerminar));

    IEnumerator Fade(float de, float para, float duracao, System.Action aoTerminar)
    {
        _cg.blocksRaycasts = true;
        float t = 0f;
        if (duracao <= 0.001f) { _cg.alpha = para; }
        else
        {
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duracao; // unscaled: funciona mesmo em pausa
                _cg.alpha = Mathf.Lerp(de, para, Mathf.Clamp01(t));
                yield return null;
            }
        }
        _cg.alpha = para;
        _cg.blocksRaycasts = para > 0.001f; // opaco bloqueia cliques; transparente deixa passar
        aoTerminar?.Invoke();
    }
}
