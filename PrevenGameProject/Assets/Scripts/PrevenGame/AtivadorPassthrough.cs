using UnityEngine;

/// <summary>
/// AtivadorPassthrough — Liga o passthrough (câmaras frontais) em runtime.
///
/// Motivo: a deteção de QR codes usa as câmaras frontais do headset, e estas só
/// ficam ativas com o passthrough ligado — sem ele o runtime nunca "vê" o código.
/// Também deixa o jogador ver a base station real durante a calibração.
///
/// O que faz ao ativar:
///   1. OVRManager.isInsightPassthroughEnabled = true (inicializa o passthrough);
///   2. garante um OVRPassthroughLayer (Underlay) no GameObject do OVRManager;
///   3. muda o fundo da câmara do olho para transparente (SolidColor, alpha 0),
///      para o mundo real aparecer atrás do conteúdo virtual.
///
/// Uso: na VrTestScene, adicionar a qualquer GameObject (ativa no Start).
/// No jogo, o GestorXR pode chamar <see cref="Ativar"/>/<see cref="Desativar"/>.
/// </summary>
public class AtivadorPassthrough : MonoBehaviour
{
    [Tooltip("Ativa o passthrough automaticamente no Start (cenas de teste).")]
    public bool AtivarNoStart = true;

    private static OVRPassthroughLayer _layer;
    private static CameraClearFlags _clearFlagsOriginais;
    private static Color _fundoOriginal;
    private static bool  _fundoGuardado;

    void Start()
    {
        if (AtivarNoStart) Ativar();
    }

    /// <summary>Liga o passthrough. Idempotente; loga o resultado.</summary>
    public static bool Ativar()
    {
        var manager = OVRManager.instance;
        if (manager == null)
        {
            Debug.LogWarning("[Passthrough] Sem OVRManager na cena — VR não está ativo?");
            return false;
        }

        manager.isInsightPassthroughEnabled = true;

        if (_layer == null)
        {
            _layer = manager.GetComponent<OVRPassthroughLayer>();
            if (_layer == null)
            {
                _layer = manager.gameObject.AddComponent<OVRPassthroughLayer>();
                _layer.overlayType = OVROverlay.OverlayType.Underlay;
            }
        }
        _layer.enabled = true;
        _layer.hidden  = false;

        // Fundo transparente na câmara do olho (o Underlay compõe por alfa) e HDR
        // desligado em TODAS as câmaras de olho — com HDR o eye buffer pode não
        // ter canal alfa e o passthrough fica preto.
        var cam = ObterCamaraOlho();
        if (cam != null)
        {
            if (!_fundoGuardado)
            {
                _clearFlagsOriginais = cam.clearFlags;
                _fundoOriginal       = cam.backgroundColor;
                _fundoGuardado       = true;
            }
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null)
        {
            DesligarHDR(rig.centerEyeAnchor);
            DesligarHDR(rig.leftEyeAnchor);
            DesligarHDR(rig.rightEyeAnchor);
        }

        Debug.Log("[Passthrough] ✅ Passthrough ativado (câmaras frontais ligadas, HDR off).");
        return true;
    }

    /// <summary>Desliga o passthrough e repõe o fundo original da câmara.</summary>
    public static void Desativar()
    {
        if (_layer != null) _layer.enabled = false;
        if (OVRManager.instance != null)
            OVRManager.instance.isInsightPassthroughEnabled = false;

        var cam = ObterCamaraOlho();
        if (cam != null && _fundoGuardado)
        {
            cam.clearFlags      = _clearFlagsOriginais;
            cam.backgroundColor = _fundoOriginal;
        }
        Debug.Log("[Passthrough] Passthrough desativado.");
    }

    static void DesligarHDR(Transform anchor)
    {
        if (anchor == null) return;
        var cam = anchor.GetComponent<Camera>();
        if (cam != null) cam.allowHDR = false;
    }

    static Camera ObterCamaraOlho()
    {
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null && rig.centerEyeAnchor != null)
        {
            var cam = rig.centerEyeAnchor.GetComponent<Camera>();
            if (cam != null) return cam;
        }
        return Camera.main;
    }
}
