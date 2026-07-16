using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

/// <summary>
/// GestorXR — Gestor persistente do ciclo de vida XR (PCVR via Quest Link) e da
/// troca entre o monitor e o headset.
///
/// Decisões de arquitetura:
///   • O XR ARRANCA COM A APP (m_InitManagerOnStart=1 no Standalone). Isto é
///     OBRIGATÓRIO: a shared library do MRUK agarra a sessão OpenXR num init
///     estático que corre UMA vez antes do splash screen — se o loader ainda não
///     estiver ativo nesse instante, o MRUK fica sem OpenXR para sempre ("Open XR
///     session is not available"). Além disso, InitializeLoaderSync a meio da
///     sessão sobre Link causa hangs longos e artefactos visuais no headset.
///   • O rig OVR é criado logo no arranque (Start deste componente); o headset
///     mostra o placard "Remove os óculos" durante as fases de monitor.
///   • A troca monitor↔VR faz-se por câmaras, não por Stop/StartXR:
///       ModoMonitor — câmara desktop (CamaraDesktop) ativa a desenhar na janela;
///                     os canvases Overlay (hub) só aparecem no monitor; o HMD,
///                     se já estiver ativo, mostra um placard "Olha para o monitor".
///       ModoVR      — câmara desktop desativada (a janela passa a mostrar o mirror
///                     do HMD); o conteúdo do jogo vive em world-space.
///   • Este componente é o ÚNICO dono dos toggles de câmara/AudioListener — só o
///     AudioListener da câmara desktop fica ativo (o rig criado por código não tem).
///
/// O rig OVR (OVRCameraRig + OVRManager) é criado por código na primeira chamada a
/// <see cref="IniciarVR"/> e vive neste GameObject persistente — assim TODAS as cenas
/// partilham o mesmo rig e o mesmo espaço de tracking, sem prefabs por cena.
/// </summary>
[DisallowMultipleComponent]
public class GestorXR : MonoBehaviour
{
    public static GestorXR Instancia { get; private set; }

    /// <summary>True depois de o loader XR ter sido inicializado com sucesso.</summary>
    public bool VrAtivo { get; private set; }

    /// <summary>True enquanto o jogo está em ModoVR (conteúdo no headset).</summary>
    public bool EmModoVR { get; private set; }

    /// <summary>Transform do CenterEyeAnchor (cabeça do jogador) — null antes de IniciarVR.</summary>
    public Transform Cabeca => _rig != null ? _rig.centerEyeAnchor : null;

    /// <summary>O rig OVR partilhado (null antes de IniciarVR).</summary>
    public OVRCameraRig Rig => _rig;

    private OVRCameraRig _rig;
    private GameObject   _placard;          // aviso no HMD durante as fases de monitor
    private Camera       _camaraDesktop;    // câmara da cena atual marcada com CamaraDesktop
    private Camera       _camaraEspectador; // espelha a vista do jogador no monitor em ModoVR

    /// <summary>Obtém o gestor, criando-o (persistente) se ainda não existir na cena.</summary>
    public static GestorXR ObterOuCriar()
    {
        if (Instancia == null)
        {
            var go = new GameObject("GestorXR");
            go.AddComponent<GestorXR>(); // Awake preenche Instancia
        }
        return Instancia;
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(this); // só o componente — o GO pode ser o OmmoBootstrap partilhado
            return;
        }
        Instancia = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    void OnDestroy()
    {
        if (Instancia != this) return;
        SceneManager.sceneLoaded -= AoCarregarCena;
        Instancia = null;
    }

    void OnApplicationQuit()
    {
        // Único sítio onde o XR é desligado.
        var mgr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (mgr != null && mgr.activeLoader != null)
        {
            mgr.StopSubsystems();
            mgr.DeinitializeLoader();
        }
    }

