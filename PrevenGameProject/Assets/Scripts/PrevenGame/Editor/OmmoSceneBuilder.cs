using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// OmmoSceneBuilder - Editor script que constrói cenas Ommo automaticamente.
///
/// Ommo → Build Scene (Diagnóstico) — cena completa com hardware panel e grelha 3D.
/// Ommo → Build Scene (Jogo)        — cena mínima para o jogo: sem UI de diagnóstico.
/// </summary>
public class OmmoSceneBuilder : EditorWindow
{
    // ── Cena do Jogo (sem UI de diagnóstico) ─────────────────────────────

    /// <summary>
    /// Constrói o conteúdo da Clinical Trial na cena ATUAL (sem diálogo de confirmação).
    /// Reutilizado pelo orquestrador BuildTresCenas() para gravar a cena ClinicalTrial.unity.
    /// </summary>
    public static void BuildJogoCore()
    {
        Debug.Log("[OmmoBuilder] A construir mundo 3D do jogo...");
        ClearExistingJogo();

        // ── EventSystem ───────────────────────────────────────────────
        // Obrigatório para qualquer input de UI (botões, sliders, etc.)
        // Sem este componente os botões renderizam mas nunca disparam onClick.
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = CreateEmpty("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ── Materiais ─────────────────────────────────────────────────
        // (O material do objeto controlado é tratado por CriarVisualSensor.)

        // Piso — grid subtil
        Material pisoMat = new Material(Shader.Find("Standard"));
        pisoMat.color = new Color(0.22f, 0.22f, 0.25f);
        pisoMat.SetFloat("_Metallic", 0f);
        pisoMat.SetFloat("_Glossiness", 0.1f);
        pisoMat.name = "PisoMaterial";

        // ── OmmoBootstrap (camada de ligação persistente) ─────────────
        // Cria/possui o dispatcher + launcher e sobrevive a trocas de cena
        // (DontDestroyOnLoad). Singleton: se entrarmos por outra cena que já o
        // criou, este auto-destrói-se. Mantém o serviço .exe vivo entre cenas.
        var bootstrapGO = CreateEmpty("OmmoBootstrap");
        bootstrapGO.AddComponent<OmmoBootstrap>();

        // ── AppManager (por cena) ─────────────────────────────────────
        // Hardware/tracking re-inicializa-se sozinho via OmmoServiceLauncher.ServiceReady
        // (o launcher persistente já está pronto) e recupera os sensores ligados.
        GameObject appManager = CreateEmpty("AppManager");
        var monitor    = appManager.AddComponent<OmmoHardwareMonitor>();
        var devManager = appManager.AddComponent<OmmoDeviceManager>();
        var sensorMgr  = appManager.AddComponent<OmmoSensorManager>();

        // ── BaseStation ───────────────────────────────────────────────
        // Empty object na origem — representa a Base Station física Ommo.
        // Todas as posições dos sensores são relativas a este ponto.
        GameObject baseStation = CreateEmpty("BaseStation");
        // Y=13 = 130 cm — altura aproximada do peito; sensores ficam abaixo deste ponto
        baseStation.transform.position = new Vector3(0f, 13f, 0f);

        // Marcador visual da BaseStation (esfera pequena, não afecta o jogo)
        var bsMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bsMarker.name = "BaseStation_Marcador";
        bsMarker.transform.SetParent(baseStation.transform);
        bsMarker.transform.localPosition = Vector3.zero;
        bsMarker.transform.localScale    = Vector3.one * 0.08f;
        Object.DestroyImmediate(bsMarker.GetComponent<SphereCollider>());
        var bsMat = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.2f, 0.6f, 1f) };
        bsMarker.GetComponent<MeshRenderer>().sharedMaterial = bsMat;

        // ── Piso ──────────────────────────────────────────────────────
        // Plano de referência visual — YUnity = 0 corresponde ao plano da Base Station
        var piso = GameObject.CreatePrimitive(PrimitiveType.Plane);
        piso.name = "Piso";
        piso.transform.position   = Vector3.zero;
        piso.transform.localScale = new Vector3(4f, 1f, 4f); // 40×40 Unity units
        Object.DestroyImmediate(piso.GetComponent<MeshCollider>());
        piso.GetComponent<MeshRenderer>().sharedMaterial = pisoMat;

        // ── Objeto controlado pelo Ommo (SensorPrefab) ────────────────
        // Prefab real (Assets/PrevenGameAssets/ObjetoControlado.prefab) se existir,
        // senão cubo branco placeholder com contorno RimGlow.
        // OmmoDevice instancia um por sensor e move-o em Update() com os dados Ommo.
        var cubo = CriarVisualSensor();

        // ── TrackedDevice Prefab ──────────────────────────────────────
        // Root inativo — OmmoDeviceManager instancia um por dispositivo conectado.
        // O CuboSensor é criado como filho de cada instância pelo OmmoDevice.
        var trackedRoot = CreateEmpty("TrackedDevicePrefab_TEMP");
        var ommoDevice  = trackedRoot.AddComponent<OmmoDevice>();
        ommoDevice.SensorPrefab  = cubo;
        // FullFusion combina IMU + magnetómetro + optical tracking → posições 3D reais
        // Default pode reportar apenas orientação sem posição
        ommoDevice.RequestedMode = Ommo.DeviceFusionMode.FullFusion;
        cubo.transform.SetParent(trackedRoot.transform, false);
        cubo.transform.localPosition = Vector3.zero;
        trackedRoot.SetActive(false);

        // ── OmmoDeviceManager ─────────────────────────────────────────
        devManager.BaseStation    = baseStation;
        devManager.UnityScaleInCM = 10f; // 1 Unity unit = 10 cm
        devManager.DeviceTypePrefabs = new OmmoDeviceManager.DeviceTypePrefab[]
        {
            // DeviceType 0 cobre o tipo genérico; o manager tem fallback para 255 e primeiro disponível
            new OmmoDeviceManager.DeviceTypePrefab { DeviceType = 0, Prefab = trackedRoot }
        };

