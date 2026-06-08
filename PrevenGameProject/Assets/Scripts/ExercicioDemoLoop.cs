using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ExercicioDemoLoop — UI no canto inferior esquerdo que demonstra o exercício durante o jogo,
/// ciclando continuamente entre as imagens (loop sempre ativo, ao contrário do
/// <see cref="CardAnimacaoHover"/> que só anima no hover).
///
/// Usada em ambos os modos (Gamification e Clinical Trial). As imagens são placeholder até
/// serem fornecidas. A responsividade (canto inferior esquerdo, ~⅓ da largura, escala com o
/// ecrã) é tratada pelo CanvasScaler + âncoras + AspectRatioFitter no momento da construção da UI.
/// </summary>
[RequireComponent(typeof(Image))]
public class ExercicioDemoLoop : MonoBehaviour
{
    [Tooltip("Image onde os sprites são mostrados. Se null, usa o Image deste GameObject.")]
    public Image ImagemAlvo;

    [Tooltip("Sequência de imagens do exercício (placeholder até fornecidas).")]
    public Sprite[] Sprites;

    [Tooltip("Intervalo entre imagens (segundos).")]
    public float IntervaloSegundos = 0.5f;

    void OnEnable()
    {
        if (ImagemAlvo == null) ImagemAlvo = GetComponent<Image>();
        StartCoroutine(Animar());
    }

    void OnDisable() => StopAllCoroutines();

    IEnumerator Animar()
    {
        if (ImagemAlvo == null || Sprites == null || Sprites.Length == 0) yield break;

        int idx = 0;
        while (true)
        {
            if (Sprites[idx] != null) ImagemAlvo.sprite = Sprites[idx];
            idx = (idx + 1) % Sprites.Length;
            yield return new WaitForSeconds(IntervaloSegundos);
        }
    }
}
