using UnityEngine;

/// <summary>
/// CamaraDesktop — marcador da câmara que renderiza APENAS para o monitor
/// (janela do jogo), nunca para o headset. O <see cref="GestorXR"/> procura
/// este componente em cada cena para alternar entre ModoMonitor e ModoVR.
///
/// Configuração imposta no Awake:
///   • stereoTargetEye = None — com o XR ativo, a câmara continua a desenhar
///     só na janela do PC (por cima do mirror do HMD).
///   • cullingMask = 0 — a câmara não desenha o mundo 3D; serve apenas de
///     fundo sólido para os canvases Overlay do hub (a UI Overlay renderiza
///     no monitor independentemente das câmaras).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CamaraDesktop : MonoBehaviour
{
    void Awake()
    {
        var cam = GetComponent<Camera>();
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.targetDisplay   = 0;
        cam.cullingMask     = 0;
        cam.clearFlags      = CameraClearFlags.SolidColor;
    }
}