    // ── Ciclo de vida XR ──────────────────────────────────────────────
    /// <summary>
    /// Garante o XR ativo. Normalmente é um no-op (o loader arranca com a app —
    /// ver nota do MRUK no topo); serve de retry se o Link não estava ligado no
    /// arranque. Atenção: o retry (InitializeLoaderSync a meio da sessão) pode
    /// bloquear vários segundos sobre Link.
    /// </summary>
    public bool IniciarVR()
    {
        if (VrAtivo) return true;

        var mgr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (mgr == null)
        {
            Debug.LogError("[GestorXR] XRGeneralSettings/Manager em falta — XR Plug-in Management mal configurado.");
            return false;
        }

        if (mgr.activeLoader == null)
        {
            mgr.InitializeLoaderSync();
            if (mgr.activeLoader == null)
            {
                Debug.LogError("[GestorXR] Falha a inicializar o loader XR — headset ligado e Quest Link ativo?");
                return false;
            }
        }
        mgr.StartSubsystems();

        CriarRig();
        VrAtivo = true;
        AtivarMRUK();
        Debug.Log("[GestorXR] ✅ XR inicializado (loader ativo, rig criado).");
        return true;
    }

    /// <summary>
    /// Ativa o MRUK da cena (o builder deixa-o INATIVO porque o Awake do MRUK
    /// exige OVRCameraRig + sessão XR). Espera pela sessão FOCUSED (headset na
    /// cabeça): configurar os trackers com a sessão sem foco falha — e o MRUK
    /// não re-tenta sozinho.
    /// </summary>
    void AtivarMRUK()
    {
        if (!VrAtivo) return;
        var mruk = FindObjectOfType<Meta.XR.MRUtilityKit.MRUK>(true);
        if (mruk == null || mruk.gameObject.activeSelf) return;
        StartCoroutine(AtivarMrukQuandoFocado(mruk));
    }

    System.Collections.IEnumerator AtivarMrukQuandoFocado(Meta.XR.MRUtilityKit.MRUK mruk)
    {
        bool avisou = false;
        while (mruk != null && !OVRManager.hasVrFocus)
        {
            if (!avisou)
            {
                avisou = true;
                Debug.Log("[GestorXR] À espera de foco da sessão VR (põe os óculos) para ativar o MRUK...");
            }
            yield return null;
        }
        yield return new WaitForSeconds(0.5f); // deixa a sessão estabilizar após o foco

        // ORDEM CRÍTICA (validada na VrTestScene): o passthrough tem de estar ativo
        // — câmaras frontais ligadas — ANTES de o MRUK configurar os trackers,
        // senão o tracker de QR fica configurado mas nunca deteta nada.
        // (O passthrough é pedido na criação do rig; aqui só se espera pelo init.)
        float timeout = Time.unscaledTime + 5f;
        while (Time.unscaledTime < timeout && !OVRManager.IsInsightPassthroughInitialized())
            yield return null;
        if (!OVRManager.IsInsightPassthroughInitialized())
            Debug.LogWarning("[GestorXR] Passthrough não inicializou em 5 s — o QR pode não detetar.");

        if (mruk != null && !mruk.gameObject.activeSelf)
        {
            mruk.gameObject.SetActive(true);
            Debug.Log("[GestorXR] MRUK ativado (sessão com foco, passthrough inicializado).");
        }
    }

    // ── Modos ─────────────────────────────────────────────────────────
    /// <summary>Hub no monitor: câmara desktop ativa; HMD (se ativo) mostra o placard.</summary>
    public void ModoMonitor()
    {
        EmModoVR = false;
        if (_camaraDesktop != null)    _camaraDesktop.enabled    = true;
        if (_camaraEspectador != null) _camaraEspectador.enabled = false;
        AtualizarPlacard();
        Debug.Log("[GestorXR] ModoMonitor.");
    }

    /// <summary>
    /// Jogo no headset: a câmara-espectador (presa ao olho do jogador) espelha a
    /// vista VR na janela do PC para o fisioterapeuta acompanhar.
    /// </summary>
    public bool ModoVR()
    {
        if (!IniciarVR()) return false;
        EmModoVR = true;
        if (_camaraDesktop != null)    _camaraDesktop.enabled    = false;
        if (_camaraEspectador != null) _camaraEspectador.enabled = true;
        AtualizarPlacard();
        Debug.Log("[GestorXR] ModoVR (espectador no monitor).");
        return true;
    }

