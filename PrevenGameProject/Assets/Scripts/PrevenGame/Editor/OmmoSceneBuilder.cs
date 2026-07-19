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
    const string CenaPomar    = "Assets/Scenes/MinijogoPomar.unity";
    const string CenaHangar   = "Assets/Scenes/MinijogoAviao.unity";

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
            "As cenas de minijogo (Dardos/Hangar/Pomar) NÃO são tocadas — são construídas/mantidas " +
            "manualmente e apenas registadas no Build Settings.\n\nContinuar?",
            "Sim", "Cancelar"))
            return;

        if (!System.IO.Directory.Exists("Assets/Scenes")) System.IO.Directory.CreateDirectory("Assets/Scenes");

        // Sprites das demos de exercício em Resources (o HudVR carrega-os em runtime).
        CopiarSpritesExerciciosParaResources();

        var sMenu = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        BuildMenuHub();
        EditorSceneManager.SaveScene(sMenu, CenaMenu);

        // Regista o Menu e, se existirem em disco, as cenas dos minijogos (mantidas à mão).
        var cenas = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(CenaMenu, true) };
        foreach (var cena in new[] { CenaMinijogo, CenaHangar, CenaPomar })
            if (System.IO.File.Exists(cena))
                cenas.Add(new EditorBuildSettingsScene(cena, true));
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

        // Modelos de calibração: base station (filha do BaseStation — segue o
        // alinhamento QR) e GRASP como visual da MaoJogador (segue o sensor e
        // liga/desliga com o tracking). Poses/escalas VALIDADAS pelo utilizador
        // com o hardware real (2026-07-17). O cubo antigo do OmmoDevice fica escondido.
        var baseStationGO = GameObject.Find("BaseStation");
        if (baseStationGO != null)
            InstanciarModeloComPose("Assets/Prefabs/calibrationPrefabs/Ommo.obj",
                baseStationGO.transform, "ModeloBaseStation",
                new Vector3(-0.06f, 0f, 0f), Vector3.zero, 0.001f);

        var maoJogador = Object.FindObjectOfType<MaoJogador>();
        if (maoJogador != null)
        {
            var grasp = InstanciarModeloComPose("Assets/Prefabs/calibrationPrefabs/Grasp.fbx",
                maoJogador.transform, "ModeloGrasp",
                new Vector3(-0.1f, 0f, 0f), new Vector3(-90f, 180f, 0f), 1.07f);
            if (grasp != null) maoJogador.Visual = grasp;
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

    /// <summary>
    /// Instancia um modelo (obj/fbx) como filho com pose LOCAL e escala uniforme
    /// exatas (valores afinados/validados no Inspector com o hardware real).
    /// </summary>
    static GameObject InstanciarModeloComPose(string caminho, Transform pai, string nome,
        Vector3 posLocal, Vector3 eulerLocal, float escalaUniforme)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (prefab == null)
        {
            Debug.LogWarning($"[OmmoBuilder] Modelo não encontrado: {caminho}");
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = nome;
        go.transform.SetParent(pai, false);
        go.transform.localPosition = posLocal;
        go.transform.localRotation = Quaternion.Euler(eulerLocal);
        go.transform.localScale    = Vector3.one * escalaUniforme;

        Debug.Log($"[OmmoBuilder] Modelo \"{nome}\": pos={posLocal}, rot={eulerLocal}, escala={escalaUniforme}.");
        return go;
    }

    // ═════════════════════════════════════════════════════════════════
    // Snapshot das luzes (guardar/repor as afinações feitas à mão)
    // ═════════════════════════════════════════════════════════════════
    [System.Serializable] class EstadoLuz
    {
        public string Nome;
        public float  Intensidade;
        public Color  Cor;
        public float  Alcance;
        public int    Tipo;
        public bool   Ativa;
    }
    [System.Serializable] class SnapshotLuzes { public List<EstadoLuz> Luzes = new List<EstadoLuz>(); }

    const string CaminhoSnapshotLuzes = "Assets/Prefabs/PrevenGameAssets/Dardos/LuzesSnapshot.json";

    /// <summary>
    /// Guarda intensidade/cor/alcance/estado de TODAS as luzes da cena aberta
    /// num JSON versionável — proteção contra perder afinações feitas à mão.
    /// </summary>
    [MenuItem("Ommo/PrevenGame/Luzes/Guardar snapshot das luzes (cena atual)")]
    public static void GuardarSnapshotLuzes()
    {
        var snap = new SnapshotLuzes();
        foreach (var luz in Object.FindObjectsOfType<Light>(true))
        {
            snap.Luzes.Add(new EstadoLuz
            {
                Nome        = luz.gameObject.name,
                Intensidade = luz.intensity,
                Cor         = luz.color,
                Alcance     = luz.range,
                Tipo        = (int)luz.type,
                Ativa       = luz.enabled && luz.gameObject.activeInHierarchy,
            });
        }

        System.IO.File.WriteAllText(CaminhoSnapshotLuzes, JsonUtility.ToJson(snap, prettyPrint: true));
        AssetDatabase.Refresh();
        Debug.Log($"[OmmoBuilder] 💡 Snapshot de {snap.Luzes.Count} luzes guardado em {CaminhoSnapshotLuzes}. " +
                  "(Lembra-te: as luzes em si vivem na CENA — Ctrl+S grava-as; isto é o backup/restore.)");
    }

    /// <summary>Reaplica o snapshot às luzes da cena aberta (por nome, pela ordem de ocorrência).</summary>
    [MenuItem("Ommo/PrevenGame/Luzes/Repor snapshot das luzes (cena atual)")]
    public static void ReporSnapshotLuzes()
    {
        if (!System.IO.File.Exists(CaminhoSnapshotLuzes))
        {
            EditorUtility.DisplayDialog("PrevenGame", "Não há snapshot guardado ainda.", "OK");
            return;
        }

        var snap = JsonUtility.FromJson<SnapshotLuzes>(System.IO.File.ReadAllText(CaminhoSnapshotLuzes));

        // Fila por nome — nomes duplicados aplicam-se pela ordem em que aparecem.
        var porNome = new Dictionary<string, Queue<EstadoLuz>>();
        foreach (var e in snap.Luzes)
        {
            if (!porNome.TryGetValue(e.Nome, out var fila)) porNome[e.Nome] = fila = new Queue<EstadoLuz>();
            fila.Enqueue(e);
        }

        int aplicadas = 0;
        foreach (var luz in Object.FindObjectsOfType<Light>(true))
        {
            if (!porNome.TryGetValue(luz.gameObject.name, out var fila) || fila.Count == 0) continue;
            var e = fila.Dequeue();
            luz.intensity = e.Intensidade;
            luz.color     = e.Cor;
            luz.range     = e.Alcance;
            luz.enabled   = e.Ativa;
            EditorUtility.SetDirty(luz);
            aplicadas++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[OmmoBuilder] 💡 Snapshot reposto: {aplicadas}/{snap.Luzes.Count} luzes aplicadas (gravar a cena para persistir).");
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

        // Painel de instruções — estilo menu de jogo VR: background da calibração
        // + texto centrado. (Os helpers Patrick/Jane ficam SÓ no monitor.)
        var painelInstr = new GameObject("PainelInstrucoes");
        painelInstr.transform.SetParent(root.transform, false);
        var piRect = painelInstr.AddComponent<RectTransform>();
        piRect.anchorMin = piRect.anchorMax = piRect.pivot = new Vector2(0.5f, 0.5f);
        piRect.anchoredPosition = Vector2.zero;
        piRect.sizeDelta        = new Vector2(1100f, 420f);
        var piImg = painelInstr.AddComponent<Image>();
        var bgCalib = UISprite("Background"); // o mesmo da calibração do monitor
        if (bgCalib != null) { piImg.sprite = bgCalib; piImg.color = Color.white; }
        else piImg.color = new Color(0.09f, 0.12f, 0.10f, 1f);
        piImg.raycastTarget = false;

        var textoInstr = CriarTexto("TextoInstrucoes", painelInstr.transform, "", 44,
            TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, Color.white, PoppinsMedium, FontStyles.Normal);
        textoInstr.rectTransform.offsetMin = new Vector2(60f, 40f);
        textoInstr.rectTransform.offsetMax = new Vector2(-60f, -40f);
        textoInstr.raycastTarget = false;
        painelInstr.SetActive(false);

        ecra.PainelInstrucoes = painelInstr;
        ecra.TextoInstrucoes  = textoInstr;

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
    /// Instala o minijogo dos dardos na cena ABERTA (a dos dardos, mantida à mão):
    /// troca o MinijogoTeste pelo MinijogoDardos, substitui o EcraVR antigo pelo
    /// novo (painel de instruções em vez do diálogo com helpers), e usa a Camera
    /// baked do FBX como ponto de partida do jogador (desativando-a).
    /// </summary>
    [MenuItem("Ommo/PrevenGame/Instalar MinijogoDardos na cena atual")]
    public static void InstalarMinijogoDardos()
    {
        var gestor = ObterOuCriarScaffoldMinijogo();
        if (gestor == null) return;

        // 0. ESCALA DO MUNDO: mede o raio real do "aro 5" e normaliza o root do
        // FBX para o alvo ter ~0.25 m de raio (alvo de dardos padrão). Sem isto
        // o bar fica quilométrico (o far plane corta a imagem, o aro 1 fica a
        // centenas de metros de altura e o dardo é um grão de pó invisível).
        const float RAIO_ALVO_ARO5 = 0.25f;
        var aro5 = GameObject.Find("aro 5");
        if (aro5 != null && aro5.GetComponent<Renderer>() != null)
        {
            var bounds = aro5.GetComponent<Renderer>().bounds;
            float raioAtual = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);

            Transform raiz = aro5.transform;
            while (raiz.parent != null) raiz = raiz.parent;

            if (raioAtual > 0.0001f && Mathf.Abs(raioAtual - RAIO_ALVO_ARO5) / RAIO_ALVO_ARO5 > 0.1f)
            {
                float fator = RAIO_ALVO_ARO5 / raioAtual;
                raiz.localScale *= fator;
                var aro1T = GameObject.Find("aro 1")?.transform;
                Debug.Log($"[OmmoBuilder] 🌍 Escala do mundo normalizada: raio do aro 5 {raioAtual:F2} m → {RAIO_ALVO_ARO5:F2} m " +
                          $"(fator {fator:E2}; escala do root \"{raiz.name}\" = {raiz.localScale.x:F4})." +
                          (aro1T != null ? $" aro 1 agora em {aro1T.position}." : ""));
            }
            else Debug.Log($"[OmmoBuilder] Escala do mundo OK (raio do aro 5 = {raioAtual:F2} m).");
        }
        else Debug.LogWarning("[OmmoBuilder] \"aro 5\" sem Renderer/não encontrado — escala do mundo não verificada.");

        // 1. Minijogo: desativa o placeholder de teste e cria/reutiliza os dardos.
        var teste = Object.FindObjectOfType<MinijogoTesteWaypoints>(true);
        if (teste != null) teste.gameObject.SetActive(false);

        var dardos = Object.FindObjectOfType<MinijogoDardos>(true);
        if (dardos == null)
            dardos = CreateEmpty("MinijogoDardos").AddComponent<MinijogoDardos>();
        gestor.Minijogo = dardos;

        // Zonas de captura atuais (a cena pode ter serializado defaults antigos).
        dardos.RaiosZonas      = new[] { 0.06f, 0.09f, 0.12f, 0.15f, 0.18f };
        dardos.PontuacoesZonas = new[] { 1.00f, 0.75f, 0.50f, 0.25f, 0.10f };

        // Modelo do dardo = o "dardo" do FBX da cena (clonado por rep; o original fica).
        if (dardos.ModeloDardo == null)
        {
            var modeloDardo = GameObject.Find("dardo") ?? GameObject.Find("Dardo");
            if (modeloDardo != null)
            {
                dardos.ModeloDardo = modeloDardo;
                Debug.Log($"[OmmoBuilder] Modelo do dardo = \"{modeloDardo.name}\" da cena.");
            }
            else Debug.LogWarning("[OmmoBuilder] Objeto \"dardo\" não encontrado — o minijogo usa o placeholder.");
        }

        // 2+. Esqueleto comum (EcraVR, alinhador, posicionador com alvo = aro 1
        // à distância de lançamento, câmaras FBX, áudio, luz).
        InstalarComumMinijogo(gestor, GameObject.Find("aro 1")?.transform, 2.37f);

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[OmmoBuilder] ✅ MinijogoDardos instalado (gravar a cena para persistir).");
    }

    // ═════════════════════════════════════════════════════════════════
    // Instaladores — Hangar e Pomar
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Instala o jogo do HANGAR (elevação total) na cena aberta (MinijogoAviao):
    /// scaffold se faltar, MinijogoAviao com o avião auto-encontrado e o asset do
    /// bastão ligado, e o esqueleto comum. Os nomes internos do FBX são
    /// desconhecidos — os campos ficam expostos no Inspector para corrigir.
    /// </summary>
    [MenuItem("Ommo/PrevenGame/Instalar MinijogoHangar na cena atual")]
    public static void InstalarMinijogoHangar()
    {
        var gestor = ObterOuCriarScaffoldMinijogo();
        if (gestor == null) return;

        var teste = Object.FindObjectOfType<MinijogoTesteWaypoints>(true);
        if (teste != null) teste.gameObject.SetActive(false);

        var jogo = Object.FindObjectOfType<MinijogoAviao>(true);
        if (jogo == null) jogo = CreateEmpty("MinijogoHangar").AddComponent<MinijogoAviao>();
        gestor.Minijogo = jogo;

        // Valores atuais (a cena pode ter serializado defaults antigos).
        jogo.DistanciaSaidaTotal = 3f;

        if (jogo.Aviao == null) jogo.AutoEncontrarAviao();

        if (jogo.ModeloBastao == null)
        {
            var bastao = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/PrevenGameAssets/Hangar/bastão avião.fbx");
            if (bastao != null)
            {
                jogo.ModeloBastao = bastao;
                Debug.Log("[OmmoBuilder] Lightstick = asset \"bastão avião.fbx\".");
            }
            else Debug.LogWarning("[OmmoBuilder] Asset \"bastão avião.fbx\" não encontrado em " +
                                  "Assets/Prefabs/PrevenGameAssets/Hangar — o jogo usa o placeholder.");
        }

        // Escala do mundo: raiz SEMPRE a 1 (decisão do utilizador — a escala
        // certa vem do import do FBX, não de auto-normalização).
        Transform raizHangar = null;
        if (jogo.Aviao != null) { raizHangar = jogo.Aviao; while (raizHangar.parent != null) raizHangar = raizHangar.parent; }
        if (raizHangar == null) raizHangar = GameObject.Find("avioneta")?.transform;
        if (raizHangar != null && raizHangar.localScale != Vector3.one)
        {
            raizHangar.localScale = Vector3.one;
            Debug.Log($"[OmmoBuilder] Escala da raiz \"{raizHangar.name}\" reposta a 1.");
        }
        InstalarComumMinijogo(gestor, jogo.Aviao, 6f);

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[OmmoBuilder] ✅ MinijogoHangar instalado (gravar a cena para persistir). " +
                  "Confirma Aviao/DirecaoSaidaLocal/ModeloBastao no Inspector.");
    }

    /// <summary>
    /// Instala o jogo do POMAR (flexão do cotovelo) na cena aberta (MinijogoPomar):
    /// scaffold se faltar, MinijogoPomar com frutas/cesta auto-encontradas e o
    /// esqueleto comum. Correr DEPOIS de montar o mundo (bushes + frutas) na cena.
    /// </summary>
    [MenuItem("Ommo/PrevenGame/Instalar MinijogoPomar na cena atual")]
    public static void InstalarMinijogoPomar()
    {
        var gestor = ObterOuCriarScaffoldMinijogo();
        if (gestor == null) return;

        var teste = Object.FindObjectOfType<MinijogoTesteWaypoints>(true);
        if (teste != null) teste.gameObject.SetActive(false);

        var jogo = Object.FindObjectOfType<MinijogoPomar>(true);
        if (jogo == null) jogo = CreateEmpty("MinijogoPomar").AddComponent<MinijogoPomar>();
        gestor.Minijogo = jogo;

        if (jogo.FrutasModelos.Count == 0) jogo.AutoEncontrarFrutas();

        if (jogo.Cesta == null)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
                if (t.name.ToLowerInvariant().Contains("cest")) { jogo.Cesta = t; break; }
            if (jogo.Cesta != null) Debug.Log($"[OmmoBuilder] Cesta = \"{jogo.Cesta.name}\".");
            else Debug.Log("[OmmoBuilder] Sem cesta na cena (ainda) — a fruta desaparece junto à anca; " +
                           "atribui Cesta no Inspector quando tiveres o prefab.");
        }

        NormalizarEscalaMundoPorBounds(30f); // pomar ~30 m no máximo
        InstalarComumMinijogo(gestor, jogo.Cesta, 1.5f);

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[OmmoBuilder] ✅ MinijogoPomar instalado (gravar a cena para persistir). " +
                  "Confirma FrutasModelos/Cesta no Inspector.");
    }

    /// <summary>
    /// Cria (ou seleciona) o marcador de spawn do jogador — coloca-o na cena onde
    /// o jogador deve ficar e roda-o para onde ele deve olhar (seta do gizmo).
    /// O PosicionadorMundoJogo dá-lhe prioridade sobre o alvo.
    /// </summary>
    [MenuItem("Ommo/PrevenGame/Criar Ponto de Spawn do Jogador (cena atual)")]
    public static void CriarPontoSpawnJogador()
    {
        var existente = Object.FindObjectOfType<PontoSpawnJogador>();
        if (existente != null)
        {
            Selection.activeGameObject = existente.gameObject;
            Debug.Log($"[OmmoBuilder] Já existe um PontoSpawnJogador (\"{existente.name}\") — selecionado.");
            return;
        }

        var go = new GameObject("SpawnJogador");
        go.AddComponent<PontoSpawnJogador>();

        // Pose inicial: no chão por baixo da primeira câmara do FBX (com o yaw
        // dela), como filho da raiz do mundo — o utilizador ajusta depois.
        foreach (var cam in Object.FindObjectsOfType<Camera>(true))
        {
            if (cam.GetComponent<CamaraDesktop>() != null) continue;
            Transform raiz = cam.transform;
            while (raiz.parent != null) raiz = raiz.parent;
            go.transform.SetParent(raiz, true);
            var pos = cam.transform.position; pos.y = raiz.position.y;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
            break;
        }

        Selection.activeGameObject = go;
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[OmmoBuilder] ✅ SpawnJogador criado — posiciona-o onde o jogador deve ficar e roda a " +
                  "seta para onde ele deve olhar (gravar a cena para persistir).");
    }

    /// <summary>Gestor da cena; sem ele, corre primeiro o scaffold completo (cenas novas/vazias).</summary>
    static GestorMinijogo ObterOuCriarScaffoldMinijogo()
    {
        var gestor = Object.FindObjectOfType<GestorMinijogo>();
        if (gestor != null) return gestor;

        Debug.Log("[OmmoBuilder] Cena sem GestorMinijogo — a correr primeiro o scaffold VR completo.");
        AdicionarScaffoldMinijogo();
        gestor = Object.FindObjectOfType<GestorMinijogo>();
        if (gestor == null)
            EditorUtility.DisplayDialog("PrevenGame", "Não foi possível criar o scaffold do minijogo.", "OK");
        return gestor;
    }

    /// <summary>
    /// Passos genéricos partilhados pelos instaladores de minijogos: EcraVR novo,
    /// AlinhadorOmmoQr normalizado (-90°, sem visuais), PosicionadorMundoJogo
    /// (alvo + distância; raiz = topmost parent do alvo), câmaras do FBX
    /// desativadas (a primeira vira MarcadorVista), GestorAudio garantido e
    /// LuzPrincipal direcional sem sombras.
    /// </summary>
    static void InstalarComumMinijogo(GestorMinijogo gestor, Transform alvoPosicionador, float distanciaAlvo)
    {
        // EcraVR novo (versões antigas tinham o diálogo dos helpers preso à câmara).
        var ecraAntigo = Object.FindObjectOfType<EcraVR>(true);
        if (ecraAntigo != null) Object.DestroyImmediate(ecraAntigo.gameObject);
        gestor.Ecra = ConstruirEcraVR();

        // AlinhadorOmmoQr: sem visuais de ajuda + yaw validado com o QR real.
        var alinhadorCena = Object.FindObjectOfType<AlinhadorOmmoQr>(true);
        if (alinhadorCena != null)
        {
            alinhadorCena.MostrarVisualBase    = false;
            alinhadorCena.CorrecaoYawOmmoGraus = -90f;
        }

        // Posicionador do mundo: a cena constrói-se À VOLTA do jogador (o rig e
        // o Ommo ficam ancorados à realidade/QR — é o mundo que vem ter com ele).
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gestor.gameObject);
        var posicionador = gestor.GetComponent<PosicionadorMundoJogo>();
        if (posicionador == null) posicionador = gestor.gameObject.AddComponent<PosicionadorMundoJogo>();
        if (alvoPosicionador != null)
        {
            posicionador.AroCentral = alvoPosicionador;
            Transform raizMundo = alvoPosicionador;
            while (raizMundo.parent != null) raizMundo = raizMundo.parent;
            posicionador.RaizMundo = raizMundo;
            posicionador.DistanciaLancamento = distanciaAlvo;
            Debug.Log($"[OmmoBuilder] PosicionadorMundoJogo: raiz = \"{raizMundo.name}\", " +
                      $"alvo = \"{alvoPosicionador.name}\" a {distanciaAlvo:F2} m.");
        }
        else Debug.Log("[OmmoBuilder] Sem alvo para o PosicionadorMundoJogo — usa o FALLBACK por spawn " +
                       "(o mundo alinha-se pelo forward do PontoSpawn/câmara do FBX aos óculos).");

        // Câmaras do FBX: desativadas; a primeira serve de MARCADOR DE VISTA do
        // posicionador (a direção câmara→alvo é a vista autoral correta).
        foreach (var cam in Object.FindObjectsOfType<Camera>(true))
        {
            if (cam.GetComponent<CamaraDesktop>() != null) continue; // não é a do FBX
            cam.gameObject.tag = "Untagged";
            cam.enabled = false;
            var listener = cam.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            if (posicionador.MarcadorVista == null)
            {
                posicionador.MarcadorVista = cam.transform;
                Debug.Log($"[OmmoBuilder] Marcador de vista do posicionador = câmara \"{cam.name}\" do FBX.");
            }
        }
        if (posicionador.MarcadorVista == null)
            Debug.LogWarning("[OmmoBuilder] Sem câmara do FBX — o posicionador usa o forward do alvo (verifica InverterNormal).");

        // Spawner manual na cena → liga-o (tem prioridade sobre o alvo no runtime).
        var spawner = Object.FindObjectOfType<PontoSpawnJogador>();
        if (spawner != null)
        {
            posicionador.Spawner = spawner;
            Debug.Log($"[OmmoBuilder] Spawner manual \"{spawner.name}\" ligado (prioridade sobre o alvo).");
        }

        // Sem alvo, o alinhamento por spawn precisa da raiz do mundo: deriva-a
        // do spawner ou do marcador (câmara do FBX).
        if (posicionador.RaizMundo == null)
        {
            Transform origem = spawner != null ? spawner.transform : posicionador.MarcadorVista;
            if (origem != null)
            {
                Transform raizFallback = origem;
                while (raizFallback.parent != null) raizFallback = raizFallback.parent;
                posicionador.RaizMundo = raizFallback;
                Debug.Log($"[OmmoBuilder] Raiz do mundo (alinhamento por spawn) = \"{raizFallback.name}\".");
            }
        }

        // Áudio: garante o GestorAudio no bootstrap da cena (Play direto com som).
        var bootstrapCena = Object.FindObjectOfType<OmmoBootstrap>();
        if (bootstrapCena != null)
        {
            var audio = bootstrapCena.GetComponent<GestorAudio>();
            if (audio == null) audio = bootstrapCena.gameObject.AddComponent<GestorAudio>();
            ConfigurarGestorAudio(audio); // idempotente — preenche clips em falta
            Debug.Log("[OmmoBuilder] GestorAudio garantido no bootstrap da cena (clips ligados).");
        }
        else Debug.LogWarning("[OmmoBuilder] Sem OmmoBootstrap na cena — áudio não configurado.");

        // Iluminação: as luzes dos FBX (Blender) vêm desreguladas — garante a
        // direcional principal SEM sombras (idempotente por nome; afinável).
        if (GameObject.Find("LuzPrincipal") == null)
        {
            var luzGO = CreateEmpty("LuzPrincipal");
            var luz = luzGO.AddComponent<Light>();
            luz.type      = LightType.Directional;
            luz.color     = new Color(1f, 0.93f, 0.82f); // quente
            luz.intensity = 1.05f;
            luz.shadows   = LightShadows.None;
            luzGO.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Debug.Log("[OmmoBuilder] Adicionada a direcional principal sem sombras (LuzPrincipal).");
        }
    }

    /// <summary>
    /// Normaliza a escala do mundo pela dimensão MÁXIMA dos bounds de todos os
    /// renderers: se o mundo for &gt;1.5× maior que o alvo, o root do FBX é
    /// reduzido para caber. (Os FBX do Blender chegam muitas vezes ×100 — foi
    /// assim com o bar dos dardos; sem isto o jogador fica DENTRO de geometria
    /// colossal: backfaces pretas, buracos brancos de culling, z-fighting.)
    /// </summary>
    static void NormalizarEscalaMundoPorBounds(float dimensaoMaxAlvo)
    {
        var rends = Object.FindObjectsOfType<Renderer>();
        if (rends.Length == 0) { Debug.Log("[OmmoBuilder] Sem renderers na cena — mundo por montar?"); return; }

        Bounds b = rends[0].bounds;
        Renderer maior = rends[0];
        foreach (var r in rends)
        {
            b.Encapsulate(r.bounds);
            if (r.bounds.size.sqrMagnitude > maior.bounds.size.sqrMagnitude) maior = r;
        }
        float dimMax = Mathf.Max(b.size.x, b.size.y, b.size.z);
        Debug.Log($"[OmmoBuilder] 🌍 Bounds do mundo: {b.size.x:F1} × {b.size.y:F1} × {b.size.z:F1} m (centro {b.center}).");

        if (dimMax <= dimensaoMaxAlvo * 2f || dimMax <= 0.001f)
        {
            Debug.Log($"[OmmoBuilder] Escala do mundo OK ({dimMax:F1} m; esperado até ~{dimensaoMaxAlvo:F0} m).");
            return;
        }

        Transform raiz = maior.transform;
        while (raiz.parent != null) raiz = raiz.parent;

        // Ordem de grandeza do export ×100 do Blender → corrige EXATAMENTE ÷100
        // (preserva o tamanho autoral do mundo — encaixar num alvo arbitrário
        // encolhia mundos legitimamente grandes e o jogador ficava gigante).
        // Fora dessa ordem de grandeza, encaixa no esperado como último recurso.
        float fator = dimMax > dimensaoMaxAlvo * 20f ? 0.01f : dimensaoMaxAlvo / dimMax;
        raiz.localScale *= fator;
        Debug.Log($"[OmmoBuilder] 🌍 Escala do mundo normalizada: {dimMax:F0} m → {dimMax * fator:F0} m " +
                  $"(fator {fator:E2}; root \"{raiz.name}\", escala = {raiz.localScale.x:F4}).");
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
        grelha.spacing         = new Vector2(40f, 20f);
        grelha.padding         = new RectOffset(0, 0, 70, 70);
        grelha.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        grelha.startAxis       = GridLayoutGroup.Axis.Horizontal;
        // CENTRADO: com menos cards que colunas (hoje são 2), ficam no meio do
        // painel em vez de encostados à esquerda.
        grelha.childAlignment  = TextAnchor.UpperCenter;
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

        // Cards — um por exercício COM minijogo pronto (abdução lateral e POMAR
        // escondidos até estarem jogáveis; o mapa de cenas está no MinigameSelectionUI).
        var tipos = new[]
        {
            ExerciciosWaypoints.TipoExercicio.FlexaoBraco,    // DARDOS
            ExerciciosWaypoints.TipoExercicio.ElevacaoTotal,  // HANGAR
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

    /// <summary>Título do minijogo de cada exercício (texto do card).</summary>
    static string TituloJogo(ExerciciosWaypoints.TipoExercicio tipo)
    {
        switch (tipo)
        {
            case ExerciciosWaypoints.TipoExercicio.FlexaoBraco:    return "DARDOS";
            case ExerciciosWaypoints.TipoExercicio.ElevacaoTotal:  return "HANGAR";
            case ExerciciosWaypoints.TipoExercicio.FlexaoCotovelo: return "POMAR";
            default:                                               return "DARDOS";
        }
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

        // Imagem do exercício — rects por estado AFINADOS pelo utilizador
        // (2026-07-18): repouso (ícone) 800×400 @ (-1,10); hover (demo, artes
        // mais pequenas) 950×575 @ (-5,90) — o CardAnimacaoHover troca entre
        // eles ao entrar/sair do hover.
        var img = CriarImagem("ImagemExercicio", card.transform, null,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-1f, 10f), new Vector2(800f, 400f));
        img.raycastTarget  = false;
        img.preserveAspect = true;
        var hover = card.AddComponent<CardAnimacaoHover>();
        hover.ImagemAlvo    = img;
        hover.Sprites       = CarregarSpritesExercicio(tipo);
        hover.SpriteRepouso = CarregarIconeMinijogo(tipo); // ícone em repouso; demo no hover
        // Sprite inicial gravado na cena = o que se vê em repouso.
        var inicial = hover.SpriteRepouso != null ? hover.SpriteRepouso
                    : (hover.Sprites.Length > 0 ? hover.Sprites[0] : null);
        // CriarImagem(null) deixa a cor com alfa ~0 (placeholder invisível) — ao
        // atribuir o sprite, a cor TEM de voltar a opaca, senão nada aparece.
        if (inicial != null) { img.sprite = inicial; img.color = Color.white; }
        Debug.Log($"[OmmoBuilder] Card {tipo}: ícone={(hover.SpriteRepouso != null ? hover.SpriteRepouso.name : "—")}, " +
                  $"demo={hover.Sprites.Length} frames, inicial={(inicial != null ? inicial.name : "NENHUM ⚠")}.");

        // Título do jogo — na faixa desenhada (~53%–60% da altura).
        var titulo = CriarTexto("TituloJogo", card.transform, TituloJogo(tipo), 40, TextAlignmentOptions.Center,
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

        // Música de fundo do hub (toca em loop nos menus; para nos minijogos).
        audio.MusicaHub = Clip("MusicaDeFundoPaiva", avisarSeFaltar: false)
                       ?? Clip("backgroundMusic",    avisarSeFaltar: false)
                       ?? Clip("musicaFundo",        avisarSeFaltar: false)
                       ?? Clip("hubMusic",           avisarSeFaltar: false);
        if (audio.MusicaHub == null)
            Debug.Log($"[OmmoBuilder] Sem música de fundo do hub — coloca um \"MusicaDeFundoPaiva.mp3\" " +
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
        if (mruk != null)
        {
            // SEM world-locking: o MRUK reescreveria o TrackingSpace com base nos
            // spatial anchors dele (persistência entre sessões que NÃO usamos — o
            // QR re-alinha a cada arranque) e lutaria contra o nosso alinhamento,
            // além de gerar o tráfego de anchors que degrada as sessões Link.
            mruk.EnableWorldLock = false;

            if (mruk.SceneSettings != null)
            {
                mruk.SceneSettings.LoadSceneOnStartup = false; // só trackables; sem modelo da sala
                var tc = mruk.SceneSettings.TrackerConfiguration;
                tc.QRCodeTrackingEnabled = true;
                mruk.SceneSettings.TrackerConfiguration = tc;
            }
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

    /// <summary>
    /// Ícone do minijogo do exercício — o PNG de nome NÃO numérico na pasta
    /// Resources/Exercises/&lt;Tipo&gt; (ex.: "preven game_dardos.png"; os frames
    /// da demo chamam-se 1..5). Devolve null se o artista ainda não o entregou.
    /// </summary>
    static Sprite CarregarIconeMinijogo(ExerciciosWaypoints.TipoExercicio tipo)
    {
        string dir = $"Assets/Resources/Exercises/{tipo}";
        if (System.IO.Directory.Exists(dir))
        {
            foreach (var f in System.IO.Directory.GetFiles(dir, "*.png"))
            {
                string nome = System.IO.Path.GetFileNameWithoutExtension(f);
                if (int.TryParse(nome, out _)) continue; // frame da demo
                return CarregarSpriteAsset(f.Replace('\\', '/'));
            }
        }
        Debug.LogWarning($"[OmmoBuilder] Sem ícone de minijogo para {tipo} em {dir} — " +
                         "o card usa o 1º frame da demo em repouso.");
        return null;
    }

    /// <summary>
    /// Sprites da animação do exercício. Fonte primária: Resources/Exercises/&lt;Tipo&gt;/1..5.png
    /// (nomes ASCII — imunes a problemas de normalização Unicode dos nomes com acentos);
    /// fallback: UIAssets/Exercises/&lt;prefixo&gt;_1..5.png (nomes do artista).
    /// </summary>
    static Sprite[] CarregarSpritesExercicio(ExerciciosWaypoints.TipoExercicio tipo)
    {
        var lista = new List<Sprite>();

        for (int s = 1; s <= 5; s++)
        {
            var sp = CarregarSpriteAsset($"Assets/Resources/Exercises/{tipo}/{s}.png");
            if (sp != null) lista.Add(sp);
        }

        if (lista.Count == 0)
        {
            string prefixo = PrefixoImagensExercicio(tipo);
            for (int s = 1; s <= 5; s++)
            {
                var sp = CarregarSpriteAsset($"{PastaUI}/Exercises/{prefixo}_{s}.png");
                if (sp != null) lista.Add(sp);
            }
        }

        if (lista.Count == 0)
            Debug.LogWarning($"[OmmoBuilder] ⚠ SEM imagens de demo para {tipo} — nem em " +
                             $"Resources/Exercises/{tipo}/1..5.png nem em {PastaUI}/Exercises.");
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
