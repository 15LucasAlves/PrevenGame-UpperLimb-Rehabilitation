using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// OmmoSceneBuilder — Constrói as cenas do PrevenGame (Gamification) por código.
///
/// Menu: Ommo → PrevenGame → Build Cenas (Menu + Minijogo).
///   • Menu (hub): Splash → Calibração (helpers) → Seleção → [Score], via GameFlowManager.
///   • MinijogoDardos: mundo dos dardos + GamificationManager (runner) + PauseMenu.
///
/// Assets reais em Assets/Prefabs/PrevenGameAssets/{UIAssets,Dardos}. As posições são um ponto
/// de partida — afinam-se depois com os guias de layout. Se um asset faltar, usa-se placeholder.
/// </summary>
public class OmmoSceneBuilder
{
    // ── Caminhos de assets ────────────────────────────────────────────
    const string PastaUI     = "Assets/Prefabs/PrevenGameAssets/UIAssets";
    const string PastaDardos = "Assets/Prefabs/PrevenGameAssets/Dardos";
    const string PastaFontes = "Assets/Fonts";

    const string CenaMenu     = "Assets/Scenes/Menu.unity";
    const string CenaMinijogo = "Assets/Scenes/MinijogoDardos.unity";

    // Emoções por índice (HelperEmocao): Neutral, Pleased, Impressed, Laugh, Surprised, Worried, Disappointed
    static readonly string[] NomesJane =
        { "JaneNeutral", "JanePleased", "JaneImpressed", "JaneLaugh", "JaneSuprised", "JaneWorried", "JaneDissapointed" };
    static readonly string[] NomesPatrick =
        { "PatrickNeutral", "PatrickPleased", "PatrickImpressed", "PatrickLaugh", "PatrickSurprised", "PatrickWorried", "PatrickDisapointed" };

    // Cores dos textos dos cards
    static readonly Color CorNomeExercicio = new Color32(95, 120, 108, 255);  // #5F786C
    static readonly Color CorReps          = new Color32(132, 164, 149, 255); // #84A495
    static readonly Color CorBalao         = new Color32(35, 31, 32, 255);    // #231F20

    // ─────────────────────────────────────────────────────────────────
    [MenuItem("Ommo/PrevenGame/Build Cenas (Menu + Minijogo)")]
    public static void BuildCenas()
    {
        if (!EditorUtility.DisplayDialog("PrevenGame — Build Cenas",
            "Cria/grava 2 cenas em Assets/Scenes:\n• Menu.unity (splash+calibração+seleção+score)\n" +
            "• MinijogoDardos.unity\n\nCenas existentes com estes nomes são sobrescritas. Continuar?",
            "Sim", "Cancelar"))
            return;

        if (!System.IO.Directory.Exists("Assets/Scenes")) System.IO.Directory.CreateDirectory("Assets/Scenes");

        var sMenu = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        BuildMenuHub();
        EditorSceneManager.SaveScene(sMenu, CenaMenu);

        var sMini = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        BuildMinijogoDardos();
        EditorSceneManager.SaveScene(sMini, CenaMinijogo);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(CenaMenu, true),
            new EditorBuildSettingsScene(CenaMinijogo, true),
        };
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(CenaMenu);

