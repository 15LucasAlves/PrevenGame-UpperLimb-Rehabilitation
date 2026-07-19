using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Adicionar ao card raiz. Em repouso mostra o ÍCONE do minijogo
/// (<see cref="SpriteRepouso"/>); quando o rato entra no card, cicla pelos
/// sprites da demo do exercício; quando sai, volta ao ícone.
/// (Sem ícone atribuído, o repouso é o primeiro frame da demo.)
/// </summary>
public class CardAnimacaoHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image    ImagemAlvo;
    [Tooltip("Ícone do minijogo mostrado em repouso (ex.: \"preven game_dardos\"). " +
             "Sem ele, usa o 1º frame da demo.")]
    public Sprite   SpriteRepouso;
    public Sprite[] Sprites;
    public float    IntervaloSegundos = 0.35f;

    [Header("Rect por estado (valores afinados pelo utilizador 2026-07-18)")]
    [Tooltip("Tamanho do rect em REPOUSO (ícone do minijogo).")]
    public Vector2 TamanhoRepouso = new Vector2(800f, 400f);
    [Tooltip("Posição do rect em REPOUSO.")]
    public Vector2 PosicaoRepouso = new Vector2(-1f, 10f);
    [Tooltip("Tamanho do rect no HOVER — as artes da demo são mais pequenas e precisam de um rect maior.")]
    public Vector2 TamanhoHover = new Vector2(950f, 575f);
    [Tooltip("Posição do rect no HOVER.")]
    public Vector2 PosicaoHover = new Vector2(-5f, 90f);

    private Coroutine _coroutine;

    void OnEnable() => MostrarRepouso();

    public void OnPointerEnter(PointerEventData _)
    {
        if (Sprites == null || Sprites.Length < 2 || ImagemAlvo == null) return;
        if (_coroutine != null) StopCoroutine(_coroutine);
        AplicarRect(TamanhoHover, PosicaoHover);
        _coroutine = StartCoroutine(AnimarSprites());
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (_coroutine != null) { StopCoroutine(_coroutine); _coroutine = null; }
        MostrarRepouso();
    }

    void AplicarRect(Vector2 tamanho, Vector2 posicao)
    {
        if (ImagemAlvo == null) return;
        var rect = ImagemAlvo.rectTransform;
        rect.sizeDelta        = tamanho;
        rect.anchoredPosition = posicao;
    }

    void MostrarRepouso()
    {
        if (ImagemAlvo == null) return;
        AplicarRect(TamanhoRepouso, PosicaoRepouso);
        var s = SpriteRepouso != null ? SpriteRepouso
              : (Sprites != null && Sprites.Length > 0 ? Sprites[0] : null);
        if (s != null)
        {
            ImagemAlvo.sprite = s;
            // Cenas antigas podem ter a cor do placeholder (alfa ~0) serializada.
            if (ImagemAlvo.color.a < 0.99f) ImagemAlvo.color = Color.white;
        }
    }

    IEnumerator AnimarSprites()
    {
        int idx = 0;
        while (true)
        {
            if (Sprites[idx] != null)
                ImagemAlvo.sprite = Sprites[idx];
            idx = (idx + 1) % Sprites.Length;
            yield return new WaitForSeconds(IntervaloSegundos);
        }
    }
}