        // ── Painel de espera "A ligar ao sensor Ommo..." ──────────────
        // Canvas overlay por cima de tudo (sortingOrder 99).
        // Visível ao arrancar; OmmoSensorManager.OcultarPainelALigar() desativa-o
        // automaticamente quando OnServiceReady disparar (~2.5s de warmup).
        var painelGO = new GameObject("PainelALigar_TEMP");
        Undo.RegisterCreatedObjectUndo(painelGO, "Create PainelALigar_TEMP");
        var painelCanvas = painelGO.AddComponent<Canvas>();
        painelCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        painelCanvas.sortingOrder = 99; // fica à frente de tudo
        painelGO.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        painelGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);

        // Fundo escuro opaco — cobre o mundo 3D enquanto o serviço não está pronto
        var fundoGO  = new GameObject("Fundo");
        fundoGO.transform.SetParent(painelGO.transform, false);
        var fundoRect = fundoGO.AddComponent<RectTransform>();
        fundoRect.anchorMin = Vector2.zero;
        fundoRect.anchorMax = Vector2.one;
        fundoRect.offsetMin = Vector2.zero;
        fundoRect.offsetMax = Vector2.zero;
        var fundoImg = fundoGO.AddComponent<Image>();
        fundoImg.color = new Color(0.06f, 0.06f, 0.08f, 1f);

        // Texto centrado
        var textoGO = new GameObject("TextoEstado");
        textoGO.transform.SetParent(painelGO.transform, false);
        var textoRect = textoGO.AddComponent<RectTransform>();
        textoRect.anchorMin        = new Vector2(0.5f, 0.5f);
        textoRect.anchorMax        = new Vector2(0.5f, 0.5f);
        textoRect.pivot            = new Vector2(0.5f, 0.5f);
        textoRect.anchoredPosition = Vector2.zero;
        textoRect.sizeDelta        = new Vector2(800f, 80f);
        var textoTMP = textoGO.AddComponent<TextMeshProUGUI>();
        textoTMP.text      = "A ligar ao sensor Ommo...";
        textoTMP.fontSize  = 28;
        textoTMP.color     = Color.white;
        textoTMP.alignment = TextAlignmentOptions.Center;

        // ── OmmoSensorManager ─────────────────────────────────────────
        sensorMgr.DeviceManager   = devManager;
        sensorMgr.HardwareMonitor = monitor;
        sensorMgr.PainelALigar    = painelCanvas;   // ← canvas de espera
        sensorMgr.TextoEstado     = textoTMP;        // ← texto de estado

        // ── EsqueletoJogador ──────────────────────────────────────────
        var esqueletoGO = CreateEmpty("EsqueletoJogador");
        var esqueleto   = esqueletoGO.AddComponent<OmmoEsqueletoJogador>();

        // ── CalibracaoCanvas ──────────────────────────────────────────
        // sortingOrder 50 — abaixo do PainelALigar (99), acima do mundo 3D
        var calibGO     = new GameObject("CalibracaoCanvas");
        Undo.RegisterCreatedObjectUndo(calibGO, "Create CalibracaoCanvas");
        var calibCanvas = calibGO.AddComponent<Canvas>();
        calibCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        calibCanvas.sortingOrder = 50;
        var calibScaler = calibGO.AddComponent<CanvasScaler>();
        calibScaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        calibScaler.referenceResolution = new Vector2(1920, 1080);
        calibGO.AddComponent<GraphicRaycaster>();

        // Painel semi-transparente centrado (750×380)
        var painelCalib = new GameObject("PainelCalibracao");
        painelCalib.transform.SetParent(calibGO.transform, false);
        var painelCalibImg  = painelCalib.AddComponent<Image>();
        painelCalibImg.color = new Color(0f, 0f, 0f, 0.55f);
        var painelRect = painelCalib.GetComponent<RectTransform>();
        painelRect.anchorMin        = new Vector2(0.5f, 0.5f);
        painelRect.anchorMax        = new Vector2(0.5f, 0.5f);
        painelRect.pivot            = new Vector2(0.5f, 0.5f);
        painelRect.anchoredPosition = Vector2.zero;
        painelRect.sizeDelta        = new Vector2(750f, 380f);

        // TextoPasso — "Passo 1 / 3" (canto superior, fontSize 18)
        var textoPassoGO = new GameObject("TextoPasso");
        textoPassoGO.transform.SetParent(painelCalib.transform, false);
        var textoPassoRect = textoPassoGO.AddComponent<RectTransform>();
        textoPassoRect.anchorMin        = new Vector2(0f, 1f);
        textoPassoRect.anchorMax        = new Vector2(1f, 1f);
        textoPassoRect.pivot            = new Vector2(0.5f, 1f);
        textoPassoRect.anchoredPosition = new Vector2(0f, -14f);
        textoPassoRect.sizeDelta        = new Vector2(0f, 28f);
        var textoPasso = textoPassoGO.AddComponent<TextMeshProUGUI>();
        textoPasso.text      = "";
        textoPasso.fontSize  = 18;
        textoPasso.color     = new Color(0.8f, 0.8f, 0.8f);
        textoPasso.alignment = TextAlignmentOptions.Center;

        // TextoInstrucao — instrução principal (fontSize 28, centrada verticalmente)
        var textoInstrGO = new GameObject("TextoInstrucao");
        textoInstrGO.transform.SetParent(painelCalib.transform, false);
        var textoInstrRect = textoInstrGO.AddComponent<RectTransform>();
        textoInstrRect.anchorMin        = new Vector2(0.05f, 0.5f);
        textoInstrRect.anchorMax        = new Vector2(0.95f, 0.5f);
        textoInstrRect.pivot            = new Vector2(0.5f, 0.5f);
        textoInstrRect.anchoredPosition = new Vector2(0f, 30f);
        textoInstrRect.sizeDelta        = new Vector2(0f, 90f);
        var textoInstr = textoInstrGO.AddComponent<TextMeshProUGUI>();
        textoInstr.text      = "A aguardar 2 sensores...";
        textoInstr.fontSize  = 28;
        textoInstr.color     = Color.white;
        textoInstr.alignment = TextAlignmentOptions.Center;

        // TextoSub — subtexto (fontSize 18, abaixo da instrução)
        var textoSubGO = new GameObject("TextoSub");
        textoSubGO.transform.SetParent(painelCalib.transform, false);
        var textoSubRect = textoSubGO.AddComponent<RectTransform>();
        textoSubRect.anchorMin        = new Vector2(0.05f, 0.5f);
        textoSubRect.anchorMax        = new Vector2(0.95f, 0.5f);
        textoSubRect.pivot            = new Vector2(0.5f, 0.5f);
        textoSubRect.anchoredPosition = new Vector2(0f, -28f);
        textoSubRect.sizeDelta        = new Vector2(0f, 50f);
        var textoSub = textoSubGO.AddComponent<TextMeshProUGUI>();
        textoSub.text      = "Liga o sensor da palma e o sensor do ombro.";
        textoSub.fontSize  = 18;
        textoSub.color     = new Color(0.75f, 0.75f, 0.75f);
        textoSub.alignment = TextAlignmentOptions.Center;

        // Texto inicial (sobrescrito pelo AtualizarUI() do manager em runtime)
        textoInstr.text = "A aguardar sensor...";
        textoSub.text   = "Liga o sensor da palma.";

        // BarraProgresso — fill horizontal (verde, 500×20 px)
        var barraGO = new GameObject("BarraProgresso");
        barraGO.transform.SetParent(painelCalib.transform, false);
        var barraRect = barraGO.AddComponent<RectTransform>();
        barraRect.anchorMin        = new Vector2(0.5f, 0f);
        barraRect.anchorMax        = new Vector2(0.5f, 0f);
        barraRect.pivot            = new Vector2(0.5f, 0f);
        barraRect.anchoredPosition = new Vector2(0f, 28f);
        barraRect.sizeDelta        = new Vector2(500f, 20f);

        // Fundo da barra (cinza escuro)
        var barraBgGO = new GameObject("BarraFundo");
        barraBgGO.transform.SetParent(barraGO.transform, false);
        var barraBgImg = barraBgGO.AddComponent<Image>();
        barraBgImg.color = new Color(0.2f, 0.2f, 0.2f);
        StretchFull(barraBgGO.GetComponent<RectTransform>());

        // Fill verde (Image type = Filled, fillMethod = Horizontal)
        var barraFillGO = new GameObject("BarraFill");
        barraFillGO.transform.SetParent(barraGO.transform, false);
        var barraFillImg = barraFillGO.AddComponent<Image>();
        barraFillImg.color      = new Color(0.2f, 0.85f, 0.3f);
        barraFillImg.type       = Image.Type.Filled;
        barraFillImg.fillMethod = Image.FillMethod.Horizontal;
        barraFillImg.fillAmount = 0f;
        StretchFull(barraFillGO.GetComponent<RectTransform>());

        // ── OmmoCalibracaoManager ─────────────────────────────────────
        var calibManager = appManager.AddComponent<OmmoCalibracaoManager>();
        calibManager.SensorManager        = sensorMgr;
        calibManager.Esqueleto            = esqueleto;
        calibManager.PainelCalibracao     = painelCalib;
        calibManager.TextoInstrucao       = textoInstr;
        calibManager.TextoPasso           = textoPasso;
        calibManager.TextoSub             = textoSub;
        calibManager.BarraProgressoImagem = barraFillImg;

        // ── PrevenGameCanvas ──────────────────────────────────────────
        // sortingOrder 40 — abaixo da calibração (50) e do painel de ligação (99)
        var gameCanvasGO = new GameObject("PrevenGameCanvas");
        Undo.RegisterCreatedObjectUndo(gameCanvasGO, "Create PrevenGameCanvas");
        var gameCanvas = gameCanvasGO.AddComponent<Canvas>();
        gameCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        gameCanvas.sortingOrder = 40;
        var gameScaler = gameCanvasGO.AddComponent<CanvasScaler>();
        gameScaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        gameScaler.referenceResolution = new Vector2(1920, 1080);
        gameCanvasGO.AddComponent<GraphicRaycaster>();

        // ── HUDJogo (ativo durante o exercício) ───────────────────────
        var hudJogo = new GameObject("HUDJogo");
        hudJogo.transform.SetParent(gameCanvasGO.transform, false);
        StretchFull(hudJogo.AddComponent<RectTransform>()); // cobre o ecrã: filhos ancoram aos cantos reais

        // TextoPercentagem — canto superior esquerdo (% média viva)
        var textoPctGO = new GameObject("TextoPercentagem");
        textoPctGO.transform.SetParent(hudJogo.transform, false);
        var textoPctRect = textoPctGO.AddComponent<RectTransform>();
        textoPctRect.anchorMin        = new Vector2(0f, 1f);
        textoPctRect.anchorMax        = new Vector2(0f, 1f);
        textoPctRect.pivot            = new Vector2(0f, 1f);
        textoPctRect.anchoredPosition = new Vector2(20f, -20f);
        textoPctRect.sizeDelta        = new Vector2(220f, 56f);
        var textoPct = textoPctGO.AddComponent<TextMeshProUGUI>();
        textoPct.text      = "0 %";
        textoPct.fontSize  = 40;
        textoPct.color     = Color.white;
        textoPct.fontStyle = FontStyles.Bold;

        // TextoRepeticao — canto inferior direito (reps x/X)
        var textoRepGO = new GameObject("TextoRepeticao");
        textoRepGO.transform.SetParent(hudJogo.transform, false);
        var textoRepRect = textoRepGO.AddComponent<RectTransform>();
        textoRepRect.anchorMin        = new Vector2(1f, 0f);
        textoRepRect.anchorMax        = new Vector2(1f, 0f);
        textoRepRect.pivot            = new Vector2(1f, 0f);
        textoRepRect.anchoredPosition = new Vector2(-40f, 40f);
        textoRepRect.sizeDelta        = new Vector2(220f, 56f);
        var textoRep = textoRepGO.AddComponent<TextMeshProUGUI>();
        textoRep.text      = "1/3";
        textoRep.fontSize  = 40;
        textoRep.color     = Color.white;
        textoRep.fontStyle = FontStyles.Bold;
        textoRep.alignment = TextAlignmentOptions.Right;

        // TextoTempo — centro superior
        var textoTempoGO = new GameObject("TextoTempo");
        textoTempoGO.transform.SetParent(hudJogo.transform, false);
        var textoTempoRect = textoTempoGO.AddComponent<RectTransform>();
        textoTempoRect.anchorMin        = new Vector2(0.5f, 1f);
        textoTempoRect.anchorMax        = new Vector2(0.5f, 1f);
        textoTempoRect.pivot            = new Vector2(0.5f, 1f);
        textoTempoRect.anchoredPosition = new Vector2(0f, -18f);
        textoTempoRect.sizeDelta        = new Vector2(200f, 50f);
        var textoTempo = textoTempoGO.AddComponent<TextMeshProUGUI>();
        textoTempo.text      = "00s";
        textoTempo.fontSize  = 34;
        textoTempo.color     = Color.white;
        textoTempo.fontStyle = FontStyles.Bold;
        textoTempo.alignment = TextAlignmentOptions.Center;

        // TextoCompensacao — canto superior direito
        var textoCompGO = new GameObject("TextoCompensacao");
        textoCompGO.transform.SetParent(hudJogo.transform, false);
        var textoCompRect = textoCompGO.AddComponent<RectTransform>();
        textoCompRect.anchorMin        = new Vector2(1f, 1f);
        textoCompRect.anchorMax        = new Vector2(1f, 1f);
        textoCompRect.pivot            = new Vector2(1f, 1f);
        textoCompRect.anchoredPosition = new Vector2(-20f, -20f);
        textoCompRect.sizeDelta        = new Vector2(280f, 40f);
        var textoComp = textoCompGO.AddComponent<TextMeshProUGUI>();
        textoComp.text      = "Compensações: 0";
        textoComp.fontSize  = 20;
        textoComp.color     = new Color(0.9f, 0.9f, 0.9f);
        textoComp.alignment = TextAlignmentOptions.Right;

        // ── Demo do exercício (canto inferior esquerdo) ───────────────
        var demoLoop = ConstruirDemoExercicio(hudJogo.transform);
        var spritesExercicios = new Sprite[][]
        {
            CarregarSpritesExercicio(1),
            CarregarSpritesExercicio(2),
            CarregarSpritesExercicio(3),
            CarregarSpritesExercicio(4),
        };
        if (spritesExercicios[0].Length > 0)
            demoLoop.Sprites = spritesExercicios[0];

        hudJogo.SetActive(false); // escondido até calibração terminar

        // ── PainelFim ─────────────────────────────────────────────────
        // Fundo preto full-screen (é o que o manager liga/desliga).
        var backdropFim = new GameObject("PainelFim");
        backdropFim.transform.SetParent(gameCanvasGO.transform, false);
        StretchFull(backdropFim.AddComponent<RectTransform>());
        backdropFim.AddComponent<Image>().color = new Color(0f, 0f, 0f, 1f);

        // Conteúdo centrado.
        var painelFimGO = new GameObject("Conteudo");
        painelFimGO.transform.SetParent(backdropFim.transform, false);
        var painelFimRect = painelFimGO.AddComponent<RectTransform>();
        painelFimRect.anchorMin        = new Vector2(0.5f, 0.5f);
        painelFimRect.anchorMax        = new Vector2(0.5f, 0.5f);
        painelFimRect.pivot            = new Vector2(0.5f, 0.5f);
        painelFimRect.anchoredPosition = Vector2.zero;
        painelFimRect.sizeDelta        = new Vector2(700f, 260f);

        // TextoResultado
        var textoResultGO = new GameObject("TextoResultado");
        textoResultGO.transform.SetParent(painelFimGO.transform, false);
        var textoResultRect = textoResultGO.AddComponent<RectTransform>();
        textoResultRect.anchorMin        = new Vector2(0.05f, 1f);
        textoResultRect.anchorMax        = new Vector2(0.95f, 1f);
        textoResultRect.pivot            = new Vector2(0.5f, 1f);
        textoResultRect.anchoredPosition = new Vector2(0f, -24f);
        textoResultRect.sizeDelta        = new Vector2(0f, 60f);
        var textoResult = textoResultGO.AddComponent<TextMeshProUGUI>();
        textoResult.text      = "✅ Exercício concluído!";
        textoResult.fontSize  = 30;
        textoResult.color     = Color.white;
        textoResult.fontStyle = FontStyles.Bold;
        textoResult.alignment = TextAlignmentOptions.Center;

        // TextoEstatisticas
        var textoEstatGO = new GameObject("TextoEstatisticas");
        textoEstatGO.transform.SetParent(painelFimGO.transform, false);
        var textoEstatRect = textoEstatGO.AddComponent<RectTransform>();
        textoEstatRect.anchorMin        = new Vector2(0.05f, 0.5f);
        textoEstatRect.anchorMax        = new Vector2(0.95f, 0.5f);
        textoEstatRect.pivot            = new Vector2(0.5f, 0.5f);
        textoEstatRect.anchoredPosition = new Vector2(0f, 10f);
        textoEstatRect.sizeDelta        = new Vector2(0f, 40f);
        var textoEstat = textoEstatGO.AddComponent<TextMeshProUGUI>();
        textoEstat.text      = "Tempo: --   |   Repetições: --   |   Compensações: --";
        textoEstat.fontSize  = 20;
        textoEstat.color     = new Color(0.85f, 0.85f, 0.85f);
        textoEstat.alignment = TextAlignmentOptions.Center;

        // BotaoRepetir
        var botaoRepGO = new GameObject("BotaoRepetir");
        botaoRepGO.transform.SetParent(painelFimGO.transform, false);
        var botaoRepRect = botaoRepGO.AddComponent<RectTransform>();
        botaoRepRect.anchorMin        = new Vector2(0.5f, 0f);
        botaoRepRect.anchorMax        = new Vector2(0.5f, 0f);
        botaoRepRect.pivot            = new Vector2(0.5f, 0f);
        botaoRepRect.anchoredPosition = new Vector2(0f, 28f);
        botaoRepRect.sizeDelta        = new Vector2(200f, 48f);
        var botaoRepImg = botaoRepGO.AddComponent<Image>();
        botaoRepImg.color = new Color(0.2f, 0.7f, 0.3f);
        var botaoRepBtn = botaoRepGO.AddComponent<Button>();
        var botaoRepColors = botaoRepBtn.colors;
        botaoRepColors.highlightedColor = new Color(0.25f, 0.85f, 0.35f);
        botaoRepColors.pressedColor     = new Color(0.15f, 0.55f, 0.2f);
        botaoRepBtn.colors = botaoRepColors;

        var botaoRepLabelGO = new GameObject("Label");
        botaoRepLabelGO.transform.SetParent(botaoRepGO.transform, false);
        var botaoRepLabelRect = botaoRepLabelGO.AddComponent<RectTransform>();
        botaoRepLabelRect.anchorMin = Vector2.zero;
        botaoRepLabelRect.anchorMax = Vector2.one;
        botaoRepLabelRect.offsetMin = Vector2.zero;
        botaoRepLabelRect.offsetMax = Vector2.zero;
        var botaoRepLabel = botaoRepLabelGO.AddComponent<TextMeshProUGUI>();
        botaoRepLabel.text      = "Nova Sessão";
        botaoRepLabel.fontSize  = 22;
        botaoRepLabel.color     = Color.white;
        botaoRepLabel.fontStyle = FontStyles.Bold;
        botaoRepLabel.alignment = TextAlignmentOptions.Center;

        backdropFim.SetActive(false);

        // ── PainelSelecaoExercicio ────────────────────────────────────
        // Aparece após calibração; permite escolher exercícios e reps.
        // Fundo preto full-screen (tapa o mundo 3D, como no MainMenu) — é o que o manager liga/desliga.
        var backdropSel = new GameObject("PainelSelecaoExercicio");
        Undo.RegisterCreatedObjectUndo(backdropSel, "Create PainelSelecao");
        backdropSel.transform.SetParent(gameCanvasGO.transform, false);
        StretchFull(backdropSel.AddComponent<RectTransform>());
        backdropSel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 1f); // preto opaco + bloqueia cliques

        // Conteúdo centrado (mantém o layout existente).
        var painelSelGO = new GameObject("Conteudo");
        painelSelGO.transform.SetParent(backdropSel.transform, false);
        var painelSelRect = painelSelGO.AddComponent<RectTransform>();
        painelSelRect.anchorMin        = new Vector2(0.5f, 0.5f);
        painelSelRect.anchorMax        = new Vector2(0.5f, 0.5f);
        painelSelRect.pivot            = new Vector2(0.5f, 0.5f);
        painelSelRect.anchoredPosition = Vector2.zero;
        painelSelRect.sizeDelta        = new Vector2(1560f, 620f);

        // Botão Voltar ao menu (canto superior esquerdo do conteúdo).
        var btnVoltarSel = CriarBotaoSimples("BotaoVoltarMenu", painelSelGO.transform, "◀ Menu",
            new Color(0.3f, 0.3f, 0.32f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -16f), new Vector2(140f, 44f)).GetComponent<Button>();

        // Título
        var tituloSelGO = new GameObject("TituloSelecao");
        tituloSelGO.transform.SetParent(painelSelGO.transform, false);
        var tituloSelRect = tituloSelGO.AddComponent<RectTransform>();
        tituloSelRect.anchorMin        = new Vector2(0.5f, 1f);
        tituloSelRect.anchorMax        = new Vector2(0.5f, 1f);
        tituloSelRect.pivot            = new Vector2(0.5f, 1f);
        tituloSelRect.anchoredPosition = new Vector2(0f, -28f);
        tituloSelRect.sizeDelta        = new Vector2(760f, 50f);
        var tituloSelTMP = tituloSelGO.AddComponent<TextMeshProUGUI>();
        tituloSelTMP.text      = "Escolhe os Exercícios";
        tituloSelTMP.fontSize  = 30;
        tituloSelTMP.color     = Color.white;
        tituloSelTMP.fontStyle = FontStyles.Bold;
        tituloSelTMP.alignment = TextAlignmentOptions.Center;

        // ── Seleção de braço (Direito / Esquerdo) ────────────────────
        var selBracoGO   = new GameObject("SelecaoBraco");
        selBracoGO.transform.SetParent(painelSelGO.transform, false);
        var selBracoRect = selBracoGO.AddComponent<RectTransform>();
        selBracoRect.anchorMin        = new Vector2(0.5f, 1f);
        selBracoRect.anchorMax        = new Vector2(0.5f, 1f);
        selBracoRect.pivot            = new Vector2(0.5f, 1f);
        selBracoRect.anchoredPosition = new Vector2(0f, -84f);
        selBracoRect.sizeDelta        = new Vector2(540f, 46f);

        var btnBracoDirBtn = CriarBotaoSimples("BotaoBracoDireito", selBracoGO.transform,
            "Braço Direito", new Color(0.2f, 0.7f, 0.3f),
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(250f, 0f)).GetComponent<Button>();

        var btnBracoEsqBtn = CriarBotaoSimples("BotaoBracoEsquerdo", selBracoGO.transform,
            "Braço Esquerdo", new Color(0.35f, 0.35f, 0.35f),
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(0f, 0f), new Vector2(250f, 0f)).GetComponent<Button>();

        // Nomes e disponibilidade dos 4 exercícios
        string[] nomesExercicios = { "Flexão do Braço", "Elevação Total", "Abdução Lateral", "Flexão do Cotovelo" };
        bool[]   disponiveis     = { true, true, true, true };
        // Posições X dos 4 cards (centradas no painel de 1560 px, cards de 360 px, gap de 20 px)
        float[]  cardX           = { -570f, -190f, 190f, 570f };

        var botoesToggle   = new Button[4];
        var textoRepsArr   = new TextMeshProUGUI[4];
        var botoesMenosArr = new Button[4];
        var botoesMaisArr  = new Button[4];
        var imagensExArr   = new Image[4];

        for (int i = 0; i < 4; i++)
        {
            bool   disp   = disponiveis[i];
            string nomeEx = nomesExercicios[i];
            Color  corCard = disp ? new Color(0.18f, 0.55f, 0.25f) : new Color(0.25f, 0.25f, 0.25f);

            // ── Raiz do card (= BotaoToggle_i) — clique → toggle ──────
            var cardGO   = new GameObject($"BotaoToggle_{i}");
            cardGO.transform.SetParent(painelSelGO.transform, false);
            var cardRect = cardGO.AddComponent<RectTransform>();
            cardRect.anchorMin        = new Vector2(0.5f, 1f);
            cardRect.anchorMax        = new Vector2(0.5f, 1f);
            cardRect.pivot            = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = new Vector2(cardX[i], -152f);
            cardRect.sizeDelta        = new Vector2(360f, 380f);
            var cardImg = cardGO.AddComponent<Image>();
            cardImg.color = corCard;
            var cardBtn = cardGO.AddComponent<Button>();
            cardBtn.interactable = disp;
            var cardColors = cardBtn.colors;
            cardColors.highlightedColor = disp ? new Color(0.22f, 0.68f, 0.30f) : new Color(0.25f, 0.25f, 0.25f);
            cardColors.pressedColor     = disp ? new Color(0.12f, 0.40f, 0.18f) : new Color(0.25f, 0.25f, 0.25f);
            cardColors.disabledColor    = new Color(0.22f, 0.22f, 0.22f, 0.7f);
            cardBtn.colors  = cardColors;
            botoesToggle[i] = cardBtn;

            // ── Nome do exercício ─────────────────────────────────────
            var nomeGO   = new GameObject("NomeExercicio");
            nomeGO.transform.SetParent(cardGO.transform, false);
            var nomeRect = nomeGO.AddComponent<RectTransform>();
            nomeRect.anchorMin        = new Vector2(0f, 1f);
            nomeRect.anchorMax        = new Vector2(1f, 1f);
            nomeRect.pivot            = new Vector2(0.5f, 1f);
            nomeRect.anchoredPosition = new Vector2(0f, -8f);
            nomeRect.sizeDelta        = new Vector2(-32f, 40f);
            var nomeTMP = nomeGO.AddComponent<TextMeshProUGUI>();
            nomeTMP.text           = nomeEx;
            nomeTMP.fontSize       = 18;
            nomeTMP.color          = Color.white;
            nomeTMP.fontStyle      = FontStyles.Bold;
            nomeTMP.alignment      = TextAlignmentOptions.Center;
            nomeTMP.raycastTarget  = false;

            // ── Imagem do exercício ───────────────────────────────────
            var imgGO   = new GameObject("ImagemExercicio");
            imgGO.transform.SetParent(cardGO.transform, false);
            var imgRect = imgGO.AddComponent<RectTransform>();
            imgRect.anchorMin        = new Vector2(0.5f, 1f);
            imgRect.anchorMax        = new Vector2(0.5f, 1f);
            imgRect.pivot            = new Vector2(0.5f, 1f);
            imgRect.anchoredPosition = new Vector2(0f, -56f);
            imgRect.sizeDelta        = new Vector2(328f, 252f);
            var imgComp = imgGO.AddComponent<Image>();
            imgComp.raycastTarget = false;
            string spritePath = $"Assets/Execercises/ex{i + 1}/1.jpg";
            // Garantir que o PNG está importado como Sprite antes de o carregar
            var texImporter = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (texImporter != null && texImporter.textureType != TextureImporterType.Sprite)
            {
                texImporter.textureType    = TextureImporterType.Sprite;
                texImporter.spriteImportMode = SpriteImportMode.Single;
                AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite != null)
            {
                imgComp.sprite         = sprite;
                imgComp.preserveAspect = true;
                imgComp.color          = Color.white;
            }
            else
            {
                imgComp.color = new Color(0.10f, 0.10f, 0.12f);
            }
            imagensExArr[i] = imgComp; // guarda referência para espelhar com seleção de braço

            // ── Animação hover — carrega todos os sprites do exercício ──
            var spritesEx = new System.Collections.Generic.List<Sprite>();
            for (int s = 1; s <= 5; s++)
            {
                string sPath = $"Assets/Execercises/ex{i + 1}/{s}.jpg";
                var sImporter = AssetImporter.GetAtPath(sPath) as TextureImporter;
                if (sImporter != null && sImporter.textureType != TextureImporterType.Sprite)
                {
                    sImporter.textureType    = TextureImporterType.Sprite;
                    sImporter.spriteImportMode = SpriteImportMode.Single;
                    AssetDatabase.ImportAsset(sPath, ImportAssetOptions.ForceUpdate);
                }
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(sPath);
                if (sp != null) spritesEx.Add(sp);
            }
            var animHover = cardGO.AddComponent<CardAnimacaoHover>();
            animHover.ImagemAlvo         = imgComp;
            animHover.Sprites            = spritesEx.ToArray();
            animHover.IntervaloSegundos  = 0.35f;

            // ── Barra +/nº/- ──────────────────────────────────────────
            var barraRepsGO   = new GameObject("BarraReps");
            barraRepsGO.transform.SetParent(cardGO.transform, false);
            var barraRepsRect = barraRepsGO.AddComponent<RectTransform>();
            barraRepsRect.anchorMin        = new Vector2(0f, 0f);
            barraRepsRect.anchorMax        = new Vector2(1f, 0f);
            barraRepsRect.pivot            = new Vector2(0.5f, 0f);
            barraRepsRect.anchoredPosition = new Vector2(0f, 8f);
            barraRepsRect.sizeDelta        = new Vector2(-32f, 52f);

            // Botão Menos
            var menosGO  = CriarBotaoSimples($"BotaoMenos_{i}", barraRepsGO.transform,
                "─", new Color(0.20f, 0.20f, 0.22f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(60f, 0f));
            botoesMenosArr[i] = menosGO.GetComponent<Button>();

            // Texto reps
            var repsGO   = new GameObject($"TextoReps_{i}");
            repsGO.transform.SetParent(barraRepsGO.transform, false);
            var repsRect = repsGO.AddComponent<RectTransform>();
            repsRect.anchorMin        = new Vector2(0.5f, 0f);
            repsRect.anchorMax        = new Vector2(0.5f, 1f);
            repsRect.pivot            = new Vector2(0.5f, 0.5f);
            repsRect.anchoredPosition = Vector2.zero;
            repsRect.sizeDelta        = new Vector2(80f, 0f);
            var repsTMP = repsGO.AddComponent<TextMeshProUGUI>();
            repsTMP.text      = "1";
            repsTMP.fontSize  = 26;
            repsTMP.color     = Color.white;
            repsTMP.fontStyle = FontStyles.Bold;
            repsTMP.alignment = TextAlignmentOptions.Center;
            textoRepsArr[i]   = repsTMP;

            // Botão Mais
            var maisGO   = CriarBotaoSimples($"BotaoMais_{i}", barraRepsGO.transform,
                "+", new Color(0.20f, 0.20f, 0.22f),
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-8f, 0f), new Vector2(60f, 0f));
            botoesMaisArr[i] = maisGO.GetComponent<Button>();
        }

        // ── Botão Iniciar Sessão ──────────────────────────────────────
        var btnIniciarGO = new GameObject("BotaoIniciarSessao");
        btnIniciarGO.transform.SetParent(painelSelGO.transform, false);
        var btnIniciarRect = btnIniciarGO.AddComponent<RectTransform>();
        btnIniciarRect.anchorMin        = new Vector2(0.5f, 0f);
        btnIniciarRect.anchorMax        = new Vector2(0.5f, 0f);
        btnIniciarRect.pivot            = new Vector2(0.5f, 0f);
        btnIniciarRect.anchoredPosition = new Vector2(0f, 30f);
        btnIniciarRect.sizeDelta        = new Vector2(280f, 58f);
        var btnIniciarImg = btnIniciarGO.AddComponent<Image>();
        btnIniciarImg.color = new Color(0.15f, 0.65f, 0.25f);
        var btnIniciarBtn = btnIniciarGO.AddComponent<Button>();
        var btnIniciarColors = btnIniciarBtn.colors;
        btnIniciarColors.highlightedColor = new Color(0.2f, 0.85f, 0.35f);
        btnIniciarColors.pressedColor     = new Color(0.1f, 0.45f, 0.18f);
        btnIniciarBtn.colors = btnIniciarColors;

        var btnIniciarLblGO   = new GameObject("Label");
        btnIniciarLblGO.transform.SetParent(btnIniciarGO.transform, false);
        var btnIniciarLblRect = btnIniciarLblGO.AddComponent<RectTransform>();
        btnIniciarLblRect.anchorMin = Vector2.zero;
        btnIniciarLblRect.anchorMax = Vector2.one;
        btnIniciarLblRect.offsetMin = Vector2.zero;
        btnIniciarLblRect.offsetMax = Vector2.zero;
        var btnIniciarLbl = btnIniciarLblGO.AddComponent<TextMeshProUGUI>();
        btnIniciarLbl.text          = "▶ Iniciar Sessão";
        btnIniciarLbl.fontSize      = 24;
        btnIniciarLbl.color         = Color.white;
        btnIniciarLbl.fontStyle     = FontStyles.Bold;
        btnIniciarLbl.alignment     = TextAlignmentOptions.Center;
        btnIniciarLbl.raycastTarget = false;

        backdropSel.SetActive(false); // aparece após calibração

        // ── PrevenGameManager ─────────────────────────────────────────
        var gameManager = appManager.AddComponent<PrevenGameManager>();
        gameManager.Esqueleto               = esqueleto;
        gameManager.CalibracaoManager       = calibManager;
        gameManager.CanvasJogo              = gameCanvas;
        gameManager.HUDJogo                 = hudJogo;
        gameManager.TextoPercentagem        = textoPct;
        gameManager.TextoRepeticao          = textoRep;
        gameManager.TextoTempo              = textoTempo;
        gameManager.TextoCompensacao        = textoComp;
        gameManager.PainelFim               = backdropFim;
        gameManager.TextoResultado          = textoResult;
        gameManager.TextoEstatisticas       = textoEstat;
        gameManager.BotaoNovaSessao         = botaoRepBtn;
        gameManager.PainelSelecaoExercicio  = backdropSel;
        gameManager.BotaoVoltarMenu         = btnVoltarSel;
        gameManager.BotoesToggleExercicio   = botoesToggle;
        gameManager.TextosRepsExercicio     = textoRepsArr;
        gameManager.BotoesMenos             = botoesMenosArr;
        gameManager.BotoesMais              = botoesMaisArr;
        gameManager.BotaoIniciarSessao      = btnIniciarBtn;
        gameManager.BotaoBracoDireito       = btnBracoDirBtn;
        gameManager.BotaoBracoEsquerdo      = btnBracoEsqBtn;
        gameManager.ImagensExercicio        = imagensExArr;
        gameManager.DemoExercicio           = demoLoop;
        gameManager.SpritesExercicios       = spritesExercicios;
        // Listeners adicionados em runtime pelo Start() do PrevenGameManager

        // ── Iluminação ────────────────────────────────────────────────
        // Luz direcional suave vinda de cima
        var luzObj = new GameObject("LuzDirecional");
        var luz = luzObj.AddComponent<Light>();
        luz.type      = LightType.Directional;
        luz.intensity = 1.2f;
        luz.color     = new Color(1f, 0.95f, 0.85f);
        luzObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        // ── Câmara principal ──────────────────────────────────────────
        // Camera.main exige Tag="MainCamera" — usa FindObjectOfType como fallback.
        Camera mainCam = Camera.main ?? Object.FindObjectOfType<Camera>();
        if (mainCam != null)
        {
            if (mainCam.gameObject.tag != "MainCamera")
                mainCam.gameObject.tag = "MainCamera";

            mainCam.clearFlags      = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            mainCam.fieldOfView     = 55f;
            mainCam.nearClipPlane   = 0.1f;
            mainCam.farClipPlane    = 200f;

            // Pose inicial provisória (antes da calibração); o OmmoCameraSetup reposiciona
            // a câmara relativamente ao ombro calibrado (lateral + acima) em runtime.
            mainCam.transform.position = new Vector3(-10f, 18f, 2f);
            mainCam.transform.LookAt(new Vector3(0f, 13f, 4f));

            if (mainCam.gameObject.GetComponent<OmmoCameraSetup>() == null)
                mainCam.gameObject.AddComponent<OmmoCameraSetup>();

            Debug.Log($"[OmmoBuilder] Câmara configurada: {mainCam.gameObject.name} pos={mainCam.transform.position}");
        }
        else
        {
            Debug.LogWarning("[OmmoBuilder] Nenhuma câmara encontrada na cena — adiciona uma manualmente.");
        }

        Debug.Log("[OmmoBuilder] ✅ Mundo 3D pronto!");
        EditorUtility.DisplayDialog("Ommo Scene Builder — Jogo",
            "✅ Mundo 3D criado!\n\n" +
            "• Ecrã 'A ligar ao sensor Ommo...' aparece ao arrancar\n" +
            "• Após ~2.5s o mundo 3D é revelado automaticamente\n" +
            "• Painel de calibração aparece quando os 2 sensores ligam\n" +
            "• Após calibração o esqueleto e o jogo iniciam automaticamente\n" +
            "• Waypoints verdes marcam o percurso do braço ao lado\n" +
            "• BaseStation (azul) = origem do espaço de tracking\n\n" +
            "Guarda a cena (Ctrl+S) e carrega Play para testar.",
            "OK");
    }

    /// <summary>
    /// Constrói a UI de demonstração do exercício (canto inferior esquerdo) que cicla
    /// as imagens do exercício em loop. Responsiva via CanvasScaler do canvas pai.
    /// </summary>
    static ExercicioDemoLoop ConstruirDemoExercicio(Transform pai)
    {
        // Container preto (borda visual de 8 px)
        var container = new GameObject("DemoContainer");
        container.transform.SetParent(pai, false);
        var cRect = container.AddComponent<RectTransform>();
        cRect.anchorMin        = new Vector2(0f, 0f);
        cRect.anchorMax        = new Vector2(0f, 0f);
        cRect.pivot            = new Vector2(0f, 0f);
        cRect.anchoredPosition = new Vector2(40f, 40f);
        cRect.sizeDelta        = new Vector2(576f, 336f); // 560+16 × 320+16
        var cImg = container.AddComponent<Image>();
        cImg.color        = new Color(0f, 0f, 0f, 0.85f);
        cImg.raycastTarget = false;

        // Imagem da demo (filho do container, padding de 8 px)
        var demoGO = new GameObject("DemoExercicio");
        demoGO.transform.SetParent(container.transform, false);
        var demoRect = demoGO.AddComponent<RectTransform>();
        demoRect.anchorMin        = new Vector2(0f, 0f);
        demoRect.anchorMax        = new Vector2(0f, 0f);
        demoRect.pivot            = new Vector2(0f, 0f);
        demoRect.anchoredPosition = new Vector2(8f, 8f);
        demoRect.sizeDelta        = new Vector2(560f, 320f);

        var demoImg = demoGO.AddComponent<Image>();
        demoImg.color          = new Color(1f, 1f, 1f, 0.96f);
        demoImg.preserveAspect = true;
        demoImg.raycastTarget  = false;

        // Ajuste da borda ao tamanho real do sprite: o DemoContainer passa a envolver a
        // imagem (padding de 8 px uniforme) em vez de deixar faixas pretas. O ExercicioDemoLoop
        // chama-o a cada troca de sprite.
        var ajuste = demoGO.AddComponent<AjusteImagemBorda>();
        ajuste.Imagem        = demoImg;
        ajuste.Container      = cRect;
        ajuste.TamanhoMaximo = new Vector2(560f, 320f); // = área útil dentro do container
        ajuste.Borda         = 8f;

        var demoLoop = demoGO.AddComponent<ExercicioDemoLoop>();
        demoLoop.ImagemAlvo        = demoImg;
        demoLoop.IntervaloSegundos = 0.5f;
        demoLoop.Ajuste            = ajuste;

        return demoLoop;
    }

    // ─────────────────────────────────────────────────────────────────
    // PrevenGame — 3 cenas (MainMenu + ClinicalTrial + Gamification)
    // ─────────────────────────────────────────────────────────────────

    [MenuItem("Ommo/PrevenGame/Build 3 Cenas (Menu + Clinical + Gamification)")]
    public static void BuildTresCenas()
    {
        if (!EditorUtility.DisplayDialog("PrevenGame — Build 3 Cenas",
            "Cria/grava 3 cenas em Assets/Scenes:\n" +
            "• MainMenu.unity\n• ClinicalTrial.unity\n• Gamification.unity\n\n" +
            "E regista-as no Build Settings (MainMenu como cena inicial).\n" +
            "Cenas existentes com estes nomes são sobrescritas. Continuar?",
            "Sim", "Cancelar"))
            return;

        const string dir   = "Assets/Scenes";
        const string pMenu = "Assets/Scenes/MainMenu.unity";
        const string pClin = "Assets/Scenes/ClinicalTrial.unity";
        const string pGam  = "Assets/Scenes/Gamification.unity";
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

        // Clinical Trial (reaproveita o builder do jogo existente).
        var sClin = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        BuildJogoCore();
        EditorSceneManager.SaveScene(sClin, pClin);

        // Gamification.
        var sGam = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        BuildGamificationCore();
        EditorSceneManager.SaveScene(sGam, pGam);

        // MainMenu.
        var sMenu = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        BuildMainMenuCore();
        EditorSceneManager.SaveScene(sMenu, pMenu);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(pMenu, true),
            new EditorBuildSettingsScene(pClin, true),
            new EditorBuildSettingsScene(pGam, true),
        };
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(pMenu);

        EditorUtility.DisplayDialog("PrevenGame — Build 3 Cenas",
            "✅ 3 cenas criadas e registadas no Build Settings.\n\n" +
            "Abre MainMenu e carrega Play. Os assets do Gamification (alvo, dardo, sala, " +
            "imagens) são placeholders — substitui-os depois sem alterar a lógica.", "OK");
    }

    // ── MainMenu ──────────────────────────────────────────────────────
    public static void BuildMainMenuCore()
    {
        ClearExistingJogo();
        GarantirEventSystem();

        CreateEmpty("OmmoBootstrap").AddComponent<OmmoBootstrap>();

        var canvas = CriarCanvasOverlay("MenuCanvas", 10);
        var fundo  = new GameObject("Fundo");
        fundo.transform.SetParent(canvas.transform, false);
        StretchFull(fundo.AddComponent<RectTransform>());
        fundo.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.09f, 1f);

        CriarTexto("Titulo", canvas.transform, "PrevenGame", 64, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -160f), new Vector2(900f, 90f), Color.white);
        CriarTexto("Subtitulo", canvas.transform, "Reabilitação dos Membros Superiores", 28,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -260f), new Vector2(900f, 50f), new Color(0.8f, 0.8f, 0.85f));

        var btnClin = CriarBotaoSimples("BotaoClinicalTrial", canvas.transform, "Clinical Trial",
            new Color(0.2f, 0.5f, 0.85f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-180f, -20f), new Vector2(320f, 90f)).GetComponent<Button>();
        var btnGam = CriarBotaoSimples("BotaoGamification", canvas.transform, "Gamification",
            new Color(0.85f, 0.45f, 0.15f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(180f, -20f), new Vector2(320f, 90f)).GetComponent<Button>();

        var ctrl = CreateEmpty("MainMenu").AddComponent<MainMenuController>();
        ctrl.BotaoClinicalTrial = btnClin;
        ctrl.BotaoGamification  = btnGam;

        Camera cam = Camera.main ?? Object.FindObjectOfType<Camera>();
        if (cam != null)
        {
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f);
        }
        Debug.Log("[OmmoBuilder] ✅ Cena MainMenu construída.");
    }

    // ── Gamification ──────────────────────────────────────────────────
    public static void BuildGamificationCore()
    {
        ClearExistingJogo();
        var s = ConstruirScaffold();

        // Sala preta + câmara (posições a afinar com os assets reais).
        Camera cam = Camera.main ?? Object.FindObjectOfType<Camera>();
        if (cam != null)
        {
            if (cam.gameObject.tag != "MainCamera") cam.gameObject.tag = "MainCamera";
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView     = 55f;
            cam.nearClipPlane   = 0.1f;
            cam.farClipPlane    = 200f;
            // Câmara fixa lateral elevada: enquadra o braço E o alvo distante (não usa
            // OmmoCameraSetup — esse reposicionamento "ao ombro" é só para o Clinical).
            cam.transform.position = new Vector3(-9f, 17f, -3f);
            cam.transform.LookAt(new Vector3(0f, 13f, 5f));
        }
        var luz = CreateEmpty("LuzDirecional").AddComponent<Light>();
        luz.type      = LightType.Directional;
        luz.intensity = 1.0f;
        luz.color     = new Color(1f, 0.97f, 0.9f);
        luz.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

        // Sala/ambiente (prefab real se existir; senão fica a sala preta da câmara).
        var salaPrefab = CarregarPrefab(AssetSala);
        if (salaPrefab != null) Object.Instantiate(salaPrefab).name = "Sala";

        // Alvo de 5 aros reais (Alvo1=exterior … Alvo5=bullseye), concêntricos.
        var alvoGO = CreateEmpty("Alvo");
        alvoGO.transform.position = new Vector3(0f, 13f, 8f);
        // Plano do alvo virado para o jogador (transform.forward = normal).
        alvoGO.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        var alvo = alvoGO.AddComponent<GamificationTarget>();
        // Rede de segurança: se algum AroPrefab falhar o carregamento, o alvo mostra
        // um placeholder paramétrico em vez de ficar invisível.
        alvo.CriarVisualPlaceholder = true;
        alvo.RaioExterior = 1.5f; // fallback se faltarem os prefabs
        alvo.AroPrefabs = new[]
        {
            CarregarPrefab(AssetAlvo1), CarregarPrefab(AssetAlvo2), CarregarPrefab(AssetAlvo3),
            CarregarPrefab(AssetAlvo4), CarregarPrefab(AssetAlvo5),
        };

        // Manager.
        var gm = CreateEmpty("GamificationManager").AddComponent<GamificationManager>();
        gm.Esqueleto         = s.Esqueleto;
        gm.CalibracaoManager = s.CalibManager;
        gm.Alvo              = alvo;
        gm.DardoPrefab       = CarregarPrefab(AssetDardo); // null → dardo placeholder

        // HUD.
        var hud = new GameObject("HUDJogo");
        hud.transform.SetParent(s.GameCanvasGO.transform, false);
        StretchFull(hud.AddComponent<RectTransform>()); // cobre o ecrã: filhos ancoram aos cantos reais
        // Pontuação (% média) — canto superior esquerdo.
        var textoPont = CriarTexto("TextoPontuacao", hud.transform, "0 %", 40,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -20f), new Vector2(220f, 56f), Color.white);
        // Dardos restantes — canto inferior direito.
        var textoDardos = CriarTexto("TextoDardos", hud.transform, "5/5", 40,
            TextAlignmentOptions.Right,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-40f, 40f), new Vector2(220f, 56f), Color.white);
        var gamDemoLoop = ConstruirDemoExercicio(hud.transform);
        var gamDemoSprites = CarregarSpritesExercicio(1); // demo = imagens do exercício (igual ao clinical), não o png dos dardos
        if (gamDemoSprites.Length > 0) gamDemoLoop.Sprites = gamDemoSprites;
        hud.SetActive(false);
        gm.HUDJogo        = hud;
        gm.TextoPontuacao = textoPont;
        gm.TextoDardos    = textoDardos;

        // Painel Fim — fundo preto full-screen + conteúdo centrado.
        var backdropFimG = new GameObject("PainelFim");
        backdropFimG.transform.SetParent(s.GameCanvasGO.transform, false);
        StretchFull(backdropFimG.AddComponent<RectTransform>());
        backdropFimG.AddComponent<Image>().color = new Color(0f, 0f, 0f, 1f);
        var fim = new GameObject("Conteudo");
        fim.transform.SetParent(backdropFimG.transform, false);
        var fimRect = fim.AddComponent<RectTransform>();
        fimRect.anchorMin = fimRect.anchorMax = fimRect.pivot = new Vector2(0.5f, 0.5f);
        fimRect.anchoredPosition = Vector2.zero;
        fimRect.sizeDelta        = new Vector2(700f, 260f);
        var textoResult = CriarTexto("TextoResultado", fim.transform, "🎯 Sessão concluída!", 28,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 20f), new Vector2(0f, 80f), Color.white);
        var btnNova = CriarBotaoSimples("BotaoNovaSessao", fim.transform, "Nova Sessão",
            new Color(0.2f, 0.7f, 0.3f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 28f), new Vector2(220f, 50f)).GetComponent<Button>();
        backdropFimG.SetActive(false);
        gm.PainelFim       = backdropFimG;
        gm.TextoResultado  = textoResult;
        gm.BotaoNovaSessao = btnNova;

        // Painel de seleção — fundo preto full-screen + conteúdo centrado.
        var backdropSelG = new GameObject("PainelSelecao");
        backdropSelG.transform.SetParent(s.GameCanvasGO.transform, false);
        StretchFull(backdropSelG.AddComponent<RectTransform>());
        backdropSelG.AddComponent<Image>().color = new Color(0f, 0f, 0f, 1f);
        var painelSel = new GameObject("Conteudo");
        painelSel.transform.SetParent(backdropSelG.transform, false);
        var selRect = painelSel.AddComponent<RectTransform>();
        selRect.anchorMin = selRect.anchorMax = selRect.pivot = new Vector2(0.5f, 0.5f);
        selRect.anchoredPosition = Vector2.zero;
        selRect.sizeDelta        = new Vector2(900f, 620f);

        // Botão Voltar ao menu (canto superior esquerdo).
        var btnVoltarGam = CriarBotaoSimples("BotaoVoltarMenu", painelSel.transform, "◀ Menu",
            new Color(0.3f, 0.3f, 0.32f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -16f), new Vector2(140f, 44f)).GetComponent<Button>();

        CriarTexto("Titulo", painelSel.transform, "Lançamento de Dardos", 30,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -28f), new Vector2(700f, 50f), Color.white);

        var btnDir = CriarBotaoSimples("BotaoBracoDireito", painelSel.transform, "Braço Direito",
            new Color(0.2f, 0.7f, 0.3f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-130f, -90f), new Vector2(250f, 46f)).GetComponent<Button>();
        var btnEsq = CriarBotaoSimples("BotaoBracoEsquerdo", painelSel.transform, "Braço Esquerdo",
            new Color(0.35f, 0.35f, 0.35f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(130f, -90f), new Vector2(250f, 46f)).GetComponent<Button>();

        // Card com a imagem do exercício.
        var cardGO = new GameObject("Card");
        cardGO.transform.SetParent(painelSel.transform, false);
        var cardRect = cardGO.AddComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, 20f);
        cardRect.sizeDelta        = new Vector2(360f, 300f);
        cardGO.AddComponent<Image>().color = new Color(0.18f, 0.55f, 0.25f);

        var imgExGO = new GameObject("ImagemExercicio");
        imgExGO.transform.SetParent(cardGO.transform, false);
        var imgExRect = imgExGO.AddComponent<RectTransform>();
        imgExRect.anchorMin = imgExRect.anchorMax = imgExRect.pivot = new Vector2(0.5f, 0.5f);
        imgExRect.anchoredPosition = Vector2.zero;
        imgExRect.sizeDelta        = new Vector2(328f, 252f);
        var imgEx = imgExGO.AddComponent<Image>();
        imgEx.preserveAspect = true;
        imgEx.raycastTarget  = false;
        var cardSprite = CarregarSpriteAsset(AssetCard)
                         ?? CarregarSpriteAsset("Assets/Execercises/ex1/1.jpg");
        if (cardSprite != null) imgEx.sprite = cardSprite; else imgEx.color = new Color(0.1f, 0.1f, 0.12f);

        var barraReps = new GameObject("BarraReps");
        barraReps.transform.SetParent(painelSel.transform, false);
        var brRect = barraReps.AddComponent<RectTransform>();
        brRect.anchorMin = brRect.anchorMax = brRect.pivot = new Vector2(0.5f, 0f);
        brRect.anchoredPosition = new Vector2(0f, 100f);
        brRect.sizeDelta        = new Vector2(300f, 52f);
        var btnMenos = CriarBotaoSimples("BotaoMenos", barraReps.transform, "─",
            new Color(0.2f, 0.2f, 0.22f),
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(8f, 0f), new Vector2(60f, 0f)).GetComponent<Button>();
        var textoReps = CriarTexto("TextoReps", barraReps.transform, "5", 26,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(80f, 0f), Color.white);
        var btnMais = CriarBotaoSimples("BotaoMais", barraReps.transform, "+",
            new Color(0.2f, 0.2f, 0.22f),
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(-8f, 0f), new Vector2(60f, 0f)).GetComponent<Button>();

        var btnIniciar = CriarBotaoSimples("BotaoIniciar", painelSel.transform, "▶ Iniciar",
            new Color(0.15f, 0.65f, 0.25f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 30f), new Vector2(280f, 58f)).GetComponent<Button>();

        backdropSelG.SetActive(false);

        gm.PainelSelecao      = backdropSelG;
        gm.BotaoVoltarMenu    = btnVoltarGam;
        gm.BotaoBracoDireito  = btnDir;
        gm.BotaoBracoEsquerdo = btnEsq;
        gm.BotaoMenos         = btnMenos;
        gm.BotaoMais          = btnMais;
        gm.TextoReps          = textoReps;
        gm.BotaoIniciar       = btnIniciar;
        gm.ImagemExercicio    = imgEx;

        Debug.Log("[OmmoBuilder] ✅ Cena Gamification construída.");
    }

    // ── Scaffold partilhado (Ommo + calibração + esqueleto) ───────────
    class ScaffoldRefs
    {
        public OmmoSensorManager     SensorMgr;
        public OmmoEsqueletoJogador  Esqueleto;
        public OmmoCalibracaoManager CalibManager;
        public GameObject            GameCanvasGO;
    }

    static ScaffoldRefs ConstruirScaffold()
    {
        GarantirEventSystem();

        CreateEmpty("OmmoBootstrap").AddComponent<OmmoBootstrap>();

        var appManager = CreateEmpty("AppManager");
        var monitor    = appManager.AddComponent<OmmoHardwareMonitor>();
        var devManager = appManager.AddComponent<OmmoDeviceManager>();
        var sensorMgr  = appManager.AddComponent<OmmoSensorManager>();

        // BaseStation.
        var baseStation = CreateEmpty("BaseStation");
        baseStation.transform.position = new Vector3(0f, 13f, 0f);
        var bsMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bsMarker.name = "BaseStation_Marcador";
        bsMarker.transform.SetParent(baseStation.transform);
        bsMarker.transform.localPosition = Vector3.zero;
        bsMarker.transform.localScale    = Vector3.one * 0.08f;
        Object.DestroyImmediate(bsMarker.GetComponent<SphereCollider>());
        bsMarker.GetComponent<MeshRenderer>().sharedMaterial =
            new Material(Shader.Find("Unlit/Color")) { color = new Color(0.2f, 0.6f, 1f) };
        // Gamification: origem/BaseStation invisível — só o dardo e a linha verde
        // interpretam o que se passa por trás. O marcador fica só como referência de transform.
        bsMarker.GetComponent<MeshRenderer>().enabled = false;

        // Objeto controlado pelo Ommo (prefab real ou cubo placeholder com RimGlow).
        var cubo = CriarVisualSensor();

        var trackedRoot = CreateEmpty("TrackedDevicePrefab_TEMP");
        var ommoDevice  = trackedRoot.AddComponent<OmmoDevice>();
        ommoDevice.SensorPrefab  = cubo;
        ommoDevice.RequestedMode = Ommo.DeviceFusionMode.FullFusion;
        cubo.transform.SetParent(trackedRoot.transform, false);
        cubo.transform.localPosition = Vector3.zero;
        trackedRoot.SetActive(false);

        devManager.BaseStation    = baseStation;
        devManager.UnityScaleInCM = 10f;
        devManager.DeviceTypePrefabs = new OmmoDeviceManager.DeviceTypePrefab[]
        {
            new OmmoDeviceManager.DeviceTypePrefab { DeviceType = 0, Prefab = trackedRoot }
        };

        // Painel "A ligar".
        var painelCanvas = CriarCanvasOverlay("PainelALigar_TEMP", 99);
        var fundoGO = new GameObject("Fundo");
        fundoGO.transform.SetParent(painelCanvas.transform, false);
        StretchFull(fundoGO.AddComponent<RectTransform>());
        fundoGO.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 1f);
        var textoEstado = CriarTexto("TextoEstado", painelCanvas.transform,
            "A ligar ao sensor Ommo...", 28, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(800f, 80f), Color.white);

        sensorMgr.DeviceManager   = devManager;
        sensorMgr.HardwareMonitor = monitor;
        sensorMgr.PainelALigar    = painelCanvas;
        sensorMgr.TextoEstado     = textoEstado;

        var esqueleto = CreateEmpty("EsqueletoJogador").AddComponent<OmmoEsqueletoJogador>();

        // Calibração.
        var calibCanvas = CriarCanvasOverlay("CalibracaoCanvas", 50);
        var painelCalib = new GameObject("PainelCalibracao");
        painelCalib.transform.SetParent(calibCanvas.transform, false);
        painelCalib.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var pcRect = painelCalib.GetComponent<RectTransform>();
        pcRect.anchorMin = pcRect.anchorMax = pcRect.pivot = new Vector2(0.5f, 0.5f);
        pcRect.anchoredPosition = Vector2.zero;
        pcRect.sizeDelta        = new Vector2(750f, 380f);

        var textoPasso = CriarTexto("TextoPasso", painelCalib.transform, "", 18,
            TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -14f), new Vector2(0f, 28f), new Color(0.8f, 0.8f, 0.8f));
        var textoInstr = CriarTexto("TextoInstrucao", painelCalib.transform, "A aguardar sensor...", 28,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 30f), new Vector2(0f, 90f), Color.white);
        var textoSub = CriarTexto("TextoSub", painelCalib.transform, "Liga o sensor da palma.", 18,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -28f), new Vector2(0f, 50f), new Color(0.75f, 0.75f, 0.75f));

        var barraGO = new GameObject("BarraProgresso");
        barraGO.transform.SetParent(painelCalib.transform, false);
        var barraRect = barraGO.AddComponent<RectTransform>();
        barraRect.anchorMin = barraRect.anchorMax = new Vector2(0.5f, 0f);
        barraRect.pivot     = new Vector2(0.5f, 0f);
        barraRect.anchoredPosition = new Vector2(0f, 28f);
        barraRect.sizeDelta        = new Vector2(500f, 20f);
        var barraBg = new GameObject("BarraFundo");
        barraBg.transform.SetParent(barraGO.transform, false);
        barraBg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
        StretchFull(barraBg.GetComponent<RectTransform>());
        var barraFill = new GameObject("BarraFill");
        barraFill.transform.SetParent(barraGO.transform, false);
        var barraFillImg = barraFill.AddComponent<Image>();
        barraFillImg.color      = new Color(0.2f, 0.85f, 0.3f);
        barraFillImg.type       = Image.Type.Filled;
        barraFillImg.fillMethod = Image.FillMethod.Horizontal;
        barraFillImg.fillAmount = 0f;
        StretchFull(barraFill.GetComponent<RectTransform>());

        var calibManager = appManager.AddComponent<OmmoCalibracaoManager>();
        calibManager.SensorManager        = sensorMgr;
        calibManager.Esqueleto            = esqueleto;
        calibManager.PainelCalibracao     = painelCalib;
        calibManager.TextoInstrucao       = textoInstr;
        calibManager.TextoPasso           = textoPasso;
        calibManager.TextoSub             = textoSub;
        calibManager.BarraProgressoImagem = barraFillImg;

        var gameCanvas = CriarCanvasOverlay("PrevenGameCanvas", 40);

        return new ScaffoldRefs
        {
            SensorMgr    = sensorMgr,
            Esqueleto    = esqueleto,
            CalibManager = calibManager,
            GameCanvasGO = gameCanvas.gameObject,
        };
    }

    // ── Helpers locais ────────────────────────────────────────────────
    static void GarantirEventSystem()
    {
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = CreateEmpty("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    static Canvas CriarCanvasOverlay(string nome, int ordem)
    {
        var go = new GameObject(nome);
        var c  = go.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = ordem;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    static TextMeshProUGUI CriarTexto(string nome, Transform pai, string texto, int tamanho,
        TextAlignmentOptions align, Vector2 aMin, Vector2 aMax, Vector2 pivot,
        Vector2 pos, Vector2 size, Color cor)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = aMin; rect.anchorMax = aMax; rect.pivot = pivot;
        rect.anchoredPosition = pos; rect.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = texto;
        tmp.fontSize  = tamanho;
        tmp.color     = cor;
        tmp.alignment = align;
        return tmp;
    }

    static Material CriarMaterialRimGlow(Color cor)
    {
        var m = new Material(Shader.Find("PrevenGame/RimGlow") ?? Shader.Find("Standard")) { color = cor };
        if (m.HasProperty("_RimColor"))
        {
            m.SetColor("_RimColor", Color.white);
            m.SetFloat("_RimPower", 3f);
            m.SetFloat("_RimIntensity", 2.2f);
        }
        return m;
    }

    // ─────────────────────────────────────────────────────────────────
    // Convenção de assets — larga os ficheiros nestes caminhos e corre
    // "Ommo → PrevenGame → Build 3 Cenas". Se faltarem, usa-se placeholder.
    // ─────────────────────────────────────────────────────────────────
    const string PastaAssets = "Assets/Prefabs/PrevenGameAssets/Dardos";
    const string AssetDardo  = PastaAssets + "/Dardo.prefab";      // dardo (Gamification)
    const string AssetAlvo1  = PastaAssets + "/Alvo1.prefab";      // aro exterior
    const string AssetAlvo2  = PastaAssets + "/Alvo2.prefab";
    const string AssetAlvo3  = PastaAssets + "/Alvo3.prefab";
    const string AssetAlvo4  = PastaAssets + "/Alvo4.prefab";
    const string AssetAlvo5  = PastaAssets + "/Alvo5.prefab";      // bullseye
    const string AssetSala   = PastaAssets + "/Sala.prefab";       // sala/ambiente (Gamification)
    const string AssetCard   = PastaAssets + "/DardosSelectionCard.png"; // imagem do card de seleção


    static GameObject CarregarPrefab(string path)
        => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    static Sprite CarregarSpriteAsset(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType      = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>Sprites de um exercício: pasta ex{n}/1.jpg…5.jpg.</summary>
    static Sprite[] CarregarSpritesExercicio(int n)
    {
        var lista = new List<Sprite>();
        for (int s = 1; s <= 5; s++)
        {
            var sp = CarregarSpriteAsset($"Assets/Execercises/ex{n}/{s}.jpg");
            if (sp != null) lista.Add(sp);
        }
        return lista.ToArray();
    }

    /// <summary>
    /// Objeto controlado pelo Ommo = cubo branco com RimGlow.
    /// No Clinical é o visual jogável; na Gamification é apenas o âncora de tracking
    /// (escondido por GamificationManager.PrepararSensorVisual — o dardo é o visual).
    /// </summary>
    static GameObject CriarVisualSensor()
    {
        var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubo.name = "CuboSensor";
        cubo.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        Object.DestroyImmediate(cubo.GetComponent<BoxCollider>());
        cubo.GetComponent<MeshRenderer>().sharedMaterial = CriarMaterialRimGlow(new Color(0.95f, 0.95f, 0.95f));
        return cubo;
    }


    // ── Helpers ───────────────────────────────────────────────────────

    static void ClearExistingJogo()
    {
        DestroyRootsByName(
            "AppManager", "BaseStation", "Piso", "LuzDirecional",
            "EventSystem", "TrackedDevicePrefab_TEMP", "ObjetoControlado", "CuboSensor",
            "PainelALigar_TEMP", "CalibracaoCanvas", "EsqueletoJogador", "PrevenGameCanvas",
            // restos da cena de diagnóstico
            "MainCanvas", "HUDCanvas", "GridCamera", "DeviceRowPrefab_TEMP");
    }

    // GameObject.Find() ignora objetos inativos (ex: TrackedDevicePrefab_TEMP).
    // GetRootGameObjects() devolve todos — ativos e inativos — como snapshot seguro.
    static void DestroyRootsByName(params string[] names)
    {
        var nameSet = new System.Collections.Generic.HashSet<string>(names);
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (nameSet.Contains(go.name))
                Object.DestroyImmediate(go);
        }
    }

    static GameObject CreateEmpty(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }


    static void StretchFull(RectTransform rect)
    {
        rect.anchorMin        = Vector2.zero;
        rect.anchorMax        = Vector2.one;
        rect.offsetMin        = Vector2.zero;
        rect.offsetMax        = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    static void AddLayoutElement(GameObject go, float preferredWidth, float preferredHeight, bool flexible)
    {
        var le = go.AddComponent<LayoutElement>();
        if (preferredWidth  > 0) le.preferredWidth  = preferredWidth;
        if (preferredHeight > 0) le.preferredHeight = preferredHeight;
        if (flexible) le.flexibleWidth = 1;
    }

    static void AddLayoutElement(Button btn, float w, float h, bool flex)
        => AddLayoutElement(btn.gameObject, w, h, flex);

    static TextMeshProUGUI FindTMP(GameObject root, string name)
    {
        var t = root.transform.Find(name);
        if (t != null) return t.GetComponent<TextMeshProUGUI>();
        // Deep search
        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (tmp.name == name) return tmp;
        return null;
    }

    static Button FindButton(GameObject root, string name)
    {
        foreach (var btn in root.GetComponentsInChildren<Button>(true))
            if (btn.name == name) return btn;
        return null;
    }

    static Transform FindTransform(GameObject root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    /// <summary>
    /// Cria um botão simples (Image + Button + Label TextMeshPro) como filho de <paramref name="pai"/>.
    /// Usado pelos cards de seleção de exercícios para os botões +/─.
    /// </summary>
    static GameObject CriarBotaoSimples(
        string nome, Transform pai, string label, Color cor,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go   = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.pivot            = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = sizeDelta;
        var img = go.AddComponent<Image>();
        img.color = cor;
        go.AddComponent<Button>();

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lblRect = lblGO.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.sizeDelta = Vector2.zero;
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text          = label;
        tmp.fontSize      = 24;
        tmp.color         = Color.white;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return go;
    }
}
