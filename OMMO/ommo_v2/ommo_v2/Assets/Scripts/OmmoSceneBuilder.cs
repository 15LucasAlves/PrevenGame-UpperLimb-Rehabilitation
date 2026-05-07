using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;

/// <summary>
/// OmmoSceneBuilder - Editor script que constrói toda a cena Ommo automaticamente.
/// Menu: Ommo → Build Scene
/// </summary>
public class OmmoSceneBuilder : EditorWindow
{
    [MenuItem("Ommo/Build Scene")]
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
