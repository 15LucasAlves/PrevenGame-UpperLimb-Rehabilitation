using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;

/// <summary>
/// OmmoSceneBuilder - Editor script que constrói cenas Ommo automaticamente.
///
/// Ommo → Build Scene (Diagnóstico) — cena completa com hardware panel e grelha 3D.
/// Ommo → Build Scene (Jogo)        — cena mínima para o jogo: sem UI de diagnóstico.
/// </summary>
public class OmmoSceneBuilder : EditorWindow
{
    // ── Cena do Jogo (sem UI de diagnóstico) ─────────────────────────────

    [MenuItem("Ommo/Build Scene (Jogo)")]
    public static void BuildSceneJogo()
    {
        if (!EditorUtility.DisplayDialog("Ommo Scene Builder — Jogo",
            "Cria o mundo 3D do PrevenGame:\n" +
            "• Mundo 3D com câmara e piso de referência\n" +
            "• BaseStation = origem do espaço de tracking\n" +
            "• Objeto 3D controlado pelo sensor Ommo\n" +
            "• OmmoSensorManager inicia tracking automaticamente\n\nContinuar?",
            "Sim, construir!", "Cancelar"))
            return;

        Debug.Log("[OmmoBuilder] A construir mundo 3D do jogo...");
        ClearExistingJogo();

        // ── Materiais ─────────────────────────────────────────────────
        // Sensor — cápsula branca (representa o membro a reabilitar)
        Material sensorMat = new Material(Shader.Find("Standard"));
        sensorMat.color = new Color(0.95f, 0.95f, 0.95f);
        sensorMat.SetFloat("_Metallic", 0.1f);
        sensorMat.SetFloat("_Glossiness", 0.6f);
        sensorMat.name = "SensorMaterial";

        // Piso — grid subtil
        Material pisoMat = new Material(Shader.Find("Standard"));
        pisoMat.color = new Color(0.22f, 0.22f, 0.25f);
        pisoMat.SetFloat("_Metallic", 0f);
        pisoMat.SetFloat("_Glossiness", 0.1f);
        pisoMat.name = "PisoMaterial";

        // ── AppManager ────────────────────────────────────────────────
        GameObject appManager = CreateEmpty("AppManager");
        appManager.AddComponent<UnityMainThreadDispatcher>();
        var launcher   = appManager.AddComponent<OmmoServiceLauncher>();
        launcher.ServiceExeName = "ommo_service_v0.22.0.exe";
        launcher.WarmupSeconds  = 2.5f;
        launcher.KillOnExit     = true;
        var monitor    = appManager.AddComponent<OmmoHardwareMonitor>();
        var devManager = appManager.AddComponent<OmmoDeviceManager>();
        var sensorMgr  = appManager.AddComponent<OmmoSensorManager>();

        // ── BaseStation ───────────────────────────────────────────────
        // Empty object na origem — representa a Base Station física Ommo.
        // Todas as posições dos sensores são relativas a este ponto.
        GameObject baseStation = CreateEmpty("BaseStation");
        baseStation.transform.position = Vector3.zero;

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

        // ── CuboSensor (SensorPrefab — o objeto controlado pelo sensor) ─
        // Cubo branco — filho do TrackedDevicePrefab.
        // OmmoDevice instancia um por unidade de sensor e move-o em Update()
        // com os dados de posição + rotação vindos do hardware Ommo.
        var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubo.name = "CuboSensor";
        cubo.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        Object.DestroyImmediate(cubo.GetComponent<BoxCollider>());
        cubo.GetComponent<MeshRenderer>().sharedMaterial = sensorMat;

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

        // ── PainelModoSensor — botões 1 Sensor / 2 Sensores ──────────
        // Container simples sem layout group — botões posicionados manualmente
        var painelModoGO = new GameObject("PainelModoSensor");
        painelModoGO.transform.SetParent(painelCalib.transform, false);
        var painelModoRect = painelModoGO.AddComponent<RectTransform>();
        painelModoRect.anchorMin        = new Vector2(0f, 0f);
        painelModoRect.anchorMax        = new Vector2(1f, 0f);
        painelModoRect.pivot            = new Vector2(0.5f, 0f);
        painelModoRect.anchoredPosition = new Vector2(0f, 24f);
        painelModoRect.sizeDelta        = new Vector2(0f, 60f);

        // Botão "1 Sensor" — lado esquerdo do centro
        var btn1GO = new GameObject("Botao1Sensor");
        btn1GO.transform.SetParent(painelModoGO.transform, false);
        var btn1Rect = btn1GO.AddComponent<RectTransform>();
        btn1Rect.anchorMin        = new Vector2(0.5f, 0.5f);
        btn1Rect.anchorMax        = new Vector2(0.5f, 0.5f);
        btn1Rect.pivot            = new Vector2(1f, 0.5f);
        btn1Rect.anchoredPosition = new Vector2(-16f, 0f);
        btn1Rect.sizeDelta        = new Vector2(220f, 54f);
        var btn1Img = btn1GO.AddComponent<Image>();
        btn1Img.color = new Color(0.18f, 0.5f, 0.82f);
        var btn1Btn = btn1GO.AddComponent<Button>();
        var btn1Colors = btn1Btn.colors;
        btn1Colors.highlightedColor = new Color(0.25f, 0.65f, 1f);
        btn1Colors.pressedColor     = new Color(0.12f, 0.38f, 0.65f);
        btn1Btn.colors = btn1Colors;

        var btn1LabelGO = new GameObject("Label");
        btn1LabelGO.transform.SetParent(btn1GO.transform, false);
        var btn1LabelRect = btn1LabelGO.AddComponent<RectTransform>();
        btn1LabelRect.anchorMin = Vector2.zero;
        btn1LabelRect.anchorMax = Vector2.one;
        btn1LabelRect.offsetMin = Vector2.zero;
        btn1LabelRect.offsetMax = Vector2.zero;
        var btn1Label = btn1LabelGO.AddComponent<TextMeshProUGUI>();
        btn1Label.text      = "1 Sensor";
        btn1Label.fontSize  = 22;
        btn1Label.color     = Color.white;
        btn1Label.fontStyle = FontStyles.Bold;
        btn1Label.alignment = TextAlignmentOptions.Center;
        btn1Label.raycastTarget = false;

        // Botão "2 Sensores" — lado direito do centro
        var btn2GO = new GameObject("Botao2Sensores");
        btn2GO.transform.SetParent(painelModoGO.transform, false);
        var btn2Rect = btn2GO.AddComponent<RectTransform>();
        btn2Rect.anchorMin        = new Vector2(0.5f, 0.5f);
        btn2Rect.anchorMax        = new Vector2(0.5f, 0.5f);
        btn2Rect.pivot            = new Vector2(0f, 0.5f);
        btn2Rect.anchoredPosition = new Vector2(16f, 0f);
        btn2Rect.sizeDelta        = new Vector2(220f, 54f);
        var btn2Img = btn2GO.AddComponent<Image>();
        btn2Img.color = new Color(0.15f, 0.6f, 0.3f);
        var btn2Btn = btn2GO.AddComponent<Button>();
        var btn2Colors = btn2Btn.colors;
        btn2Colors.highlightedColor = new Color(0.2f, 0.8f, 0.4f);
        btn2Colors.pressedColor     = new Color(0.1f, 0.45f, 0.22f);
        btn2Btn.colors = btn2Colors;

        var btn2LabelGO = new GameObject("Label");
        btn2LabelGO.transform.SetParent(btn2GO.transform, false);
        var btn2LabelRect = btn2LabelGO.AddComponent<RectTransform>();
        btn2LabelRect.anchorMin = Vector2.zero;
        btn2LabelRect.anchorMax = Vector2.one;
        btn2LabelRect.offsetMin = Vector2.zero;
        btn2LabelRect.offsetMax = Vector2.zero;
        var btn2Label = btn2LabelGO.AddComponent<TextMeshProUGUI>();
        btn2Label.text      = "2 Sensores";
        btn2Label.fontSize  = 22;
        btn2Label.color     = Color.white;
        btn2Label.fontStyle = FontStyles.Bold;
        btn2Label.alignment = TextAlignmentOptions.Center;
        btn2Label.raycastTarget = false;

        // ── Atualiza instrução inicial para EscolherModo ──────────────
        textoInstr.text = "Quantos sensores tens?";
        textoSub.text   = "Escolhe o modo de rastreamento.";

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
        calibManager.PainelModoSensor     = painelModoGO;

        // Liga os botões de modo ao manager (AddListener necessita do manager já criado)
        btn1GO.GetComponent<Button>().onClick.AddListener(calibManager.EscolherModo1Sensor);
        btn2GO.GetComponent<Button>().onClick.AddListener(calibManager.EscolherModo2Sensores);

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

        // ── HUDJogo (ativo durante o exercício) ───────────────────────
        var hudJogo = new GameObject("HUDJogo");
        hudJogo.transform.SetParent(gameCanvasGO.transform, false);
        hudJogo.AddComponent<RectTransform>();

        // TextoRepeticao — canto superior esquerdo
        var textoRepGO = new GameObject("TextoRepeticao");
        textoRepGO.transform.SetParent(hudJogo.transform, false);
        var textoRepRect = textoRepGO.AddComponent<RectTransform>();
        textoRepRect.anchorMin        = new Vector2(0f, 1f);
        textoRepRect.anchorMax        = new Vector2(0f, 1f);
        textoRepRect.pivot            = new Vector2(0f, 1f);
        textoRepRect.anchoredPosition = new Vector2(20f, -20f);
        textoRepRect.sizeDelta        = new Vector2(380f, 40f);
        var textoRep = textoRepGO.AddComponent<TextMeshProUGUI>();
        textoRep.text      = "Repetição 1 / 3  ↓";
        textoRep.fontSize  = 22;
        textoRep.color     = new Color(0.9f, 0.9f, 0.9f);
        textoRep.fontStyle = FontStyles.Bold;

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

        hudJogo.SetActive(false); // escondido até calibração terminar

        // ── PainelFim ─────────────────────────────────────────────────
        var painelFimGO = new GameObject("PainelFim");
        painelFimGO.transform.SetParent(gameCanvasGO.transform, false);
        var painelFimImg = painelFimGO.AddComponent<Image>();
        painelFimImg.color = new Color(0f, 0f, 0f, 0.7f);
        var painelFimRect = painelFimGO.GetComponent<RectTransform>();
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
        botaoRepLabel.text      = "Repetir";
        botaoRepLabel.fontSize  = 22;
        botaoRepLabel.color     = Color.white;
        botaoRepLabel.fontStyle = FontStyles.Bold;
        botaoRepLabel.alignment = TextAlignmentOptions.Center;

        painelFimGO.SetActive(false);

        // ── PrevenGameManager ─────────────────────────────────────────
        var gameManager = appManager.AddComponent<PrevenGameManager>();
        gameManager.Esqueleto          = esqueleto;
        gameManager.CalibracaoManager  = calibManager;
        gameManager.CanvasJogo         = gameCanvas;
        gameManager.HUDJogo            = hudJogo;
        gameManager.TextoRepeticao     = textoRep;
        gameManager.TextoTempo         = textoTempo;
        gameManager.TextoCompensacao   = textoComp;
        gameManager.PainelFim          = painelFimGO;
        gameManager.TextoResultado     = textoResult;
        gameManager.TextoEstatisticas  = textoEstat;
        gameManager.BotaoRepetir       = botaoRepBtn;

        // Liga o botão Repetir ao método do manager
        botaoRepBtn.onClick.AddListener(gameManager.RepetirExercicio);

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

            // Posição definida em edit-time E garantida em runtime pelo OmmoCameraSetup
            mainCam.transform.position = new Vector3(0f, 5f, -10f);
            mainCam.transform.LookAt(new Vector3(0f, 3f, 2f));

            // Componente runtime — garante posição correta em cada Play independentemente do editor
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

    // ── Cena de Diagnóstico (hardware panel + grelha 3D) ─────────────────

    [MenuItem("Ommo/Build Scene (Diagnóstico)")]
    public static void BuildScene()
    {
        if (!EditorUtility.DisplayDialog("Ommo Scene Builder",
            "Isto vai criar todos os GameObjects e UI na cena atual.\n\nContinuar?",
            "Sim, construir!", "Cancelar"))
            return;

        Debug.Log("[OmmoBuilder] A construir cena...");

        // Limpa objetos Ommo existentes
        ClearExisting();

        // Materiais
        Material whiteMat  = CreateUnlitMaterial("OmmoWhite",  Color.white);
        Material redMat    = CreateUnlitMaterial("OmmoRed",    new Color(0.9f, 0.08f, 0.08f));
        Material gridMat   = CreateUnlitMaterial("OmmoGrid",   new Color(0.55f, 0.55f, 0.55f));

        // ── AppManager ────────────────────────────────────────────────
        GameObject appManager = CreateEmpty("AppManager");
        var dispatcher = appManager.AddComponent<UnityMainThreadDispatcher>();
        var launcher   = appManager.AddComponent<OmmoServiceLauncher>();
        launcher.ServiceExeName = "ommo_service_v0.22.0.exe";
        launcher.WarmupSeconds  = 2.5f;
        launcher.KillOnExit     = true;
        var monitor    = appManager.AddComponent<OmmoHardwareMonitor>();
        var devManager = appManager.AddComponent<OmmoDeviceManager>();

        // ── BaseStation ───────────────────────────────────────────────
        GameObject baseStation = CreateEmpty("BaseStation");
        baseStation.transform.position = Vector3.zero;

        // ── TrackedDevice Prefab ──────────────────────────────────────
        GameObject trackedDevicePrefab = BuildTrackedDevicePrefab(redMat);

        // ── DeviceRow Prefab ──────────────────────────────────────────
        GameObject deviceRowPrefab = BuildDeviceRowPrefab();

        // ── Main Canvas (Hardware Panel) ──────────────────────────────
        GameObject mainCanvas = BuildMainCanvas(deviceRowPrefab);

        // ── HUD Canvas (3D overlay) ───────────────────────────────────
        GameObject hudCanvas = BuildHUDCanvas();

        // ── Grid Camera ───────────────────────────────────────────────
        GameObject gridCamObj = BuildGridCamera(gridMat);

        // ── OmmoUIManager ─────────────────────────────────────────────
        var uiManager = appManager.AddComponent<OmmoUIManager>();
        WireUIManager(uiManager, mainCanvas, hudCanvas, gridCamObj, monitor,
                      deviceRowPrefab);

        // ── OmmoDeviceManager ─────────────────────────────────────────
        devManager.BaseStation  = baseStation;
        devManager.UnityScaleInCM = 10f;
        devManager.DeviceTypePrefabs = new OmmoDeviceManager.DeviceTypePrefab[]
        {
            new OmmoDeviceManager.DeviceTypePrefab { DeviceType = 0, Prefab = trackedDevicePrefab }
        };

        // ── Main Camera background ────────────────────────────────────
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.clearFlags       = CameraClearFlags.SolidColor;
            mainCam.backgroundColor  = new Color(0.93f, 0.93f, 0.93f);
            mainCam.transform.position = new Vector3(0, 0, -10);
        }

        Debug.Log("[OmmoBuilder] ✅ Cena construída com sucesso! Carrega Play para testar.");
        EditorUtility.DisplayDialog("Ommo Scene Builder",
            "✅ Cena construída com sucesso!\n\nAntes de carregar Play:\n• Garante que o OmmoService.exe está a correr\n\nDepois carrega Play.",
            "OK");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    static void ClearExisting()
    {
        string[] names = { "AppManager", "BaseStation", "MainCanvas", "HUDCanvas", "GridCamera",
                           "TrackedDevicePrefab_TEMP", "DeviceRowPrefab_TEMP" };
        foreach (var n in names)
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    static void ClearExistingJogo()
    {
        // Limpa objetos do jogo E eventuais restos da cena de diagnóstico
        string[] names = {
            "AppManager", "BaseStation", "Piso", "LuzDirecional",
            "TrackedDevicePrefab_TEMP", "ObjetoControlado", "CuboSensor",
            "PainelALigar_TEMP", "CalibracaoCanvas", "EsqueletoJogador", "PrevenGameCanvas",
            // restos da cena de diagnóstico
            "MainCanvas", "HUDCanvas", "GridCamera", "DeviceRowPrefab_TEMP"
        };
        foreach (var n in names)
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    static GameObject CreateEmpty(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    static Material CreateUnlitMaterial(string name, Color color)
    {
        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = color;
        mat.name  = name;
        return mat;
    }

    // ── TrackedDevice Prefab ──────────────────────────────────────────
    static GameObject BuildTrackedDevicePrefab(Material redMat)
    {
        var root = CreateEmpty("TrackedDevicePrefab_TEMP");
        root.AddComponent<OmmoDevice>();

        // Sensor sphere filho
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "SensorSphere";
        sphere.transform.SetParent(root.transform);
        sphere.transform.localScale    = Vector3.one * 0.15f;
        sphere.transform.localPosition = Vector3.zero;
        Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());
        sphere.GetComponent<MeshRenderer>().sharedMaterial = redMat;

        // Liga o SensorPrefab no OmmoDevice
        var ommoDevice = root.GetComponent<OmmoDevice>();
        ommoDevice.SensorPrefab = sphere;

        root.SetActive(false); // prefab inativo
        return root;
    }

    // ── Device Row Prefab ─────────────────────────────────────────────
    static GameObject BuildDeviceRowPrefab()
    {
        // Row container
        var row = CreateEmpty("DeviceRowPrefab_TEMP");
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 75);

        var rowImg = row.AddComponent<Image>();
        rowImg.color = Color.white;

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding        = new RectOffset(10, 10, 8, 8);
        layout.spacing        = 12;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth  = false;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth  = false;

        var outline = row.AddComponent<Outline>();
        outline.effectColor    = new Color(0.8f, 0.8f, 0.8f);
        outline.effectDistance = new Vector2(0, -1);

        // Status dot
        var dot = CreateUIImage("StatusDot", row.transform, new Vector2(18, 18));
        dot.color = new Color(0.5f, 0.5f, 0.5f);
        AddLayoutElement(dot.gameObject, 18, 18, false);

        // Name group (vertical)
        var nameGroup = CreateEmpty("NameGroup");
        nameGroup.transform.SetParent(row.transform, false);
        nameGroup.AddComponent<RectTransform>();
        var vLayout = nameGroup.AddComponent<VerticalLayoutGroup>();
        vLayout.childAlignment        = TextAnchor.MiddleLeft;
        vLayout.childControlHeight    = false;
        vLayout.childControlWidth     = true;
        vLayout.childForceExpandWidth = true;
        AddLayoutElement(nameGroup, 200, -1, true);

        var nameText = CreateTMPText("DeviceNameText", nameGroup.transform, "Device Name", 14, FontStyles.Bold);
        nameText.color = new Color(0.1f, 0.1f, 0.1f);
        AddLayoutElement(nameText.gameObject, -1, 22, false);

        var uuidText = CreateTMPText("DeviceUUIDText", nameGroup.transform, "0000000000", 11, FontStyles.Normal);
        uuidText.color = new Color(0.45f, 0.45f, 0.45f);
        AddLayoutElement(uuidText.gameObject, -1, 18, false);

        // Status text
        var statusText = CreateTMPText("StatusText", row.transform, "Idle", 13, FontStyles.Normal);
        statusText.color = new Color(0.3f, 0.3f, 0.3f);
        AddLayoutElement(statusText.gameObject, 80, -1, false);

        // Channel info
        var chanText = CreateTMPText("ChannelInfoText", row.transform, "Data Ch: Not Set\nSync Ch: 25", 11, FontStyles.Normal);
        chanText.color     = new Color(0.4f, 0.4f, 0.4f);
        chanText.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(chanText.gameObject, 140, -1, false);

        // PPS text
        var ppsText = CreateTMPText("PPSText", row.transform, "0 pps", 12, FontStyles.Normal);
        ppsText.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(ppsText.gameObject, 70, -1, false);

        // Action button
        var btn = CreateButton("ActionButton", row.transform, "Stop Motor", 100);
        AddLayoutElement(btn, 100, 36, false);

        // Add OmmoDeviceRow and wire fields
        var rowComp = row.AddComponent<OmmoDeviceRow>();
        rowComp.StatusDot       = dot;
        rowComp.DeviceNameText  = nameText;
        rowComp.DeviceUUIDText  = uuidText;
        rowComp.StatusText      = statusText;
        rowComp.ChannelInfoText = chanText;
        rowComp.PPSText         = ppsText;
        rowComp.ActionButton    = btn.GetComponent<Button>();
        rowComp.ActionButtonText = btn.GetComponentInChildren<TextMeshProUGUI>();

        row.SetActive(false);
        return row;
    }

    // ── Main Canvas ───────────────────────────────────────────────────
    static GameObject BuildMainCanvas(GameObject rowPrefab)
    {
        var canvasGO = CreateEmpty("MainCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bg = CreateUIImage("Background", canvasGO.transform, Vector2.zero);
        bg.color = new Color(0.93f, 0.93f, 0.93f);
        StretchFull(bg.GetComponent<RectTransform>());

        // ── Header panel ──────────────────────────────────────────────
        var header = CreatePanel("Header", canvasGO.transform, new Color(0.97f, 0.97f, 0.97f));
        var headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot     = new Vector2(0.5f, 1);
        headerRect.offsetMin = new Vector2(0, -110);
        headerRect.offsetMax = Vector2.zero;

        // Title
        var title = CreateTMPText("TitleText", header.transform, "ommo service", 13, FontStyles.Normal);
        title.color = new Color(0.3f, 0.3f, 0.3f);
        PlaceText(title, new Vector2(8, -8), TextAlignmentOptions.TopLeft);

        // Service Status row
        var svcLabel  = CreateTMPText("SvcLabel",  header.transform, "Service Status:", 13, FontStyles.Bold);
        var svcStatus = CreateTMPText("ServiceStatusText", header.transform, "Connecting...", 13, FontStyles.Normal);
        svcStatus.color = new Color(0.5f, 0.5f, 0.5f);
        PlaceText(svcLabel,  new Vector2(8, -28),  TextAlignmentOptions.TopLeft);
        PlaceText(svcStatus, new Vector2(145, -28), TextAlignmentOptions.TopLeft);

        // gRPC Port row
        var portLabel = CreateTMPText("PortLabel", header.transform, "gRPC Port:", 13, FontStyles.Bold);
        var portText  = CreateTMPText("GrpcPortText", header.transform, "localhost:50051", 13, FontStyles.Normal);
        PlaceText(portLabel, new Vector2(8, -48),   TextAlignmentOptions.TopLeft);
        PlaceText(portText,  new Vector2(145, -48), TextAlignmentOptions.TopLeft);

        // Errors row
        var errLabel = CreateTMPText("ErrLabel",  header.transform, "Errors:", 13, FontStyles.Bold);
        var errText  = CreateTMPText("ErrorsText", header.transform, "0", 13, FontStyles.Normal);
        PlaceText(errLabel, new Vector2(8, -68),   TextAlignmentOptions.TopLeft);
        PlaceText(errText,  new Vector2(145, -68), TextAlignmentOptions.TopLeft);

        // Separator line
        var sep = CreateUIImage("Separator", header.transform, new Vector2(0, 1));
        sep.color = new Color(0.78f, 0.78f, 0.78f);
        var sepRect = sep.GetComponent<RectTransform>();
        sepRect.anchorMin = new Vector2(0, 0);
        sepRect.anchorMax = new Vector2(1, 0);
        sepRect.pivot     = new Vector2(0.5f, 0);
        sepRect.sizeDelta = new Vector2(0, 1);
        sepRect.anchoredPosition = Vector2.zero;

        // Error Details button (top right)
        var errBtn = CreateButton("ErrorDetailsBtn", header.transform, "Error Details", 110);
        var errBtnRect = errBtn.GetComponent<RectTransform>();
        errBtnRect.anchorMin = new Vector2(1, 1);
        errBtnRect.anchorMax = new Vector2(1, 1);
        errBtnRect.pivot     = new Vector2(1, 1);
        errBtnRect.anchoredPosition = new Vector2(-8, -8);
        errBtnRect.sizeDelta = new Vector2(110, 28);

        // ── Button row ────────────────────────────────────────────────
        var btnRow = CreatePanel("ButtonRow", canvasGO.transform, new Color(0.93f, 0.93f, 0.93f));
        var btnRowRect = btnRow.GetComponent<RectTransform>();
        btnRowRect.anchorMin = new Vector2(0, 1);
        btnRowRect.anchorMax = new Vector2(1, 1);
        btnRowRect.pivot     = new Vector2(0.5f, 1);
        btnRowRect.offsetMin = new Vector2(0, -155);
        btnRowRect.offsetMax = new Vector2(0, -110);

        var btnLayout = btnRow.AddComponent<HorizontalLayoutGroup>();
        btnLayout.padding    = new RectOffset(8, 8, 6, 6);
        btnLayout.spacing    = 8;
        btnLayout.childAlignment         = TextAnchor.MiddleLeft;
        btnLayout.childControlHeight     = true;
        btnLayout.childControlWidth      = false;
        btnLayout.childForceExpandHeight = true;
        btnLayout.childForceExpandWidth  = false;

        var pairBtn      = CreateButton("StartPairingButton", btnRow.transform, "Start Pairing", 110);
        var openViewBtn  = CreateButton("OpenViewButton",     btnRow.transform, "Open 3D View ▶", 130);
        StyleButton(openViewBtn, new Color(0.18f, 0.47f, 0.78f), Color.white);
        AddLayoutElement(pairBtn,    110, 36, false);
        AddLayoutElement(openViewBtn, 130, 36, false);

        // Reset button on the right
        var spacer = CreateEmpty("Spacer");
        spacer.transform.SetParent(btnRow.transform, false);
        spacer.AddComponent<RectTransform>();
        AddLayoutElement(spacer, -1, -1, true); // flexible

        var resetBtn = CreateButton("ResetWirelessButton", btnRow.transform, "Reset Wireless Configuration", 210);
        AddLayoutElement(resetBtn, 210, 36, false);

        // ── Main scroll view (all sections inside) ───────────────────
        var scrollView = new GameObject("DeviceScrollView");
        scrollView.transform.SetParent(canvasGO.transform, false);
        var scrollRect = scrollView.AddComponent<ScrollRect>();
        var svRectT    = scrollView.GetComponent<RectTransform>();
        svRectT.anchorMin = new Vector2(0, 0);
        svRectT.anchorMax = new Vector2(1, 1);
        svRectT.offsetMin = new Vector2(8,  8);
        svRectT.offsetMax = new Vector2(-8, -160);

        var viewport = CreatePanel("Viewport", scrollView.transform, Color.clear);
        StretchFull(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        // Master content container
        var masterContent = CreateEmpty("MasterContent");
        masterContent.transform.SetParent(viewport.transform, false);
        var mcRect = masterContent.AddComponent<RectTransform>();
        mcRect.anchorMin = new Vector2(0, 1);
        mcRect.anchorMax = new Vector2(1, 1);
        mcRect.pivot     = new Vector2(0.5f, 1);
        mcRect.anchoredPosition = Vector2.zero;
        mcRect.sizeDelta = new Vector2(0, 0);
        var mcVlg = masterContent.AddComponent<VerticalLayoutGroup>();
        mcVlg.spacing = 2;
        mcVlg.childControlWidth = true;
        mcVlg.childControlHeight = true;
        mcVlg.childForceExpandWidth = true;
        mcVlg.childForceExpandHeight = false;
        var mcCsf = masterContent.AddComponent<ContentSizeFitter>();
        mcCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content  = mcRect;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;

        // ── Connected Section ─────────────────────────────────────────
        var connectedSection = BuildSection("ConnectedSection", mcRect.transform,
            "Connected Hardware", new Color(0.18f, 0.60f, 0.18f),
            out Transform connectedContainer);

        // ── Disconnected Section ──────────────────────────────────────
        var disconnectedSection = BuildSection("DisconnectedSection", mcRect.transform,
            "Disconnected Hardware", new Color(0.5f, 0.5f, 0.5f),
            out Transform disconnectedContainer);

        // ── Blocked Section ───────────────────────────────────────────
        var blockedSection = BuildSection("BlockedSection", mcRect.transform,
            "Blocked Wireless Hardware", new Color(0.85f, 0.3f, 0.3f),
            out Transform blockedContainer);

        return canvasGO;
    }

    // ── HUD Canvas ────────────────────────────────────────────────────
    static GameObject BuildHUDCanvas()
    {
        var canvasGO = CreateEmpty("HUDCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // above everything
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Semi-transparent top bar background
        var topBg = CreateUIImage("TopBarBG", canvasGO.transform, new Vector2(0, 20));
        topBg.color = new Color(0, 0, 0, 0.45f);
        var tbRect = topBg.GetComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0, 1);
        tbRect.anchorMax = new Vector2(1, 1);
        tbRect.pivot     = new Vector2(0.5f, 1);
        tbRect.sizeDelta = new Vector2(0, 20);
        tbRect.anchoredPosition = Vector2.zero;

        // Top bar text
        var topBarText = CreateTMPText("TopBarText", canvasGO.transform,
            "0,000000   (UUID: 0) (Port: 0) (Sensor: 0)   Fusion Mode: Default", 12, FontStyles.Normal);
        topBarText.color = Color.white;
        PlaceText(topBarText, new Vector2(6, -2), TextAlignmentOptions.TopLeft);

        // Left HUD info
        var hudBg = CreateUIImage("HUDBg", canvasGO.transform, new Vector2(320, 60));
        hudBg.color = new Color(0, 0, 0, 0.35f);
        var hudBgRect = hudBg.GetComponent<RectTransform>();
        hudBgRect.anchorMin = new Vector2(0, 1);
        hudBgRect.anchorMax = new Vector2(0, 1);
        hudBgRect.pivot     = new Vector2(0, 1);
        hudBgRect.anchoredPosition = new Vector2(0, -20);
        hudBgRect.sizeDelta = new Vector2(380, 60);

        var devCount = CreateTMPText("DeviceCountText", canvasGO.transform, "Device Count: 0", 13, FontStyles.Normal);
        devCount.color = Color.white;
        PlaceText(devCount, new Vector2(6, -24), TextAlignmentOptions.TopLeft);

        var baseText = CreateTMPText("BaseStationText", canvasGO.transform,
            "Base Station  X: 0.00  Y: 0.00  Z: 0.00  [Reference]", 13, FontStyles.Normal);
        baseText.color = Color.white;
        PlaceText(baseText, new Vector2(6, -40), TextAlignmentOptions.TopLeft);

        var devInfo = CreateTMPText("DeviceInfoText", canvasGO.transform,
            "UUID: —  Port: —  [No Device]", 13, FontStyles.Normal);
        devInfo.color = Color.white;
        PlaceText(devInfo, new Vector2(6, -56), TextAlignmentOptions.TopLeft);

        // Back button (top left)
        var backBtn = CreateButton("BackButton", canvasGO.transform, "◀ Back", 90);
        var bbRect  = backBtn.GetComponent<RectTransform>();
        bbRect.anchorMin = new Vector2(1, 1);
        bbRect.anchorMax = new Vector2(1, 1);
        bbRect.pivot     = new Vector2(1, 1);
        bbRect.anchoredPosition = new Vector2(-8, -8);
        bbRect.sizeDelta = new Vector2(90, 30);
        StyleButton(backBtn, new Color(0.2f, 0.2f, 0.2f, 0.8f), Color.white);

        canvasGO.SetActive(false);
        return canvasGO;
    }

    // ── Grid Camera ───────────────────────────────────────────────────
    static GameObject BuildGridCamera(Material gridMat)
    {
        var go  = CreateEmpty("GridCamera");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.fieldOfView     = 50f;
        cam.depth           = 1;
        cam.enabled         = false;

        // Position for isometric-ish view matching Ommo visualizer
        go.transform.position = new Vector3(12f, 10f, -14f);
        go.transform.LookAt(Vector3.zero);

        var viz = go.AddComponent<OmmoGridVisualizer>();
        viz.GridColor   = new Color(0.55f, 0.55f, 0.55f);
        viz.MarkerColor = new Color(0.9f, 0.08f, 0.08f);
        viz.GridHalfSize  = 10;
        viz.GridDivisions = 10;

        return go;
    }

    // ── Wire OmmoUIManager ────────────────────────────────────────────
    static void WireUIManager(OmmoUIManager ui,
        GameObject mainCanvas, GameObject hudCanvas,
        GameObject gridCamObj,
        OmmoHardwareMonitor monitor,
        GameObject rowPrefab)
    {
        ui.MainCanvas   = mainCanvas.GetComponent<Canvas>();
        ui.GridCamera   = gridCamObj.GetComponent<Camera>();
        ui.HUDCanvas    = hudCanvas.GetComponent<Canvas>();
        ui.HardwareMonitor = monitor;
        ui.GridVisualizer  = gridCamObj.GetComponent<OmmoGridVisualizer>();
        ui.DeviceRowPrefab = rowPrefab;

        // Hardware panel references
        ui.ServiceStatusText  = FindTMP(mainCanvas, "ServiceStatusText");
        ui.GrpcPortText       = FindTMP(mainCanvas, "GrpcPortText");
        ui.ErrorsText         = FindTMP(mainCanvas, "ErrorsText");
        ui.DeviceManager         = monitor.GetComponent<OmmoDeviceManager>();
        ui.ConnectedContainer    = FindTransform(mainCanvas, "ConnectedContainer");
        ui.DisconnectedContainer = FindTransform(mainCanvas, "DisconnectedContainer");
        ui.BlockedContainer      = FindTransform(mainCanvas, "BlockedContainer");
        ui.ConnectedSection      = FindGameObject(mainCanvas, "ConnectedSection");
        ui.DisconnectedSection   = FindGameObject(mainCanvas, "DisconnectedSection");
        ui.BlockedSection        = FindGameObject(mainCanvas, "BlockedSection");
        ui.StartPairingButton    = FindButton(mainCanvas, "StartPairingButton");
        ui.ResetWirelessButton = FindButton(mainCanvas, "ResetWirelessButton");
        ui.OpenViewButton      = FindButton(mainCanvas, "OpenViewButton");

        // HUD references
        ui.BackButton         = FindButton(hudCanvas, "BackButton");
        ui.HUDTopBarText      = FindTMP(hudCanvas, "TopBarText");
        ui.HUDDeviceCountText = FindTMP(hudCanvas, "DeviceCountText");
        ui.HUDBaseStationText = FindTMP(hudCanvas, "BaseStationText");
        ui.HUDDeviceInfoText  = FindTMP(hudCanvas, "DeviceInfoText");
    }

    // ── UI Helpers ────────────────────────────────────────────────────

    static GameObject BuildSection(string name, Transform parent, string label, Color labelColor, out Transform container)
    {
        var section = CreateEmpty(name);
        section.transform.SetParent(parent, false);
        var sectionRect = section.AddComponent<RectTransform>();
        var sectionVlg  = section.AddComponent<VerticalLayoutGroup>();
        sectionVlg.spacing = 2;
        sectionVlg.childControlWidth = true;
        sectionVlg.childControlHeight = true;
        sectionVlg.childForceExpandWidth = true;
        sectionVlg.childForceExpandHeight = false;
        sectionVlg.padding = new RectOffset(0, 0, 4, 4);
        var sectionCsf = section.AddComponent<ContentSizeFitter>();
        sectionCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sectionLe = section.AddComponent<LayoutElement>();
        sectionLe.flexibleWidth = 1;

        // Label
        var lbl = CreateTMPText(name + "Label", section.transform, label, 12, FontStyles.Bold);
        lbl.color = labelColor;
        var lblLe = lbl.gameObject.AddComponent<LayoutElement>();
        lblLe.minHeight = 24;
        lblLe.preferredHeight = 24;

        // Container for device rows
        var cont = CreateEmpty(name.Replace("Section","Container"));
        cont.transform.SetParent(section.transform, false);
        var contRect = cont.AddComponent<RectTransform>();
        var contVlg  = cont.AddComponent<VerticalLayoutGroup>();
        contVlg.spacing = 3;
        contVlg.childControlWidth = true;
        contVlg.childControlHeight = false;
        contVlg.childForceExpandWidth = true;
        var contCsf = cont.AddComponent<ContentSizeFitter>();
        contCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var contLe = cont.AddComponent<LayoutElement>();
        contLe.flexibleWidth = 1;

        container = cont.transform;
        return section;
    }

    static GameObject FindGameObject(GameObject root, string name)
    {
        var t = root.transform.Find(name);
        if (t != null) return t.gameObject;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child.gameObject;
        return null;
    }

        static Image CreateUIImage(string name, Transform parent, Vector2 size)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        return go.AddComponent<Image>();
    }

    static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img  = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    static TextMeshProUGUI CreateTMPText(string name, Transform parent, string text, int size, FontStyles style)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        tmp.text       = text;
        tmp.fontSize   = size;
        tmp.fontStyle  = style;
        tmp.color      = new Color(0.1f, 0.1f, 0.1f);
        tmp.raycastTarget = false;
        return tmp;
    }

    static GameObject CreateButton(string name, Transform parent, string label, float width)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 32);
        var img  = go.AddComponent<Image>();
        img.color = new Color(0.88f, 0.88f, 0.88f);
        var btn  = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.78f, 0.78f, 0.78f);
        colors.pressedColor     = new Color(0.65f, 0.65f, 0.65f);
        btn.colors = colors;

        var lbl = CreateTMPText("Label", go.transform, label, 12, FontStyles.Normal);
        lbl.color     = new Color(0.1f, 0.1f, 0.1f);
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.raycastTarget = false;
        StretchFull(lbl.GetComponent<RectTransform>());

        return go;
    }

    static void StyleButton(GameObject btn, Color bgColor, Color textColor)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img) img.color = bgColor;
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp) tmp.color = textColor;
    }

    static void PlaceText(TextMeshProUGUI tmp, Vector2 anchoredPos, TextAlignmentOptions align)
    {
        tmp.alignment = align;
        var rect = tmp.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0, 1);
        rect.anchorMax        = new Vector2(1, 1);
        rect.pivot            = new Vector2(0, 1);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(0, 20);
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
}
