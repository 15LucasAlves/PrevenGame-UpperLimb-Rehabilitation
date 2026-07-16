using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// OmmoSceneBuilder — Constrói a cena Menu do PrevenGame (Gamification) por código.
///
/// Menu: Ommo → PrevenGame → Build Cena (Menu).
///   • Menu (hub): Splash → Calibração (helpers) → Seleção → [Score], via GameFlowManager.
///
/// A cena MinijogoDardos.unity é construída e mantida MANUALMENTE no editor — o builder
/// não lhe toca (apenas a regista no Build Settings se existir).
///
/// Assets reais em Assets/Prefabs/PrevenGameAssets/UIAssets. Se um asset faltar, usa-se placeholder.
/// </summary>
public class OmmoSceneBuilder
{
    // ── Caminhos de assets ────────────────────────────────────────────
    const string PastaUI     = "Assets/Prefabs/PrevenGameAssets/UIAssets";
    const string PastaSons   = "Assets/Prefabs/PrevenGameAssets/Sounds and SFX";
    const string PastaFontes = "Assets/Fonts";

    const string CenaMenu     = "Assets/Scenes/Menu.unity";
    const string CenaMinijogo = "Assets/Scenes/MinijogoDardos.unity";

    // Emoções por índice (HelperEmocao): Neutral, Pleased, Impressed, Laugh, Surprised, Worried, Disappointed
    static readonly string[] NomesJane =
        { "JaneNeutral", "JanePleased", "JaneImpressed", "JaneLaugh", "JaneSurprised", "JaneWorried", "JaneDisappointed" };
    static readonly string[] NomesPatrick =
        { "PatrickNeutral", "PatrickPleased", "PatrickImpressed", "PatrickLaugh", "PatrickSurprised", "PatrickWorried", "PatrickDisappointed" };

    // Cores dos textos dos cards
    static readonly Color CorNomeExercicio = new Color32(95, 120, 108, 255);  // #5F786C
    static readonly Color CorReps          = new Color32(132, 164, 149, 255); // #84A495
    static readonly Color CorBalao         = new Color32(35, 31, 32, 255);    // #231F20

    // ─────────────────────────────────────────────────────────────────
    [MenuItem("Ommo/PrevenGame/Build Cena (Menu)")]
    public static void BuildCenas()
    {
        if (!EditorUtility.DisplayDialog("PrevenGame — Build Cena",
            "Cria/grava Assets/Scenes/Menu.unity (splash+calibração+seleção+score).\n\n" +
            "A cena MinijogoDardos.unity NÃO é tocada — é construída/mantida manualmente.\n\nContinuar?",
            "Sim", "Cancelar"))
            return;

        if (!System.IO.Directory.Exists("Assets/Scenes")) System.IO.Directory.CreateDirectory("Assets/Scenes");

        // Sprites das demos de exercício em Resources (o HudVR carrega-os em runtime).
        CopiarSpritesExerciciosParaResources();

        var sMenu = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        BuildMenuHub();
        EditorSceneManager.SaveScene(sMenu, CenaMenu);

        // Regista o Menu e, se existir em disco, a cena do minijogo (mantida à mão).
        var cenas = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(CenaMenu, true) };
        if (System.IO.File.Exists(CenaMinijogo))
            cenas.Add(new EditorBuildSettingsScene(CenaMinijogo, true));
        EditorBuildSettings.scenes = cenas.ToArray();

        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(CenaMenu);

