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

    [Tooltip("Ajuste da imagem+borda ao tamanho do sprite (opcional). Se atribuído, " +
             "a borda passa a envolver a imagem sem faixas pretas.")]
    public AjusteImagemBorda Ajuste;

    void OnEnable()
    {
        if (ImagemAlvo == null) ImagemAlvo = GetComponent<Image>();
        StartCoroutine(Animar());
    }

    void OnDisable() => StopAllCoroutines();

    public void DefinirSprites(Sprite[] novos)
    {
        StopAllCoroutines();
        Sprites = novos;
        if (!isActiveAndEnabled || Sprites == null || Sprites.Length == 0) return;
        if (ImagemAlvo == null) ImagemAlvo = GetComponent<Image>();
        if (Sprites[0] != null)
        {
            ImagemAlvo.sprite = Sprites[0];
            if (Ajuste != null) Ajuste.AjustarSeMudou(Sprites[0]);
        }
        StartCoroutine(Animar());
    }

    IEnumerator Animar()
    {
        if (ImagemAlvo == null || Sprites == null || Sprites.Length == 0) yield break;

        // Mede a 1ª imagem → ajusta a borda/container ao seu tamanho UMA vez;
        // depois anima os frames lá dentro (a borda mantém-se estável).
        if (Ajuste != null && Sprites[0] != null) Ajuste.AjustarSeMudou(Sprites[0]);

        int idx = 0;
        while (true)
        {
            if (Sprites[idx] != null) ImagemAlvo.sprite = Sprites[idx];
            idx = (idx + 1) % Sprites.Length;
            yield return new WaitForSeconds(IntervaloSegundos);
        }
    }
}
