using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Adicionar ao card raiz. Quando o rato entra no card, cicla pelos sprites do exercício;
/// quando sai, volta ao primeiro sprite.
/// </summary>
public class CardAnimacaoHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image    ImagemAlvo;
    public Sprite[] Sprites;
    public float    IntervaloSegundos = 0.35f;

    private Coroutine _coroutine;

    public void OnPointerEnter(PointerEventData _)
    {
        if (Sprites == null || Sprites.Length < 2 || ImagemAlvo == null) return;
        if (_coroutine != null) StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(AnimarSprites());
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (_coroutine != null) { StopCoroutine(_coroutine); _coroutine = null; }
        if (ImagemAlvo != null && Sprites != null && Sprites.Length > 0 && Sprites[0] != null)
            ImagemAlvo.sprite = Sprites[0];
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
