using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HudVR — HUD head-locked do jogador em VR (filho rígido do CenterEyeAnchor):
///   • canto inferior ESQUERDO — animação de demonstração do exercício
///     (<see cref="ExercicioDemoLoop"/> reutilizado; sprites trocados quando o
///     minijogo/exercício muda);
///   • canto inferior DIREITO — contador "Nº x/reps" (reset por braço e por
///     minijogo, feito pelo <see cref="GestorMinijogo"/>).
///
/// Criado por código na primeira chamada a <see cref="ObterOuCriar"/> (o
/// CenterEyeAnchor só existe depois do GestorXR inicializar o VR). Os sprites da
/// demo são carregados de Resources/Exercises/&lt;Tipo&gt;/1..5 (o builder copia-os
/// para lá a partir de UIAssets/Exercises).
/// </summary>
public class HudVR : MonoBehaviour
{
    public ExercicioDemoLoop Demo;
    public TextMeshProUGUI TextoReps;

    private static HudVR _instancia;
    private Transform  _canvas;      // para pendurar painéis criados a pedido
    private GameObject _feedPainel;  // canto superior esquerdo — feed da câmara de apoio
    private RawImage   _feedImagem;

    /// <summary>Obtém o HUD, criando-o preso à cabeça se necessário. Null se o VR não estiver ativo.</summary>
    public static HudVR ObterOuCriar()
    {
        if (_instancia != null) return _instancia;

        var xr = GestorXR.Instancia;
        var cabeca = xr != null ? xr.Cabeca : null;
        if (cabeca == null) return null;

        _instancia = Construir(cabeca);
        return _instancia;
    }

    /// <summary>Troca a animação de demonstração para o exercício dado.</summary>
    public void DefinirExercicio(ExerciciosWaypoints.TipoExercicio tipo)
    {
        if (Demo == null) return;
        Demo.DefinirSprites(CarregarSprites(tipo));
    }

    /// <summary>Atualiza o contador "Nº x/reps" (chamar com o valor do braço/minijogo atual).</summary>
    public void DefinirReps(int atual, int total)
    {
        if (TextoReps != null) TextoReps.text = $"Nº {atual}/{total}";
    }

    public void Mostrar(bool ativo) => gameObject.SetActive(ativo);

    /// <summary>
    /// Liga o feed da câmara de apoio (vista lateral do exercício) ao canto
    /// superior ESQUERDO do HUD — cria o painel na primeira chamada.
    /// </summary>
    public void DefinirFeedApoio(Texture textura)
    {
        if (textura == null || _canvas == null) return;

        if (_feedPainel == null)
        {
            _feedPainel = new GameObject("FeedApoioFundo");
            _feedPainel.transform.SetParent(_canvas, false);
            var fundoRect = _feedPainel.AddComponent<RectTransform>();
            fundoRect.anchorMin = fundoRect.anchorMax = new Vector2(0f, 1f);
            fundoRect.pivot = new Vector2(0f, 1f);
            fundoRect.anchoredPosition = new Vector2(30f, -30f);
            fundoRect.sizeDelta = new Vector2(340f, 340f);
            var fundoImg = _feedPainel.AddComponent<Image>();
            fundoImg.color = new Color(0.09f, 0.12f, 0.10f, 0.9f); // igual ao painel da demo
            fundoImg.raycastTarget = false;

            var feedGO = new GameObject("FeedApoio");
            feedGO.transform.SetParent(_feedPainel.transform, false);
            var feedRect = feedGO.AddComponent<RectTransform>();
            feedRect.anchorMin = Vector2.zero; feedRect.anchorMax = Vector2.one;
            feedRect.offsetMin = new Vector2(8f, 8f);
            feedRect.offsetMax = new Vector2(-8f, -8f);
            _feedImagem = feedGO.AddComponent<RawImage>();
            _feedImagem.raycastTarget = false;
        }

        _feedImagem.texture = textura;
        _feedPainel.SetActive(true);
    }

    /// <summary>Mostra/esconde o painel do feed de apoio (se já foi criado).</summary>
    public void MostrarFeedApoio(bool ativo)
    {
        if (_feedPainel != null) _feedPainel.SetActive(ativo);
    }

