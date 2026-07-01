using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AjusteImagemBorda — Dimensiona uma imagem a partir do tamanho REAL do seu sprite
/// (preservando o rácio, dentro de um limite máximo) e ajusta a borda/container para
/// envolver exatamente a imagem, com padding uniforme.
///
/// Fluxo pretendido: obter tamanho do sprite → criar/ajustar a borda ao tamanho → animar.
/// Assim não há faixas pretas (letterbox) à volta de imagens cujo rácio não coincide com
/// um container fixo — a borda passa a "abraçar" a imagem.
///
/// Chamar <see cref="AjustarSeMudou"/> sempre que o sprite muda (ex.: no loop da demo ou
/// no hover do card). O ajuste real só ocorre quando o sprite é diferente do anterior.
///
/// Requisitos de layout: tanto a Imagem como o Container devem ter âncoras "pontuais"
/// (anchorMin == anchorMax) para que <c>sizeDelta</c> corresponda ao tamanho real em px.
/// A imagem deve estar posicionada dentro do container com um offset igual a <see cref="Borda"/>
/// a partir do canto do pivot, para o padding ficar simétrico.
/// </summary>
public class AjusteImagemBorda : MonoBehaviour
{
    [Tooltip("Imagem do exercício a dimensionar.")]
    public Image Imagem;

    [Tooltip("Borda/fundo que envolve a imagem (opcional). Fica com o tamanho da imagem + 2×Borda.")]
    public RectTransform Container;

    [Tooltip("Limite máximo (px de referência do CanvasScaler). A imagem cabe aqui preservando o rácio.")]
    public Vector2 TamanhoMaximo = new Vector2(560f, 320f);

    [Tooltip("Espessura da borda à volta da imagem (px).")]
    public float Borda = 8f;

    private Sprite _ultimo;

    /// <summary>Ajusta imagem+borda ao sprite, só se for diferente do último aplicado.</summary>
    public void AjustarSeMudou(Sprite sprite)
    {
        if (sprite == _ultimo) return;
        _ultimo = sprite;
        Ajustar(sprite);
    }

    /// <summary>Ajusta imagem+borda ao tamanho nativo do sprite (preservando o rácio).</summary>
    public void Ajustar(Sprite sprite)
    {
        if (Imagem == null || sprite == null) return;

        Vector2 nativo = sprite.rect.size; // dimensões reais do sprite em px
        if (nativo.x <= 0f || nativo.y <= 0f) return;

        // Escala para caber em TamanhoMaximo preservando o rácio.
        float escala = Mathf.Min(TamanhoMaximo.x / nativo.x, TamanhoMaximo.y / nativo.y);
        Vector2 tam = nativo * escala;

        Imagem.rectTransform.sizeDelta = tam;
        if (Container != null)
            Container.sizeDelta = tam + Vector2.one * (Borda * 2f);
    }
}