        EditorUtility.DisplayDialog("PrevenGame — Build Cena",
            "✅ Cena Menu criada e registada no Build Settings.", "OK");
    }

    // ═════════════════════════════════════════════════════════════════
    // CENA MENU (HUB)
    // ═════════════════════════════════════════════════════════════════
    public static void BuildMenuHub()
    {
        GarantirEventSystem();
        CriarBootstrap();

        var scaffold = ConstruirScaffoldOmmo();

        // Câmara desktop — desenha SÓ na janela do monitor (fundo dos canvases Overlay).
        // Em VR o rig OVR é criado em runtime pelo GestorXR; esta câmara não vê o 3D.
        Camera cam = Camera.main ?? Object.FindObjectOfType<Camera>();
        if (cam != null)
        {
            cam.gameObject.tag  = "Untagged"; // a tag MainCamera pertence ao CenterEyeAnchor do rig VR
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            if (cam.gameObject.GetComponent<CamaraDesktop>() == null)
                cam.gameObject.AddComponent<CamaraDesktop>();
        }

        // MRUK (deteção de QR para o alinhamento Ommo↔VR).
        InstanciarMRUK();

        // EcraVR — ecrã world-space do paciente (diálogo em VR na calibração).
        var ecraVr = ConstruirEcraVR();
        if (scaffold.Calib != null) scaffold.Calib.EcraVr = ecraVr;

        // Canvas principal dos menus (overlay).
        var canvas = CriarCanvasOverlay("MenuCanvas", 40);

        // ── Painel Splash (firstMenu) ─────────────────────────────────
        var splash = CriarPainel("SplashPainel", canvas.transform);
        CriarImagemFull("SplashBg", splash.transform, UISprite("firstMenu"), new Color(0.06f, 0.06f, 0.09f, 1f));

        // ── Painel Calibração (só fundo; instruções via diálogo) ──────
        var calib = CriarPainel("CalibracaoPainel", canvas.transform);
        CriarImagemFull("CalibBg", calib.transform, UISprite("Background"), new Color(0.09f, 0.12f, 0.10f, 1f));

        // ── Painel Seleção (camadas: bottom → selection box/cards → top → botões) ─
        var selecao = CriarPainel("SelecaoPainel", canvas.transform);
        CriarImagemFull("SelecaoBgBottom", selecao.transform, UISprite("mainMenuBackgroundBottom"), new Color(0.09f, 0.12f, 0.10f, 1f));
        var selUI = ConstruirSelecao(selecao.transform);
        selUI.GrupoInteracao = selecao.AddComponent<CanvasGroup>(); // bloqueado durante o tutorial

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

        // Sequências de diálogo editáveis (assets). Não sobrescreve se já existirem.
        if (scaffold.Calib != null)
            scaffold.Calib.IntroSeq = CriarSequenciaDialogo("IntroCalibracao", FalasIntroCalibracaoDefault());
        gfm.TutorialSelecaoSeq = CriarSequenciaDialogo("TutorialSelecao", FalasTutorialDefault());
        gfm.ScoreAltoSeq  = CriarSequenciaDialogo("ScoreAlto",  FalasScoreDefault(HelperEmocao.Impressed, "Excelente trabalho! Estás mesmo a melhorar."));
        gfm.ScoreMedioSeq = CriarSequenciaDialogo("ScoreMedio", FalasScoreDefault(HelperEmocao.Pleased,   "Bom trabalho! Continua assim."));
        gfm.ScoreBaixoSeq = CriarSequenciaDialogo("ScoreBaixo", FalasScoreDefault(HelperEmocao.Worried,   "Não faz mal — cada sessão conta. Vamos tentar outra vez!"));

        Debug.Log("[OmmoBuilder] ✅ Cena Menu construída.");
    }

    // ═════════════════════════════════════════════════════════════════
    // Scaffold Ommo partilhado
    // ═════════════════════════════════════════════════════════════════
    class ScaffoldRefs
    {
        public OmmoSensorManager     SensorMgr;
        public OmmoCalibracaoManager Calib;
    }

    static ScaffoldRefs ConstruirScaffoldOmmo(bool comCalibracao = true)
    {
        var appManager  = CreateEmpty("AppManager");
        var monitor     = appManager.AddComponent<OmmoHardwareMonitor>();
        var devManager  = appManager.AddComponent<OmmoDeviceManager>();
        var sensorMgr   = appManager.AddComponent<OmmoSensorManager>();
        var autoPairing = appManager.AddComponent<OmmoAutoPairing>();

        // BaseStation (origem do tracking). Pose por defeito AFASTADA do ponto de
        // arranque do jogador (senão os visuais nascem "em cima" da cabeça) — a
        // pose real vem do QR code via AlinhadorOmmoQr assim que o VR alinha.
        // Yaw 90°: convenção de eixos do Ommo com o jogador de frente para a base.
        var baseStation = CreateEmpty("BaseStation");
        baseStation.transform.SetPositionAndRotation(
            new Vector3(0f, 0.85f, 1.5f), Quaternion.Euler(0f, 90f, 0f));

        // Alinhador Ommo↔VR: ancora o BaseStation na pose do QR (ou F2 = manual).
        var alinhador = appManager.AddComponent<AlinhadorOmmoQr>();
        alinhador.OmmoRoot = baseStation.transform;

        // Objeto controlado (cubo) + prefab de dispositivo inativo.
        var cubo = CriarVisualSensor();
        var trackedRoot = CreateEmpty("TrackedDevicePrefab_TEMP");
        var ommoDevice  = trackedRoot.AddComponent<OmmoDevice>();
        ommoDevice.SensorPrefab  = cubo;
        ommoDevice.RequestedMode = Ommo.DeviceFusionMode.FullFusion;
        ommoDevice.DebugIntervalSegundos = 0f; // sem spam de posições na consola
        cubo.transform.SetParent(trackedRoot.transform, false);
        cubo.transform.localPosition = Vector3.zero;
        trackedRoot.SetActive(false);

        devManager.BaseStation    = baseStation;
        devManager.UnityScaleInCM = 100f; // 1 unidade Unity = 1 m (obrigatório para VR/MRUK)
        devManager.DeviceTypePrefabs = new OmmoDeviceManager.DeviceTypePrefab[]
        {
            new OmmoDeviceManager.DeviceTypePrefab { DeviceType = 0, Prefab = trackedRoot }
        };

        sensorMgr.DeviceManager   = devManager;
        sensorMgr.HardwareMonitor = monitor;
        sensorMgr.AutoPairing     = autoPairing;

        // O jogador nas cenas: só a mão (pose do sensor) + estimador de corpo (câmara VR).
        // O antigo esqueleto visível (OmmoEsqueletoJogador) foi aposentado.
        CreateEmpty("MaoJogador").AddComponent<MaoJogador>();
        CreateEmpty("RastreadorCorpo").AddComponent<RastreadorCorpoJogador>();

        OmmoCalibracaoManager calib = null;
        if (comCalibracao)
        {
            calib = appManager.AddComponent<OmmoCalibracaoManager>();
            calib.SensorManager = sensorMgr;
            var pressao         = appManager.AddComponent<EntradaPressao>();
            pressao.FiltroNome  = "GRASP"; // o sensor de pressão anuncia-se como GRASP_x.y.z
            pressao.LogValores  = false;   // sem spam de leituras na consola
            calib.Pressao       = pressao;
            calib.AutoPairing   = autoPairing;
        }

        return new ScaffoldRefs { SensorMgr = sensorMgr, Calib = calib };
    }

    // ═════════════════════════════════════════════════════════════════
    // Scaffold VR para a cena de minijogo (mantida manualmente)
    // ═════════════════════════════════════════════════════════════════
    /// <summary>
    /// Adiciona à CENA ABERTA tudo o que uma cena de minijogo precisa do lado
    /// PrevenGame/Ommo/VR: bootstrap, scaffold Ommo (sem calibração), MRUK,
    /// EcraVR, EntradaPressao, GestorMinijogo e o MinijogoTesteWaypoints (trocar
    /// depois pela implementação real de MinijogoBase, ex.: dardos).
    /// O mundo 3D (sala, luz, alvo...) continua a ser feito à mão no editor.
    /// </summary>
    [MenuItem("Ommo/PrevenGame/Adicionar Scaffold VR ao Minijogo (cena atual)")]
    public static void AdicionarScaffoldMinijogo()
    {
        if (Object.FindObjectOfType<GestorMinijogo>() != null)
        {
            EditorUtility.DisplayDialog("PrevenGame", "A cena já tem um GestorMinijogo.", "OK");
            return;
        }

        GarantirEventSystem();
        CriarBootstrap();
        var scaffold = ConstruirScaffoldOmmo(comCalibracao: false);
        InstanciarMRUK();
        var ecra = ConstruirEcraVR();

        var pressao = new GameObject("EntradaPressao").AddComponent<EntradaPressao>();
        pressao.FiltroNome = "GRASP";
        pressao.LogValores = false; // sem spam de leituras na consola

        var gestorGO = CreateEmpty("GestorMinijogo");
        var gestor   = gestorGO.AddComponent<GestorMinijogo>();
        gestor.Ecra          = ecra;
        gestor.SensorManager = scaffold.SensorMgr;
        gestor.Pressao       = pressao;
        gestor.Minijogo      = CreateEmpty("MinijogoTeste").AddComponent<MinijogoTesteWaypoints>();

        CopiarSpritesExerciciosParaResources();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[OmmoBuilder] ✅ Scaffold VR do minijogo adicionado à cena atual.");
    }

    /// <summary>
    /// Copia os PNG das demos (UIAssets/Exercises/&lt;prefixo&gt;_1..5.png) para
    /// Assets/Resources/Exercises/&lt;Tipo&gt;/&lt;n&gt;.png — o HudVR carrega-os em runtime
    /// (as cenas de minijogo não passam pelo builder). O destino usa o nome do enum
    /// (ASCII) e índices numéricos, independente dos nomes de export do artista.
    /// Re-copia quando a origem é mais recente (re-exports).
    /// </summary>
    static void CopiarSpritesExerciciosParaResources()
    {
        foreach (ExerciciosWaypoints.TipoExercicio tipo in
                 System.Enum.GetValues(typeof(ExerciciosWaypoints.TipoExercicio)))
        {
            string prefixo    = PrefixoImagensExercicio(tipo);
            string dirDestino = $"Assets/Resources/Exercises/{tipo}";
            for (int s = 1; s <= 5; s++)
            {
                string origem  = $"{PastaUI}/Exercises/{prefixo}_{s}.png";
                string destino = $"{dirDestino}/{s}.png";
                if (!System.IO.File.Exists(origem)) continue;

                CarregarSpriteAsset(origem); // normaliza import settings na origem

                if (System.IO.File.Exists(destino))
                {
                    // Só re-copia se o export de origem for mais recente.
                    if (System.IO.File.GetLastWriteTimeUtc(origem) <=
                        System.IO.File.GetLastWriteTimeUtc(destino)) continue;
                    AssetDatabase.DeleteAsset(destino);
                }

                if (!System.IO.Directory.Exists(dirDestino))
                    System.IO.Directory.CreateDirectory(dirDestino);
                AssetDatabase.Refresh();
                AssetDatabase.CopyAsset(origem, destino);
                CarregarSpriteAsset(destino); // mesmos import settings na cópia
            }
        }
        AssetDatabase.SaveAssets();
    }

    // ═════════════════════════════════════════════════════════════════
    // EcraVR — ecrã world-space do paciente (diálogo, tabela de score, pausa)
    // ═════════════════════════════════════════════════════════════════
    static EcraVR ConstruirEcraVR()
    {
        var root = CreateEmpty("EcraVR");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta  = new Vector2(1920f, 1080f);
        rt.localScale = Vector3.one * 0.001f; // 1920 px → 1.92 m de largura

        var ecra = root.AddComponent<EcraVR>();
        // Cobertura larga do viewport (o conteúdo é encostado aos cantos abaixo).
        ecra.EscalaConteudo = 1f;
        ecra.Distancia     = 1.2f;

        // Diálogo dos helpers — instância própria (mesmo layout do diálogo do monitor).
        CriarPainelDialogo("DialogoVR", root.transform, out var dlgVr);
        ecra.Dialogo = dlgVr;

        // Ajustes VR (menos clutter): só o orador visível, helpers mais pequenos
        // ENCOSTADOS aos cantos inferiores, balão pequeno ancorado ao orador.
        AjustarDialogoParaVR(dlgVr, escalaHelpers: 0.7f, escalaBalao: 0.55f);

        // Tabela de score do minijogo (preenchida pelo GestorMinijogo).
        var painelTabela = CriarPainel("PainelTabela", root.transform);
        CriarImagemFull("TabelaBg", painelTabela.transform, null, new Color(0f, 0f, 0f, 0.75f));
        var textoTabela = CriarTexto("TextoTabela", painelTabela.transform, "", 56, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1400f, 800f), Color.white, PoppinsExtraBold, FontStyles.Bold);
        painelTabela.SetActive(false);
        ecra.PainelTabela = painelTabela;
        ecra.TextoTabela  = textoTabela;

        // Aviso de pausa (espelho do PauseMenu do operador).
        var painelPausa = CriarPainel("PainelPausa", root.transform);
        CriarImagemFull("PausaBg", painelPausa.transform, null, new Color(0f, 0f, 0f, 0.75f));
        CriarTexto("TextoPausa", painelPausa.transform, "Jogo em pausa", 96, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1400f, 300f), Color.white, PoppinsExtraBold, FontStyles.Bold);
        painelPausa.SetActive(false);
        ecra.PainelPausa = painelPausa;

        root.SetActive(false); // mostrado pelo fluxo (calibração/minijogo)
        return ecra;
    }

    /// <summary>
    /// Reduz o clutter do diálogo na instância VR: só o ORADOR fica visível,
    /// personagens mais pequenos colados aos cantos inferiores, e o balão
    /// (com textos proporcionais) pequeno e ANCORADO ao personagem que fala.
    /// Não toca na instância do monitor (os modos são opt-in por instância).
    /// </summary>
    static void AjustarDialogoParaVR(HelperDialogueManager dlg, float escalaHelpers, float escalaBalao)
    {
        dlg.MostrarApenasOrador = true;
        dlg.BalaoJuntoAoOrador  = true;

        // Personagens: escala menor + encostados aos cantos (âncoras já são os cantos inferiores).
        if (dlg.ImagemPatrick != null)
        {
            dlg.ImagemPatrick.rectTransform.localScale       = Vector3.one * escalaHelpers;
            dlg.ImagemPatrick.rectTransform.anchoredPosition = new Vector2(15f, 5f);
        }
        if (dlg.ImagemJane != null)
        {
            dlg.ImagemJane.rectTransform.localScale       = Vector3.one * escalaHelpers;
            dlg.ImagemJane.rectTransform.anchoredPosition = new Vector2(-15f, 5f);
        }

        // Balão: encolher o rect (o manager reescreve localScale no flip, por isso
        // a escala não serve) e os textos proporcionalmente.
        if (dlg.BalaoImagem != null)
            dlg.BalaoImagem.rectTransform.sizeDelta *= escalaBalao;
        EncolherTextoBalao(dlg.TextoBalao,    escalaBalao);
        EncolherTextoBalao(dlg.TextoBalaoSub, escalaBalao);

        // Offsets do balão a partir do canto inferior do orador (BalaoJuntoAoOrador):
        // assenta logo acima da cabeça do personagem (personagem ~532 px alto a 0.7).
        dlg.PosBalaoPatrick = new Vector2(40f, 545f);
        dlg.PosBalaoJane    = new Vector2(-40f, 545f);
    }

    static void EncolherTextoBalao(TextMeshProUGUI texto, float f)
    {
        if (texto == null) return;
        texto.fontSize *= f;
        var rt = texto.rectTransform;
        rt.offsetMin *= f;
        rt.offsetMax *= f;
    }

    // ═════════════════════════════════════════════════════════════════
    // UI — Seleção
    // ═════════════════════════════════════════════════════════════════
    static MinigameSelectionUI ConstruirSelecao(Transform pai)
    {
        // Selection box — container SEM visual próprio, alinhado com o painel claro
        // desenhado no mainMenuBackground (x 90..1830, y 280..1055 @1920×1080).
        // Contém um ScrollRect vertical: os cards têm tamanho fixo numa grelha de
        // 4 colunas (esquerda→direita, linhas para baixo) e a área faz scroll
        // quando houver mais cards do que cabem.
        var box = new GameObject("SelectionBox");
        box.transform.SetParent(pai, false);
        var boxRect = box.AddComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = new Vector2(0f, -132f);
        boxRect.sizeDelta        = new Vector2(1740f, 820f);
        var scroll = box.AddComponent<ScrollRect>();

        // Viewport: máscara de recorte + Image invisível para a roda do rato
        // funcionar em qualquer ponto da área (mesmo nos espaços entre cards).
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(box.transform, false);
        var vpRect = viewport.AddComponent<RectTransform>();
        StretchFull(vpRect);
        viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        viewport.AddComponent<RectMask2D>();

        // Content: grelha 4×N. Com UpperLeft, uma última linha incompleta fica
        // alinhada à esquerda, deixando o espaço livre à direita.
        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var ctRect = content.AddComponent<RectTransform>();
        ctRect.anchorMin = new Vector2(0f, 1f);
        ctRect.anchorMax = new Vector2(1f, 1f);
        ctRect.pivot     = new Vector2(0.5f, 1f);
        ctRect.offsetMin = Vector2.zero;
        ctRect.offsetMax = Vector2.zero;

        var grelha = content.AddComponent<GridLayoutGroup>();
        grelha.cellSize        = new Vector2(420f, 635f);   // = tamanho fixo dos cards
        grelha.spacing         = new Vector2(20f, 20f);
        grelha.padding         = new RectOffset(0, 0, 70, 70);
        grelha.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        grelha.startAxis       = GridLayoutGroup.Axis.Horizontal;
        grelha.childAlignment  = TextAnchor.UpperLeft;
        grelha.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grelha.constraintCount = 4;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content           = ctRect;
        scroll.viewport          = vpRect;
        scroll.horizontal        = false;
        scroll.vertical          = true;
        scroll.movementType      = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        // Cards — um por exercício (4).
        var tipos = new[]
        {
            ExerciciosWaypoints.TipoExercicio.FlexaoBraco,
            ExerciciosWaypoints.TipoExercicio.ElevacaoTotal,
            ExerciciosWaypoints.TipoExercicio.AbducaoLateral,
            ExerciciosWaypoints.TipoExercicio.FlexaoCotovelo,
        };
        var cards = new SelectionCard[tipos.Length];
        for (int i = 0; i < tipos.Length; i++)
            cards[i] = ConstruirCard(content.transform, tipos[i]);

        // Camada TOP do fundo — desenhada DEPOIS dos cards para o scroll deslizar
        // "por dentro" da moldura (integração seamless). Sem raycast para não
        // bloquear os cliques nos cards/scroll por baixo.
        var topo = CriarImagemFull("SelecaoBgTop", pai, UISprite("mainMenuBackgroundTop"), new Color(0f, 0f, 0f, 0f));
        topo.raycastTarget = false;

        // START / EXIT — por cima da camada top para continuarem clicáveis.
        var start = CriarBotaoImagem("BotaoStart", pai, UISprite("startButton"), UISprite("startButtonHover"),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-110f, -75f), new Vector2(260f, 110f));
        start.transform.localScale = Vector3.one * 1.4f;
        var exit  = CriarBotaoImagem("BotaoExit", pai, UISprite("exitButton"), UISprite("exitButtonHover"),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -75f), new Vector2(260f, 110f));
        exit.transform.localScale = Vector3.one * 1.4f;

        var selGO = CreateEmpty("MinigameSelectionUI");
        var selUI = selGO.AddComponent<MinigameSelectionUI>();
        selUI.Cards      = cards;
        selUI.BotaoStart = start;
        selUI.BotaoExit  = exit;
        selUI.CenaMinijogo = "MinijogoDardos";
        return selUI;
    }

    static SelectionCard ConstruirCard(Transform pai, ExerciciosWaypoints.TipoExercicio tipo)
    {
        // O tamanho/posição reais vêm do GridLayoutGroup do Content (cellSize 420×635);
        // as zonas internas abaixo estão calibradas para esse tamanho (rácio da arte 423×640).
        var card = new GameObject($"Card_{tipo}");
        card.transform.SetParent(pai, false);
        var rect = card.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(420f, 635f);
        var cardImg = card.AddComponent<Image>();
        var cardSprite = UISprite("selectionCard");
        if (cardSprite != null) cardImg.sprite = cardSprite; else cardImg.color = new Color(0.18f, 0.55f, 0.25f);
        var cardBtn = card.AddComponent<Button>();

        // Imagem do exercício — dentro da moldura desenhada (10%–50% da altura).
        // As imagens do artista são ~quadradas (584×573) e já trazem a borda que
        // combina com o card — preserveAspect evita distorção dentro da zona.
        var img = CriarImagem("ImagemExercicio", card.transform, null,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(330f, 240f));
        img.raycastTarget  = false;
        img.preserveAspect = true;
        var hover = card.AddComponent<CardAnimacaoHover>();
        hover.ImagemAlvo = img;
        hover.Sprites    = CarregarSpritesExercicio(tipo);

        // Título do jogo — na faixa desenhada (~53%–60% da altura).
        var titulo = CriarTexto("TituloJogo", card.transform, "DARDOS", 40, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -337f), new Vector2(330f, 48f), Color.white, PoppinsExtraBold, FontStyles.Bold);
        titulo.raycastTarget = false;

        // Nome do exercício — logo abaixo da faixa do título.
        var nome = CriarTexto("NomeExercicio", card.transform, ExerciciosWaypoints.Nome(tipo), 20,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -394f), new Vector2(330f, 32f), CorNomeExercicio, PoppinsMedium, FontStyles.Normal);
        nome.raycastTarget = false;

        // Linhas de reps — zonas de clique invisíveis sobre os − / + desenhados
        // (a arte já inclui as letras L e R e os círculos dos botões).
        var repsL = ConstruirLinhaReps(card.transform, "L", -466f, out var lMenos, out var lMais);
        var repsR = ConstruirLinhaReps(card.transform, "R", -541f, out var rMenos, out var rMais);

        var sc = card.AddComponent<SelectionCard>();
        sc.Tipo             = tipo;
        sc.TituloJogo       = titulo;
        sc.NomeExercicio    = nome;
        sc.ImagemExercicio  = img;
        sc.BotaoSelecionar  = cardBtn;
        sc.FundoCard        = cardImg;
        sc.SpriteNormal     = cardSprite;
        sc.SpriteSelecionado = UISprite("selectionCardSelected");
        sc.RepsLTexto      = repsL;
        sc.RepsRTexto      = repsR;
        sc.BotaoLMenos     = lMenos; sc.BotaoLMais = lMais;
        sc.BotaoRMenos     = rMenos; sc.BotaoRMais = rMais;
        return sc;
    }

    /// <summary>
    /// Linha de reps: a arte do card já desenha a letra L/R e os círculos − e +
    /// (centros a ~33% e ~77% da largura), por isso só criamos zonas de clique
    /// invisíveis por cima deles e o número no espaço entre os dois.
    /// yCentro = centro vertical da linha em coordenadas do card (a partir do topo).
    /// </summary>
    static TextMeshProUGUI ConstruirLinhaReps(Transform pai, string lado, float yCentro, out Button menos, out Button mais)
    {
        menos = CriarBotaoInvisivel($"Menos{lado}", pai, new Vector2(-76f, yCentro), new Vector2(70f, 68f));
        mais  = CriarBotaoInvisivel($"Mais{lado}",  pai, new Vector2(130f, yCentro), new Vector2(70f, 68f));

        var num = CriarTexto($"Num{lado}", pai, "1", 38, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(19f, yCentro), new Vector2(80f, 60f), CorReps, PoppinsExtraBold, FontStyles.Bold);
        num.raycastTarget = false;
        return num;
    }

    /// <summary>Zona de clique invisível (Image transparente mas com raycast) sobre arte desenhada.</summary>
    static Button CriarBotaoInvisivel(string nome, Transform pai, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta        = size;
        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f); // invisível; mantém o raycast
        return go.AddComponent<Button>();
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

        // Patrick (canto inferior esquerdo) e Jane (canto inferior direito) — sempre os dois.
        var patrickImg = CriarImagem("HelperPatrick", conteudo.transform, null,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, -15f), new Vector2(520f, 760f));
        patrickImg.preserveAspect = true;
        patrickImg.raycastTarget  = false;
        patrickImg.rectTransform.localScale = Vector3.one * 1.15f;

        var janeImg = CriarImagem("HelperJane", conteudo.transform, null,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, -15f), new Vector2(520f, 760f));
        janeImg.preserveAspect = true;
        janeImg.raycastTarget  = false;
        janeImg.rectTransform.localScale = Vector3.one * 1.15f;

        // Balão (topo-centro; o manager posiciona-o e espelha-o consoante o orador).
        var balaoImg = CriarImagem("Balao", conteudo.transform,
            CarregarSpriteAsset($"{PastaUI}/balãoDeFala.png", 4096),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(120f, -120f), new Vector2(1200f, 420f));
        balaoImg.type = Image.Type.Simple;
        balaoImg.raycastTarget = false;

        // Na arte do balão, o corpo da bolha ocupa ~4%–74% da altura (o resto é o bico),
        // por isso os textos centram-se nessa zona e não no centro do rect.
        var texto = CriarTexto("TextoBalao", balaoImg.transform, "", 40, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            CorBalao, PoppinsMedium, FontStyles.Normal);
        var tr = texto.rectTransform;
        tr.offsetMin = new Vector2(90f, 155f);   // fundo: acima do bico e da faixa do subtexto
        tr.offsetMax = new Vector2(-90f, -45f);  // topo: dentro do contorno da bolha
        texto.raycastTarget = false;

        // Linha secundária (ex.: dica da calibração) — caixa própria, sem máquina de escrever.
        var textoSub = CriarTexto("TextoBalaoSub", balaoImg.transform, "", 26, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            new Color(CorBalao.r, CorBalao.g, CorBalao.b, 0.75f), PoppinsMedium, FontStyles.Normal);
        var trSub = textoSub.rectTransform;
        trSub.offsetMin = new Vector2(90f, 115f);
        trSub.offsetMax = new Vector2(-90f, -265f);
        textoSub.raycastTarget = false;
        textoSub.gameObject.SetActive(false);    // só aparece quando há subtexto

        dlg.Painel         = conteudo;   // alterna o conteúdo, não a raiz
        dlg.ImagemPatrick  = patrickImg;
        dlg.ImagemJane     = janeImg;
        dlg.BalaoImagem    = balaoImg;
        dlg.TextoBalao     = texto;
        dlg.TextoBalaoSub  = textoSub;
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
        go.AddComponent<GestorXR>(); // ciclo de vida XR + troca monitor↔VR (persistente)
        ConfigurarGestorAudio(go.AddComponent<GestorAudio>());
        var cursor = go.AddComponent<CursorManager>();
        cursor.CursorTextura = UITexture("mouse");

        // Sensor de pressão BLE — GameObject próprio: o Awake do BLEManager destrói
        // o GO duplicado ao voltar à cena, por isso não pode partilhar o OmmoBootstrap.
        CreateEmpty("BLEManager").AddComponent<BLEManager>();
    }

    /// <summary>
    /// Preenche o catálogo do GestorAudio a partir da pasta Sounds and SFX.
    /// A música de fundo do hub é opcional: procura "backgroundMusic"/"musicaFundo"/
    /// "hubMusic" (.mp3/.ogg/.wav) e avisa se ainda não existir.
    /// </summary>
    static void ConfigurarGestorAudio(GestorAudio audio)
    {
        audio.SfxScoreAlto           = Clip("amazingScoreSFX");
        audio.SfxScoreMedio          = Clip("goodScoreSFX");
        audio.SfxScoreBaixo          = Clip("badScoreSFX");
        audio.SfxBracoConcluido      = Clip("completedArm");
        audio.SfxExercicioConcluido  = Clip("completedExercise");
        audio.SfxRufo                = Clip("drumRoll");
        audio.SfxDialogoCurtoJane    = Clip("shortDialogue_Jane");
        audio.SfxDialogoLongoJane    = Clip("longDialogue_Jane");
        audio.SfxDialogoCurtoPatrick = Clip("shortDialogue_Patrick");
        audio.SfxDialogoLongoPatrick = Clip("longDialogue_Patrick");
        audio.SfxLancamentoDardo     = Clip("Darts/dartThrow");
        audio.AmbienteBarDardos      = Clip("Darts/barAmbience");

        // Música de fundo do hub (a adicionar pelo utilizador — nomes candidatos).
        audio.MusicaHub = Clip("backgroundMusic", avisarSeFaltar: false)
                       ?? Clip("musicaFundo",     avisarSeFaltar: false)
                       ?? Clip("hubMusic",        avisarSeFaltar: false);
        if (audio.MusicaHub == null)
            Debug.Log($"[OmmoBuilder] Sem música de fundo do hub — coloca um \"backgroundMusic.mp3\" " +
                      $"em {PastaSons} e refaz o Build (ou atribui no Inspector do GestorAudio).");
    }

    /// <summary>Carrega um AudioClip da pasta Sounds and SFX (tenta .mp3, .ogg e .wav).</summary>
    static AudioClip Clip(string nome, bool avisarSeFaltar = true)
    {
        foreach (var ext in new[] { "mp3", "ogg", "wav" })
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>($"{PastaSons}/{nome}.{ext}");
            if (c != null) return c;
        }
        if (avisarSeFaltar)
            Debug.LogWarning($"[OmmoBuilder] AudioClip em falta: {PastaSons}/{nome}.(mp3|ogg|wav)");
        return null;
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

    /// <summary>
    /// Instancia o prefab MRUK (pacote com.meta.xr.mrutilitykit) na cena aberta,
    /// com QR tracking ativo e SEM carregar o modelo da sala (só precisamos dos
    /// trackables). Idempotente — não duplica se já existir um MRUK na cena.
    /// Também disponível como menu para a cena MinijogoDardos (mantida à mão).
    /// </summary>
    [MenuItem("Ommo/PrevenGame/Adicionar MRUK à cena atual")]
    public static void InstanciarMRUK()
    {
        if (Object.FindObjectOfType<Meta.XR.MRUtilityKit.MRUK>() != null)
        {
            Debug.Log("[OmmoBuilder] MRUK já existe na cena — nada a fazer.");
            return;
        }

        const string caminhoPrefab = "Packages/com.meta.xr.mrutilitykit/Core/Tools/MRUK.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminhoPrefab);
        if (prefab == null)
        {
            Debug.LogError($"[OmmoBuilder] Prefab MRUK não encontrado em {caminhoPrefab}.");
            return;
        }

        var instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instancia.name = "MRUK";
        Undo.RegisterCreatedObjectUndo(instancia, "Create MRUK");

        var mruk = instancia.GetComponent<Meta.XR.MRUtilityKit.MRUK>();
        if (mruk != null && mruk.SceneSettings != null)
        {
            mruk.SceneSettings.LoadSceneOnStartup = false; // só trackables; sem modelo da sala
            var tc = mruk.SceneSettings.TrackerConfiguration;
            tc.QRCodeTrackingEnabled = true;
            mruk.SceneSettings.TrackerConfiguration = tc;
        }

        // INATIVO de propósito: o MRUK exige OVRCameraRig + sessão XR no Awake,
        // e no nosso fluxo ambos só existem depois do GestorXR.IniciarVR() (fim
        // da intro da calibração). O GestorXR ativa este objeto nesse momento.
        instancia.SetActive(false);
        EditorUtility.SetDirty(instancia);
        Debug.Log("[OmmoBuilder] ✅ MRUK adicionado (QR on, inativo até o GestorXR ligar o VR).");
    }

    static GameObject CriarVisualSensor()
    {
        var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubo.name = "CuboSensor";
        cubo.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f); // ~3 cm (1 u = 1 m)
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

    /// <summary>Textura para cursor de hardware — o Cursor.SetCursor exige Read/Write ativo.</summary>
    static Texture2D UITexture(string nome)
    {
        string path = $"{PastaUI}/{nome}.png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && (imp.textureType != TextureImporterType.Cursor || !imp.isReadable))
        {
            imp.textureType = TextureImporterType.Cursor;
            imp.isReadable  = true;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Sprite[] SpritesHelper(string subpasta, string[] nomes)
    {
        var arr = new Sprite[nomes.Length];
        for (int i = 0; i < nomes.Length; i++)
            arr[i] = CarregarSpriteAsset($"{PastaUI}/{subpasta}/{nomes[i]}.png");
        return arr;
    }

    /// <summary>
    /// Carrega um sprite garantindo os import settings de qualidade da UI: tipo Sprite,
    /// sem compressão (a DXT cria blocos nas bordas), mipmaps + trilinear (sem serrilhado
    /// quando a imagem é desenhada mais pequena que a textura). Idempotente — só reimporta
    /// se algo estiver diferente, por isso cada Build Cenas re-normaliza os assets.
    /// </summary>
    static Sprite CarregarSpriteAsset(string path, int maxTextureSize = 2048)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            bool mudou = false;
            if (imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType      = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                mudou = true;
            }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed)
            { imp.textureCompression = TextureImporterCompression.Uncompressed; mudou = true; }
            if (!imp.mipmapEnabled)                     { imp.mipmapEnabled = true;               mudou = true; }
            if (imp.filterMode != FilterMode.Trilinear) { imp.filterMode = FilterMode.Trilinear;  mudou = true; }
            if (imp.maxTextureSize != maxTextureSize)   { imp.maxTextureSize = maxTextureSize;    mudou = true; }
            if (mudou) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// Prefixo dos ficheiros de animação do exercício, como exportados pelo artista
    /// (UIAssets/Exercises/&lt;prefixo&gt;_1..5.png, nomes PT com acentos).
    /// </summary>
    static string PrefixoImagensExercicio(ExerciciosWaypoints.TipoExercicio tipo)
    {
        switch (tipo)
        {
            case ExerciciosWaypoints.TipoExercicio.FlexaoBraco:    return "flexãoDoBraço";
            case ExerciciosWaypoints.TipoExercicio.ElevacaoTotal:  return "elevaçãoTotal";
            case ExerciciosWaypoints.TipoExercicio.AbducaoLateral: return "abduçãoLateral";
            case ExerciciosWaypoints.TipoExercicio.FlexaoCotovelo: return "flexãoHorizontal";
            default:                                               return tipo.ToString();
        }
    }

    /// <summary>Sprites da animação do exercício (UIAssets/Exercises/&lt;prefixo&gt;_1..5.png).</summary>
    static Sprite[] CarregarSpritesExercicio(ExerciciosWaypoints.TipoExercicio tipo)
    {
        var lista = new List<Sprite>();
        string prefixo = PrefixoImagensExercicio(tipo);
        for (int s = 1; s <= 5; s++)
        {
            var sp = CarregarSpriteAsset($"{PastaUI}/Exercises/{prefixo}_{s}.png");
            if (sp != null) lista.Add(sp);
        }
        if (lista.Count == 0)
            Debug.LogWarning($"[OmmoBuilder] Sem imagens para {tipo} (esperava {PastaUI}/Exercises/{prefixo}_1..5.png).");
        return lista.ToArray();
    }

    static TMP_FontAsset PoppinsExtraBold => ObterFontePoppins("Poppins-ExtraBold");
    static TMP_FontAsset PoppinsMedium    => ObterFontePoppins("Poppins-Medium");

    /// <summary>Carrega o SDF do Poppins; se ainda não existir, gera-o a partir do TTF em Assets/Fonts.</summary>
    static TMP_FontAsset ObterFontePoppins(string nome)
    {
        string caminho = $"{PastaFontes}/{nome} SDF.asset";
        var fonte = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(caminho);
        if (fonte == null)
        {
            PoppinsFontBuilder.CriarFontes();
            fonte = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(caminho);
        }
        return fonte;
    }

    // ── Sequências de diálogo (assets editáveis) ──────────────────────
    static HelperFala F(HelperId q, HelperEmocao e, string t)
        => new HelperFala { Quem = q, Emocao = e, Texto = t };

    /// <summary>Introdução da calibração: a Jane e o Patrick apresentam-se e o jogo (avança por clique).</summary>
    static HelperFala[] FalasIntroCalibracaoDefault() => new[]
    {
        F(HelperId.Jane,    HelperEmocao.Neutral,   "Bem-vindo ao PrevenGame!"),
        F(HelperId.Jane,    HelperEmocao.Neutral,   "Eu sou a Jane e este é o meu colega Patrick. Vamos aparecer ao longo do jogo para te ajudar e motivar."),
        F(HelperId.Patrick, HelperEmocao.Impressed, "Aqui vais encontrar vários minijogos para ajudar na tua reabilitação. Exercícios escolhidos a dedo e gamificados para ações reais em cenários reais."),
        F(HelperId.Jane,    HelperEmocao.Laugh,     "Agora agarra o Ommo e vamos começar por calibrar."),
    };

    static HelperFala[] FalasTutorialDefault()
    {
        var h = HelperId.Patrick;
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