    // ── Internos ──────────────────────────────────────────────────────
    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        LigarCena();
        AtivarMRUK(); // o MRUK de cada cena nasce inativo; com o XR já ativo pode ligar logo
        // Reaplica o modo atual à cena nova (a câmara desktop é por-cena).
        if (EmModoVR) { if (_camaraDesktop != null) _camaraDesktop.enabled = false; }
        else          { if (_camaraDesktop != null) _camaraDesktop.enabled = true;  }
        AtualizarPlacard();
    }

    void Start()
    {
        LigarCena(); // primeira cena (sceneLoaded já passou quando o Awake corre em cena aberta)

        // Com m_InitManagerOnStart=1 o loader já está ativo aqui — adota-o já:
        // cria o rig, ativa o MRUK e entra em ModoMonitor (placard no HMD).
        // Se o Link não estiver ligado, o init automático falhou e VrAtivo fica
        // false — o jogo corre só no monitor (IniciarVR tenta de novo mais tarde).
        var mgr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (mgr != null && mgr.activeLoader != null)
        {
            CriarRig();
            VrAtivo = true;
            AtivarMRUK();
            ModoMonitor();
            Debug.Log("[GestorXR] ✅ XR ativo desde o arranque (rig criado, MRUK ativado).");
        }
        else
        {
            Debug.LogWarning("[GestorXR] XR não inicializou no arranque (Link desligado?) — jogo em modo monitor.");
        }
    }

    /// <summary>Encontra a câmara desktop da cena e garante um único AudioListener.</summary>
    void LigarCena()
    {
        var marcador = FindObjectOfType<CamaraDesktop>();
        _camaraDesktop = marcador != null ? marcador.GetComponent<Camera>() : null;

        // Exatamente um AudioListener ativo: o da câmara desktop, ou o primeiro encontrado.
        var listeners = FindObjectsOfType<AudioListener>(true);
        AudioListener escolhido = null;
        if (_camaraDesktop != null) escolhido = _camaraDesktop.GetComponent<AudioListener>();
        foreach (var l in listeners)
        {
            if (escolhido == null) escolhido = l;
            l.enabled = (l == escolhido);
        }
    }

    /// <summary>
    /// Cria o rig OVR por código (OVRCameraRig constrói a hierarquia sozinho no Awake).
    /// O GameObject nasce INATIVO para os componentes serem configurados ANTES dos
    /// Awake — em particular, o passthrough tem de estar pedido quando o OVRManager
    /// inicializa (caminho de referência dos Building Blocks).
    /// </summary>
    void CriarRig()
    {
        if (_rig != null) return;

        var go = new GameObject("RigVR");
        go.transform.SetParent(transform, false); // persiste com o GestorXR
        go.SetActive(false);                      // defere os Awake até estar configurado

        var manager = go.AddComponent<OVRManager>();
        manager.trackingOriginType          = OVRManager.TrackingOrigin.FloorLevel;
        manager.isInsightPassthroughEnabled = true; // câmaras frontais: QR + ver a base real
        manager.AllowRecenter               = true;

        var passthrough = go.AddComponent<OVRPassthroughLayer>();
        passthrough.overlayType = OVROverlay.OverlayType.Underlay;

        _rig = go.AddComponent<OVRCameraRig>();

        go.SetActive(true); // Awake corre agora: OVRManager (passthrough pedido) + rig (câmaras)

        ConfigurarCamarasOlho();
        CriarCamaraEspectador();
        CriarPlacard();
    }

    /// <summary>
    /// Fundo transparente (o Underlay do passthrough compõe por alfa) e HDR
    /// DESLIGADO nas câmaras de olho — com HDR o eye buffer pode não ter canal
    /// alfa (R11G11B10) e o passthrough fica preto.
    /// </summary>
    void ConfigurarCamarasOlho()
    {
        ConfigurarCamaraOlho(_rig.centerEyeAnchor);
        ConfigurarCamaraOlho(_rig.leftEyeAnchor);
        ConfigurarCamaraOlho(_rig.rightEyeAnchor);
    }

    static void ConfigurarCamaraOlho(Transform anchor)
    {
        if (anchor == null) return;
        var cam = anchor.GetComponent<Camera>();
        if (cam == null) return;
        cam.allowHDR        = false;
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
    }

    /// <summary>
    /// Câmara-espectador: filha do CenterEyeAnchor, desenha SÓ na janela do PC
    /// (stereoTargetEye None) — em ModoVR o monitor mostra o que o jogador vê.
    /// </summary>
    void CriarCamaraEspectador()
    {
        var go = new GameObject("CamaraEspectador");
        go.transform.SetParent(_rig.centerEyeAnchor, false);

        _camaraEspectador = go.AddComponent<Camera>();
        _camaraEspectador.stereoTargetEye = StereoTargetEyeMask.None;
        _camaraEspectador.targetDisplay   = 0;
        _camaraEspectador.depth           = 10f; // por cima de qualquer outra câmara no monitor
        _camaraEspectador.fieldOfView     = 75f;
        _camaraEspectador.allowHDR        = false;
        _camaraEspectador.enabled         = false; // ligada só em ModoVR
    }

    /// <summary>
    /// Placard world-space mostrado no HMD sempre que o jogo volta ao monitor
    /// (o jogador tem de tirar os óculos para continuar — ex.: seleção e score).
    /// </summary>
    void CriarPlacard()
    {
        _placard = new GameObject("PlacardMonitor");
        _placard.transform.SetParent(_rig.transform, false);

        var canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(_placard.transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta  = new Vector2(1200f, 400f);
        rt.localScale = Vector3.one * 0.001f; // 1200 px → 1.2 m

        // Fundo escuro para o aviso ficar legível em qualquer ambiente.
        var fundoGO = new GameObject("Fundo");
        fundoGO.transform.SetParent(canvasGO.transform, false);
        var fundoRt = fundoGO.AddComponent<RectTransform>();
        fundoRt.anchorMin = Vector2.zero; fundoRt.anchorMax = Vector2.one;
        fundoRt.offsetMin = Vector2.zero; fundoRt.offsetMax = Vector2.zero;
        var fundo = fundoGO.AddComponent<UnityEngine.UI.Image>();
        fundo.color = new Color(0.06f, 0.06f, 0.09f, 0.92f);
        fundo.raycastTarget = false;

        var textoGO = new GameObject("Texto");
        textoGO.transform.SetParent(canvasGO.transform, false);
        var texto = textoGO.AddComponent<TMPro.TextMeshProUGUI>();
        texto.text      = "Remove os óculos\npara continuar";
        texto.fontSize  = 96f;
        texto.alignment = TMPro.TextAlignmentOptions.Center;
        texto.color     = Color.white;
        texto.raycastTarget = false;
        var trt = texto.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(40f, 40f); trt.offsetMax = new Vector2(-40f, -40f);

        _placard.SetActive(false);
    }

    /// <summary>Mostra o placard no HMD só em ModoMonitor (com XR ativo) e recentra-o à frente da cabeça.</summary>
    void AtualizarPlacard()
    {
        if (_placard == null) return;
        bool mostrar = VrAtivo && !EmModoVR;
        _placard.SetActive(mostrar);
        if (mostrar && Cabeca != null)
        {
            Vector3 frente = Cabeca.forward; frente.y = 0f;
            if (frente.sqrMagnitude < 0.001f) frente = Vector3.forward;
            frente.Normalize();
            _placard.transform.position = Cabeca.position + frente * 2f;
            _placard.transform.rotation = Quaternion.LookRotation(frente, Vector3.up);
        }
    }
}
