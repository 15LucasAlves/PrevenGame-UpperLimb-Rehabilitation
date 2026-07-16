using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// TesteQrLogger — script TEMPORÁRIO (M0) para validar a deteção de QR codes
/// via MRUK sobre Quest Link, antes de investir no alinhamento Ommo↔VR.
///
/// Como usar:
///   1. Abrir a VrTestScene (tem o rig OVR + prefab MRUK com QR ativo).
///   2. Adicionar este componente a um GameObject qualquer (ex.: o MRUK).
///   3. Entrar em Play Mode com o Quest 3 ligado por Link, com
///      "Spatial Data over Meta Quest Link" ativo na app Meta Quest Link
///      (Settings → Beta) e permissões de dados espaciais no headset.
///   4. Olhar para o QR code colado na base station do Ommo.
///
/// O que esperar na consola:
///   - "QR tracking suportado: True" — se False, o runtime/Link não suporta.
///   - Uma linha "➕ QR detetado" com o payload quando o QR aparece.
///   - A cada segundo, pose + jitter (desvio face à última leitura) de cada QR.
///     Jitter < ~1 cm = estável o suficiente para ancorar o mundo (M3).
///
/// Remover este script no M6 (consolidação).
/// </summary>
public class TesteQrLogger : MonoBehaviour
{
    [Tooltip("Intervalo em segundos entre linhas de log por trackable.")]
    public float IntervaloLog = 1f;

    private bool _subscrito;
    private float _timer;
    private float _proximoWatchdog = -1f;
    private readonly List<MRUKTrackable> _trackables = new List<MRUKTrackable>();
    private readonly Dictionary<MRUKTrackable, Pose> _ultimaPose = new Dictionary<MRUKTrackable, Pose>();

    void Update()
    {
        // MRUK.Instance só existe depois do prefab MRUK acordar — subscrever quando estiver pronto
        if (!_subscrito)
        {
            if (MRUK.Instance == null) return;

            MRUK.Instance.SceneSettings.TrackableAdded.AddListener(AoTrackableAdicionado);
            MRUK.Instance.SceneSettings.TrackableRemoved.AddListener(AoTrackableRemovido);
            _subscrito = true;
            _proximoWatchdog = Time.unscaledTime + 8f;

            Debug.Log($"[TesteQr] Subscrito ao MRUK. QR tracking suportado: {MRUK.Instance.QRCodeTrackingSupported}");
        }

        WatchdogConfigTracker();

        _timer += Time.deltaTime;
        if (_timer < IntervaloLog) return;
        _timer = 0f;

        // Estado da cadeia (o mesmo diagnóstico do AlinhadorOmmoQr).
        Debug.Log($"[TesteQr] Estado: suportado={MRUK.Instance.QRCodeTrackingSupported} | " +
                  $"config pedida QR={MRUK.Instance.SceneSettings.TrackerConfiguration.QRCodeTrackingEnabled} | " +
                  $"config ATIVA QR={MRUK.Instance.TrackerConfiguration.QRCodeTrackingEnabled} | " +
                  $"foco sessão={OVRManager.hasVrFocus}");

        MRUK.Instance.GetTrackables(_trackables);

        int qrs = 0;
        foreach (var t in _trackables)
        {
            if (t == null || t.TrackableType != OVRAnchor.TrackableType.QRCode) continue;
            qrs++;

            Vector3 pos = t.transform.position;
            Quaternion rot = t.transform.rotation;

            // Jitter: desvio face à última leitura deste QR
            float jitterCm = -1f;
            float jitterGraus = -1f;
            if (_ultimaPose.TryGetValue(t, out var anterior))
            {
                jitterCm = Vector3.Distance(pos, anterior.position) * 100f;
                jitterGraus = Quaternion.Angle(rot, anterior.rotation);
            }
            _ultimaPose[t] = new Pose(pos, rot);

            Debug.Log($"[TesteQr] QR \"{t.MarkerPayloadString}\" | tracked={t.IsTracked} " +
                      $"| pos=({pos.x:F3}, {pos.y:F3}, {pos.z:F3}) m " +
                      $"| rot euler=({rot.eulerAngles.x:F1}, {rot.eulerAngles.y:F1}, {rot.eulerAngles.z:F1})° " +
                      (jitterCm >= 0f ? $"| jitter={jitterCm:F2} cm / {jitterGraus:F2}°" : "| (primeira leitura)"));
        }

        if (qrs == 0)
            Debug.Log("[TesteQr] Nenhum QR detetado ainda — aponta o headset ao QR da base station.");
    }

    /// <summary>
    /// Mesmo watchdog do AlinhadorOmmoQr: se o runtime recusou a config (ex.: MRUK
    /// acordou sem a sessão ter foco), o MRUK não re-tenta sozinho — mudar o pedido
    /// (flag do teclado) força novas tentativas até a config ativa ficar True.
    /// </summary>
    void WatchdogConfigTracker()
    {
        if (MRUK.Instance.TrackerConfiguration.QRCodeTrackingEnabled) return;
        if (_proximoWatchdog < 0f || Time.unscaledTime < _proximoWatchdog) return;
        _proximoWatchdog = Time.unscaledTime + 5f;

        var cfg = MRUK.Instance.SceneSettings.TrackerConfiguration;
        cfg.QRCodeTrackingEnabled   = true;
        cfg.KeyboardTrackingEnabled = !cfg.KeyboardTrackingEnabled;
        MRUK.Instance.SceneSettings.TrackerConfiguration = cfg;
        Debug.Log("[TesteQr] Watchdog: config ativa do QR ainda False — a forçar novo pedido ao runtime.");
    }

    private void AoTrackableAdicionado(MRUKTrackable t)
    {
        if (t.TrackableType != OVRAnchor.TrackableType.QRCode) return;
        Debug.Log($"[TesteQr] ➕ QR detetado: payload=\"{t.MarkerPayloadString}\" pos={t.transform.position}");
    }

    private void AoTrackableRemovido(MRUKTrackable t)
    {
        if (t.TrackableType != OVRAnchor.TrackableType.QRCode) return;
        Debug.Log($"[TesteQr] ➖ QR removido: payload=\"{t.MarkerPayloadString}\"");
        _ultimaPose.Remove(t);
    }

    void OnDestroy()
    {
        if (_subscrito && MRUK.Instance != null)
        {
            MRUK.Instance.SceneSettings.TrackableAdded.RemoveListener(AoTrackableAdicionado);
            MRUK.Instance.SceneSettings.TrackableRemoved.RemoveListener(AoTrackableRemovido);
        }
    }
}