    // ── Construção ────────────────────────────────────────────────────
    static HudVR Construir(Transform cabeca)
    {
        var root = new GameObject("HudVR");
        root.transform.SetParent(cabeca, false);
        root.transform.localPosition = new Vector3(0f, -0.15f, 1.1f); // ligeiramente abaixo do olhar

        var canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(root.transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta  = new Vector2(1400f, 700f);
        rt.localScale = Vector3.one * 0.001f; // 1400 px → 1.4 m a 1.1 m de distância

        var hud = root.AddComponent<HudVR>();
        hud._canvas = canvasGO.transform;

        // Demo do exercício — baixo-esquerda. As imagens do artista têm fundo
        // transparente + borda própria, por isso levam um FUNDO sólido atrás
        // para contraste na apresentação em VR.
        var fundoGO = new GameObject("DemoFundo");
        fundoGO.transform.SetParent(canvasGO.transform, false);
        var fundoRect = fundoGO.AddComponent<RectTransform>();
        fundoRect.anchorMin = fundoRect.anchorMax = new Vector2(0f, 0f);
        fundoRect.pivot = new Vector2(0f, 0f);
        fundoRect.anchoredPosition = new Vector2(30f, 30f);
        fundoRect.sizeDelta = new Vector2(340f, 334f); // imagem ~quadrada (584×573) + margem
        var fundoImg = fundoGO.AddComponent<Image>();
        fundoImg.color = new Color(0.09f, 0.12f, 0.10f, 0.9f); // tom escuro dos fundos do jogo
        fundoImg.raycastTarget = false;

        var demoGO = new GameObject("DemoExercicio");
        demoGO.transform.SetParent(fundoGO.transform, false);
        var demoRect = demoGO.AddComponent<RectTransform>();
        demoRect.anchorMin = Vector2.zero; demoRect.anchorMax = Vector2.one;
        // Margens NEGATIVAS: a imagem (PNG transparente com borda própria) fica
        // maior que o fundo cinzento, que se mantém do mesmo tamanho.
        demoRect.offsetMin = new Vector2(-18f, -18f);
        demoRect.offsetMax = new Vector2(18f, 18f);
        var demoImg = demoGO.AddComponent<Image>();
        demoImg.color = Color.white;
        demoImg.preserveAspect = true;
        demoImg.raycastTarget = false;
        hud.Demo = demoGO.AddComponent<ExercicioDemoLoop>();
        hud.Demo.ImagemAlvo = demoImg;

        // Contador de reps — baixo-direita.
        var repsGO = new GameObject("TextoReps");
        repsGO.transform.SetParent(canvasGO.transform, false);
        var repsRect = repsGO.AddComponent<RectTransform>();
        repsRect.anchorMin = repsRect.anchorMax = new Vector2(1f, 0f);
        repsRect.pivot = new Vector2(1f, 0f);
        repsRect.anchoredPosition = new Vector2(-30f, 30f);
        repsRect.sizeDelta = new Vector2(400f, 120f);
        var reps = repsGO.AddComponent<TextMeshProUGUI>();
        reps.fontSize  = 72f;
        reps.alignment = TextAlignmentOptions.BottomRight;
        reps.color     = Color.white;
        reps.raycastTarget = false;
        hud.TextoReps = reps;

        return hud;
    }

    /// <summary>
    /// Sprites 1..5 do exercício em Resources/Exercises/&lt;Tipo&gt; (ordenados
    /// numericamente). A pasta também tem o ÍCONE do minijogo (nome não numérico,
    /// ex.: "preven game_dardos") — esse fica de fora da demo.
    /// </summary>
    static Sprite[] CarregarSprites(ExerciciosWaypoints.TipoExercicio tipo)
    {
        var todos  = Resources.LoadAll<Sprite>($"Exercises/{tipo}");
        var frames = new System.Collections.Generic.List<Sprite>();
        if (todos != null)
            foreach (var s in todos)
                if (int.TryParse(s.name, out _)) frames.Add(s);

        if (frames.Count == 0)
        {
            Debug.LogWarning($"[HudVR] Sem sprites em Resources/Exercises/{tipo} — corre o Build Cena (Menu) para os copiar.");
            return new Sprite[0];
        }
        frames.Sort((a, b) => int.Parse(a.name).CompareTo(int.Parse(b.name)));
        return frames.ToArray();
    }
}