        EditorUtility.DisplayDialog("PrevenGame — Build Cenas",
            "✅ Cenas criadas e registadas no Build Settings.\n\nAbre Menu e carrega Play.", "OK");
    }

    // ═════════════════════════════════════════════════════════════════
    // CENA MENU (HUB)
    // ═════════════════════════════════════════════════════════════════
    public static void BuildMenuHub()
    {
        GarantirEventSystem();
        CriarBootstrap();

        var scaffold = ConstruirScaffoldOmmo();

        // Câmara simples (fundo escuro; a calibração usa o esqueleto mas os menus tapam o 3D).
        Camera cam = Camera.main ?? Object.FindObjectOfType<Camera>();
        if (cam != null)
        {
            if (cam.gameObject.tag != "MainCamera") cam.gameObject.tag = "MainCamera";
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            if (cam.gameObject.GetComponent<OmmoCameraSetup>() == null)
                cam.gameObject.AddComponent<OmmoCameraSetup>();
        }

        // Canvas principal dos menus (overlay).
        var canvas = CriarCanvasOverlay("MenuCanvas", 40);

        // ── Painel Splash (firstMenu) ─────────────────────────────────
        var splash = CriarPainel("SplashPainel", canvas.transform);
        CriarImagemFull("SplashBg", splash.transform, UISprite("firstMenu"), new Color(0.06f, 0.06f, 0.09f, 1f));

        // ── Painel Calibração (só fundo; instruções via diálogo) ──────
        var calib = CriarPainel("CalibracaoPainel", canvas.transform);
        CriarImagemFull("CalibBg", calib.transform, UISprite("Background"), new Color(0.09f, 0.12f, 0.10f, 1f));

        // ── Painel Seleção (fundo + selection box + cards + START/EXIT) ─
        var selecao = CriarPainel("SelecaoPainel", canvas.transform);
        CriarImagemFull("SelecaoBg", selecao.transform, UISprite("mainMenuBackground"), new Color(0.09f, 0.12f, 0.10f, 1f));
        var selUI = ConstruirSelecao(selecao.transform);

        // ── Painel Score ──────────────────────────────────────────────
        var score = CriarPainel("ScorePainel", canvas.transform);
        CriarImagemFull("ScoreBg", score.transform, UISprite("mainMenuBackground"), new Color(0.09f, 0.12f, 0.10f, 1f));
        var scoreTxt = CriarTexto("ScoreTexto", score.transform, "", 40, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 120f), new Vector2(900f, 400f), Color.white, PoppinsExtraBold, FontStyles.Bold);

        // ── Diálogo único (personagem + balão) por cima das fases ─────
        var dialogoGO = CriarPainelDialogo("Dialogo", canvas.transform, out var dlg);

        // ── ScreenFader (canvas por cima) ─────────────────────────────
        var fader = CriarFader();

        // ── GameFlowManager ───────────────────────────────────────────
        var gfmGO = CreateEmpty("GameFlowManager");
        var gfm   = gfmGO.AddComponent<GameFlowManager>();
        gfm.Fader        = fader;
        gfm.Dialogo      = dlg;
        gfm.Calibracao   = scaffold.Calib;
        gfm.Selecao      = selUI;
        gfm.SplashPainel     = splash;
        gfm.CalibracaoPainel = calib;
        gfm.SelecaoPainel    = selecao;
        gfm.ScorePainel      = score;
        gfm.ScoreTexto       = scoreTxt;
        gfm.HelperCalibracao = HelperId.Jane;

        // Sequências de diálogo editáveis (assets). Não sobrescreve se já existirem.
        gfm.TutorialSelecaoSeq = CriarSequenciaDialogo("TutorialSelecao", FalasTutorialDefault());
        gfm.ScoreAltoSeq  = CriarSequenciaDialogo("ScoreAlto",  FalasScoreDefault(HelperEmocao.Impressed, "Excelente trabalho! Estás mesmo a melhorar."));
        gfm.ScoreMedioSeq = CriarSequenciaDialogo("ScoreMedio", FalasScoreDefault(HelperEmocao.Pleased,   "Bom trabalho! Continua assim."));
        gfm.ScoreBaixoSeq = CriarSequenciaDialogo("ScoreBaixo", FalasScoreDefault(HelperEmocao.Worried,   "Não faz mal — cada sessão conta. Vamos tentar outra vez!"));

        Debug.Log("[OmmoBuilder] ✅ Cena Menu construída.");
    }

    // ═════════════════════════════════════════════════════════════════
    // CENA MINIJOGO (DARDOS)
    // ═════════════════════════════════════════════════════════════════
    public static void BuildMinijogoDardos()
    {
        GarantirEventSystem();
        CriarBootstrap();

        var scaffold = ConstruirScaffoldOmmo(comCalibracao: false);

        // Câmara fixa a enquadrar braço + alvo.
        Camera cam = Camera.main ?? Object.FindObjectOfType<Camera>();
        if (cam != null)
        {
            if (cam.gameObject.tag != "MainCamera") cam.gameObject.tag = "MainCamera";
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView     = 55f;
            cam.nearClipPlane   = 0.1f;
            cam.farClipPlane    = 200f;
            cam.transform.position = new Vector3(-9f, 17f, -3f);
            cam.transform.LookAt(new Vector3(0f, 13f, 5f));
        }

        var luz = CreateEmpty("LuzDirecional").AddComponent<Light>();
        luz.type = LightType.Directional; luz.intensity = 1.0f;
        luz.color = new Color(1f, 0.97f, 0.9f);
        luz.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

        // Sala (opcional).
        var salaPrefab = CarregarPrefab($"{PastaDardos}/Sala.prefab");
        if (salaPrefab != null) Object.Instantiate(salaPrefab).name = "Sala";

        // Alvo de 5 aros.
        var alvoGO = CreateEmpty("Alvo");
        alvoGO.transform.position = new Vector3(0f, 13f, 8f);
        alvoGO.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        var alvo = alvoGO.AddComponent<GamificationTarget>();
        alvo.CriarVisualPlaceholder = true;
        alvo.RaioExterior = 1.5f;
        alvo.AroPrefabs = new[]
        {
            CarregarPrefab($"{PastaDardos}/Alvo1.prefab"), CarregarPrefab($"{PastaDardos}/Alvo2.prefab"),
            CarregarPrefab($"{PastaDardos}/Alvo3.prefab"), CarregarPrefab($"{PastaDardos}/Alvo4.prefab"),
            CarregarPrefab($"{PastaDardos}/Alvo5.prefab"),
        };

        // HUD.
        var hudCanvas = CriarCanvasOverlay("HUDCanvas", 40);
        var hud = CriarPainel("HUDJogo", hudCanvas.transform);
        var textoPont = CriarTexto("TextoPontuacao", hud.transform, "0 %", 40, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -20f), new Vector2(240f, 56f), Color.white, PoppinsExtraBold, FontStyles.Bold);
        var textoDardos = CriarTexto("TextoDardos", hud.transform, "0/0", 40, TextAlignmentOptions.Right,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-40f, 40f), new Vector2(240f, 56f), Color.white, PoppinsExtraBold, FontStyles.Bold);
        hud.SetActive(false);

        // GamificationManager (runner).
        var gmGO = CreateEmpty("GamificationManager");
        var gm   = gmGO.AddComponent<GamificationManager>();
        gm.Esqueleto     = scaffold.Esqueleto;
        gm.Alvo          = alvo;
        gm.DardoPrefab   = CarregarPrefab($"{PastaDardos}/Dardo.prefab");
        gm.HUDJogo       = hud;
        gm.TextoPontuacao = textoPont;
        gm.TextoDardos    = textoDardos;

        // Pausa (ESC).
        var pauseUI = ConstruirPausa(hudCanvas.transform, out var pauseMenu);

        // Controller.
        var ctrlGO = CreateEmpty("MinigameController");
        var ctrl   = ctrlGO.AddComponent<MinigameController>();
        ctrl.Jogo          = gm;
        ctrl.Esqueleto     = scaffold.Esqueleto;
        ctrl.SensorManager = scaffold.SensorMgr;

        // ScreenFader.
        CriarFader();

        Debug.Log("[OmmoBuilder] ✅ Cena MinijogoDardos construída.");
    }

    // ═════════════════════════════════════════════════════════════════
    // Scaffold Ommo partilhado
    // ═════════════════════════════════════════════════════════════════
    class ScaffoldRefs
    {
        public OmmoSensorManager     SensorMgr;
        public OmmoEsqueletoJogador  Esqueleto;
        public OmmoCalibracaoManager Calib;
    }

    static ScaffoldRefs ConstruirScaffoldOmmo(bool comCalibracao = true)
    {
        var appManager = CreateEmpty("AppManager");
        var monitor    = appManager.AddComponent<OmmoHardwareMonitor>();
        var devManager = appManager.AddComponent<OmmoDeviceManager>();
        var sensorMgr  = appManager.AddComponent<OmmoSensorManager>();

        // BaseStation (origem do tracking) — invisível.
        var baseStation = CreateEmpty("BaseStation");
        baseStation.transform.position = new Vector3(0f, 13f, 0f);

        // Objeto controlado (cubo) + prefab de dispositivo inativo.
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

        sensorMgr.DeviceManager   = devManager;
        sensorMgr.HardwareMonitor = monitor;

        var esqueleto = CreateEmpty("EsqueletoJogador").AddComponent<OmmoEsqueletoJogador>();

        OmmoCalibracaoManager calib = null;
        if (comCalibracao)
        {
            calib = appManager.AddComponent<OmmoCalibracaoManager>();
            calib.SensorManager    = sensorMgr;
            calib.Esqueleto        = esqueleto;
            calib.HelperCalibracao = HelperId.Jane;
        }

        return new ScaffoldRefs { SensorMgr = sensorMgr, Esqueleto = esqueleto, Calib = calib };
    }

    // ═════════════════════════════════════════════════════════════════
    // UI — Seleção
    // ═════════════════════════════════════════════════════════════════
    static MinigameSelectionUI ConstruirSelecao(Transform pai)
    {
        // Selection box (container central).
        var box = new GameObject("SelectionBox");
        box.transform.SetParent(pai, false);
        var boxRect = box.AddComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = new Vector2(0f, -30f);
        boxRect.sizeDelta        = new Vector2(1700f, 760f);
        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0f, 0f, 0f, 0.15f);

        // START / EXIT.
        var start = CriarBotaoImagem("BotaoStart", pai, UISprite("startButton"), UISprite("startButtonHover"),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(260f, 110f));
        var exit  = CriarBotaoImagem("BotaoExit", pai, UISprite("exitButton"), UISprite("exitButtonHover"),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(260f, 110f));

        // Cards — um por exercício (4).
        var tipos = new[]
        {
            ExerciciosWaypoints.TipoExercicio.FlexaoBraco,
            ExerciciosWaypoints.TipoExercicio.ElevacaoTotal,
            ExerciciosWaypoints.TipoExercicio.AbducaoLateral,
            ExerciciosWaypoints.TipoExercicio.FlexaoCotovelo,
        };
        float[] cardX = { -600f, -200f, 200f, 600f };
        var cards = new SelectionCard[tipos.Length];
        for (int i = 0; i < tipos.Length; i++)
            cards[i] = ConstruirCard(box.transform, tipos[i], new Vector2(cardX[i], 0f));

        var selGO = CreateEmpty("MinigameSelectionUI");
        var selUI = selGO.AddComponent<MinigameSelectionUI>();
        selUI.Cards      = cards;
        selUI.BotaoStart = start;
        selUI.BotaoExit  = exit;
        selUI.CenaMinijogo = "MinijogoDardos";
        return selUI;
    }

    static SelectionCard ConstruirCard(Transform pai, ExerciciosWaypoints.TipoExercicio tipo, Vector2 pos)
    {
        var card = new GameObject($"Card_{tipo}");
        card.transform.SetParent(pai, false);
        var rect = card.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta        = new Vector2(360f, 620f);
        var cardImg = card.AddComponent<Image>();
        var cardSprite = UISprite("selectionCard");
        if (cardSprite != null) cardImg.sprite = cardSprite; else cardImg.color = new Color(0.18f, 0.55f, 0.25f);
        var cardBtn = card.AddComponent<Button>();

        // Imagem do exercício (hover anima).
        var img = CriarImagem("ImagemExercicio", card.transform, null,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(320f, 250f));
        img.raycastTarget = false;
        var hover = card.AddComponent<CardAnimacaoHover>();
        hover.ImagemAlvo = img;
        hover.Sprites    = CarregarSpritesExercicio(tipo);

        // Título do jogo — 40 Poppins ExtraBold branco.
        var titulo = CriarTexto("TituloJogo", card.transform, "DARDOS", 40, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -300f), new Vector2(320f, 50f), Color.white, PoppinsExtraBold, FontStyles.Bold);
        titulo.raycastTarget = false;

        // Nome do exercício — 20 Poppins Medium #5F786C.
        var nome = CriarTexto("NomeExercicio", card.transform, ExerciciosWaypoints.Nome(tipo), 20,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -350f), new Vector2(320f, 32f), CorNomeExercicio, PoppinsMedium, FontStyles.Normal);
        nome.raycastTarget = false;

        // Linha L: - [num] +
        var repsL = ConstruirLinhaReps(card.transform, "L", new Vector2(0f, -430f), out var lMenos, out var lMais);
        // Linha R: - [num] +
        var repsR = ConstruirLinhaReps(card.transform, "R", new Vector2(0f, -510f), out var rMenos, out var rMais);

        var sc = card.AddComponent<SelectionCard>();
        sc.Tipo            = tipo;
        sc.TituloJogo      = titulo;
        sc.NomeExercicio   = nome;
        sc.ImagemExercicio = img;
        sc.BotaoSelecionar = cardBtn;
        sc.RepsLTexto      = repsL;
        sc.RepsRTexto      = repsR;
        sc.BotaoLMenos     = lMenos; sc.BotaoLMais = lMais;
        sc.BotaoRMenos     = rMenos; sc.BotaoRMais = rMais;
        return sc;
    }

    static TextMeshProUGUI ConstruirLinhaReps(Transform pai, string lado, Vector2 pos, out Button menos, out Button mais)
    {
        var linha = new GameObject($"Reps{lado}");
        linha.transform.SetParent(pai, false);
        var lRect = linha.AddComponent<RectTransform>();
        lRect.anchorMin = lRect.anchorMax = lRect.pivot = new Vector2(0.5f, 1f);
        lRect.anchoredPosition = pos;
        lRect.sizeDelta        = new Vector2(320f, 64f);

        CriarTexto($"Label{lado}", linha.transform, lado, 30, TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(10f, 0f), new Vector2(50f, 60f), CorReps, PoppinsExtraBold, FontStyles.Bold).raycastTarget = false;

        var menosGO = CriarBotaoSimples($"Menos{lado}", linha.transform, "-", new Color(0.20f, 0.20f, 0.22f),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(54f, 54f));
        menos = menosGO.GetComponent<Button>();

        var num = CriarTexto($"Num{lado}", linha.transform, "1", 38, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(80f, 60f), CorReps, PoppinsExtraBold, FontStyles.Bold);
        num.raycastTarget = false;

        var maisGO = CriarBotaoSimples($"Mais{lado}", linha.transform, "+", new Color(0.20f, 0.20f, 0.22f),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(54f, 54f));
        mais = maisGO.GetComponent<Button>();
        return num;
    }

    // ═════════════════════════════════════════════════════════════════
    // UI — Pausa
    // ═════════════════════════════════════════════════════════════════
    static GameObject ConstruirPausa(Transform pai, out PauseMenu pauseMenu)
    {
        var overlay = CriarPainel("PausaOverlay", pai);
        var cg = overlay.AddComponent<CanvasGroup>();
        CriarImagemFull("PausaBg", overlay.transform, UISprite("Background"), new Color(0f, 0f, 0f, 0.85f));

        var cont = CriarBotaoImagem("BotaoContinuar", overlay.transform, UISprite("continueButton"), UISprite("continueButtonHover"),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(420f, 110f));
        var main = CriarBotaoImagem("BotaoMainMenu", overlay.transform, UISprite("mainMenuButton"), UISprite("mainMenuButtonHover"),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(420f, 110f));
        var exitG = CriarBotaoImagem("BotaoExitGame", overlay.transform, UISprite("exitGameButton"), UISprite("exitGameButtonHover"),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(420f, 110f));

        overlay.SetActive(false);

        var pmGO = CreateEmpty("PauseMenu");
        pauseMenu = pmGO.AddComponent<PauseMenu>();
        pauseMenu.Overlay        = overlay;
        pauseMenu.OverlayGroup   = cg;
        pauseMenu.BotaoContinuar = cont;
        pauseMenu.BotaoMainMenu  = main;
        pauseMenu.BotaoExitGame  = exitG;
        return overlay;
    }

    // ═════════════════════════════════════════════════════════════════
    // UI — Diálogo dos helpers
    // ═════════════════════════════════════════════════════════════════
    static GameObject CriarPainelDialogo(string nome, Transform pai, out HelperDialogueManager dlg)
    {
        // Raiz sempre ativa (para o Update/click-advance correr); o conteúdo é que alterna.
        var root = new GameObject(nome);
        root.transform.SetParent(pai, false);
        StretchFull(root.AddComponent<RectTransform>());
        dlg = root.AddComponent<HelperDialogueManager>();

        var conteudo = CriarPainel("Conteudo", root.transform);

        // Personagem (canto inferior esquerdo).
        var helperImg = CriarImagem("Helper", conteudo.transform, null,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(520f, 760f));
        helperImg.preserveAspect = true;
        helperImg.raycastTarget  = false;

        // Balão (acima/à direita do personagem).
        var balaoImg = CriarImagem("Balao", conteudo.transform, UISprite("balãoDeFala"),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(480f, -120f), new Vector2(1200f, 420f));
        balaoImg.type = Image.Type.Sliced;
        balaoImg.raycastTarget = false;

        var texto = CriarTexto("TextoBalao", balaoImg.transform, "", 40, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            CorBalao, PoppinsMedium, FontStyles.Normal);
        var tr = texto.rectTransform;
        tr.offsetMin = new Vector2(80f, 90f);
        tr.offsetMax = new Vector2(-80f, -60f);
        texto.raycastTarget = false;

        dlg.Painel         = conteudo;   // alterna o conteúdo, não a raiz
        dlg.HelperImagem   = helperImg;
        dlg.BalaoImagem    = balaoImg;
        dlg.TextoBalao     = texto;
        dlg.SpritesJane    = SpritesHelper("Jane", NomesJane);
        dlg.SpritesPatrick = SpritesHelper("Patrick", NomesPatrick);

        return root;
    }

    // ═════════════════════════════════════════════════════════════════
    // Helpers de construção
    // ═════════════════════════════════════════════════════════════════
    static void CriarBootstrap()
    {
        var go = CreateEmpty("OmmoBootstrap");
        go.AddComponent<OmmoBootstrap>();
        go.AddComponent<SessionManager>();
        var cursor = go.AddComponent<CursorManager>();
        cursor.CursorTextura = UITexture("mouse");
    }

    static ScreenFader CriarFader()
    {
        var canvas = CriarCanvasOverlay("FadeCanvas", 100);
        var go = new GameObject("Fade");
        go.transform.SetParent(canvas.transform, false);
        StretchFull(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = Color.black;
        go.AddComponent<CanvasGroup>();
        return go.AddComponent<ScreenFader>();
    }

    static GameObject CriarPainel(string nome, Transform pai)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        StretchFull(go.AddComponent<RectTransform>());
        return go;
    }

    static Image CriarImagemFull(string nome, Transform pai, Sprite sprite, Color fallback)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        StretchFull(go.AddComponent<RectTransform>());
        var img = go.AddComponent<Image>();
        if (sprite != null) { img.sprite = sprite; img.color = Color.white; img.preserveAspect = false; }
        else img.color = fallback;
        return img;
    }

    static Image CriarImagem(string nome, Transform pai, Sprite sprite,
        Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = aMin; rect.anchorMax = aMax; rect.pivot = pivot;
        rect.anchoredPosition = pos; rect.sizeDelta = size;
        var img = go.AddComponent<Image>();
        if (sprite != null) img.sprite = sprite; else img.color = new Color(1f, 1f, 1f, 0.001f);
        return img;
    }

    static Button CriarBotaoImagem(string nome, Transform pai, Sprite normal, Sprite hover,
        Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var img = CriarImagem(nome, pai, normal, aMin, aMax, pivot, pos, size);
        img.preserveAspect = true;
        var btn = img.gameObject.AddComponent<Button>();
        if (hover != null)
        {
            btn.transition = Selectable.Transition.SpriteSwap;
            var ss = btn.spriteState;
            ss.highlightedSprite = hover;
            ss.pressedSprite     = hover;
            btn.spriteState = ss;
        }
        return btn;
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
        Vector2 pos, Vector2 size, Color cor, TMP_FontAsset fonte = null, FontStyles estilo = FontStyles.Normal)
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
        tmp.fontStyle = estilo;
        if (fonte != null) tmp.font = fonte;
        return tmp;
    }

    static GameObject CriarBotaoSimples(string nome, Transform pai, string label, Color cor,
        Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go   = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = aMin; rect.anchorMax = aMax; rect.pivot = pivot;
        rect.anchoredPosition = pos; rect.sizeDelta = size;
        go.AddComponent<Image>().color = cor;
        go.AddComponent<Button>();

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lblRect = lblGO.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero; lblRect.anchorMax = Vector2.one; lblRect.sizeDelta = Vector2.zero;
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 28; tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return go;
    }

    static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    static void GarantirEventSystem()
    {
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = CreateEmpty("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    static GameObject CreateEmpty(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    static GameObject CriarVisualSensor()
    {
        var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubo.name = "CuboSensor";
        cubo.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        Object.DestroyImmediate(cubo.GetComponent<BoxCollider>());
        cubo.GetComponent<MeshRenderer>().sharedMaterial = CriarMaterialRimGlow(new Color(0.95f, 0.95f, 0.95f));
        return cubo;
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

    // ── Assets ────────────────────────────────────────────────────────
    static Sprite UISprite(string nome) => CarregarSpriteAsset($"{PastaUI}/{nome}.png");

    static Texture2D UITexture(string nome) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaUI}/{nome}.png");

    static Sprite[] SpritesHelper(string subpasta, string[] nomes)
    {
        var arr = new Sprite[nomes.Length];
        for (int i = 0; i < nomes.Length; i++)
            arr[i] = CarregarSpriteAsset($"{PastaUI}/{subpasta}/{nomes[i]}.png");
        return arr;
    }

    static GameObject CarregarPrefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

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

    /// <summary>Sprites de exemplo do exercício (pasta UIAssets/Exercises — vazia até haver animações).</summary>
    static Sprite[] CarregarSpritesExercicio(ExerciciosWaypoints.TipoExercicio tipo)
    {
        var lista = new List<Sprite>();
        string pasta = $"{PastaUI}/Exercises/{tipo}";
        for (int s = 1; s <= 5; s++)
        {
            var sp = CarregarSpriteAsset($"{pasta}/{s}.png");
            if (sp != null) lista.Add(sp);
        }
        return lista.ToArray();
    }

    static TMP_FontAsset PoppinsExtraBold => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{PastaFontes}/Poppins-ExtraBold SDF.asset");
    static TMP_FontAsset PoppinsMedium    => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{PastaFontes}/Poppins-Medium SDF.asset");

    // ── Sequências de diálogo (assets editáveis) ──────────────────────
    static HelperFala F(HelperId q, HelperEmocao e, string t)
        => new HelperFala { Quem = q, Emocao = e, Texto = t };

    static HelperFala[] FalasTutorialDefault()
    {
        var h = HelperId.Patrick; // o outro helper (Jane calibra por defeito)
        return new[]
        {
            F(h, HelperEmocao.Pleased,   "Boa! Agora escolhe os teus minijogos aqui."),
            F(h, HelperEmocao.Neutral,   "Passa o rato por cima de um card e ele mostra-te o exercício."),
            F(h, HelperEmocao.Neutral,   "Nos botões L e R escolhes as repetições para o braço esquerdo e direito."),
            F(h, HelperEmocao.Impressed, "Podes escolher vários exercícios ao mesmo tempo!"),
            F(h, HelperEmocao.Laugh,     "O nome do jogo e do exercício aparecem em cada card."),
            F(h, HelperEmocao.Pleased,   "Quando estiveres pronto, carrega em START. Vamos a isto!"),
        };
    }

    static HelperFala[] FalasScoreDefault(HelperEmocao emocao, string txt)
    {
        var h = HelperId.Jane;
        return new[]
        {
            F(h, HelperEmocao.Neutral, "Vamos ver como te saíste..."),
            F(h, emocao, txt),
            F(h, HelperEmocao.Pleased, "Clica para voltar ao menu."),
        };
    }

    static DialogueSequence CriarSequenciaDialogo(string nome, HelperFala[] falas)
    {
        const string dir = "Assets/Prefabs/PrevenGameAssets/Dialogos";
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        string path = $"{dir}/{nome}.asset";

        var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
        if (seq != null) return seq; // já existe — preserva as edições do utilizador

        seq = ScriptableObject.CreateInstance<DialogueSequence>();
        seq.Falas = new List<HelperFala>(falas);
        AssetDatabase.CreateAsset(seq, path);
        AssetDatabase.SaveAssets();
        return seq;
    }
}
