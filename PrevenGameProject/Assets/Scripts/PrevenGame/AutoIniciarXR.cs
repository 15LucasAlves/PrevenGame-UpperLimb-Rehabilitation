using UnityEngine;
using UnityEngine.XR.Management;

/// <summary>
/// AutoIniciarXR — Liga o XR imediatamente no Start. SÓ para cenas de teste
/// (ex.: VrTestScene, que tem o seu próprio rig OVR).
///
/// Contexto: no jogo, o XR já não arranca com a app (m_InitManagerOnStart=0 no
/// Standalone) — é o <see cref="GestorXR"/> que o inicializa no momento certo do
/// fluxo (fim da intro da calibração). Cenas de teste isoladas não passam por
/// esse fluxo, por isso precisam deste componente para o headset acordar.
///
/// Nota: NÃO usar nas cenas do jogo (Menu/minijogos) — aí é o GestorXR que manda,
/// e este componente não cria rig nenhum (usa o que existir na cena).
///
/// Corre no Awake com prioridade máxima: o Awake do MRUK exige a sessão XR já
/// criada, por isso a inicialização tem de acontecer antes de qualquer outro
/// componente da cena acordar.
/// </summary>
[DefaultExecutionOrder(-5000)]
public class AutoIniciarXR : MonoBehaviour
{
    void Awake()
    {
        var mgr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (mgr == null)
        {
            Debug.LogError("[AutoIniciarXR] XRGeneralSettings/Manager em falta.");
            return;
        }

        if (mgr.activeLoader == null)
        {
            mgr.InitializeLoaderSync();
            if (mgr.activeLoader == null)
            {
                Debug.LogError("[AutoIniciarXR] Falha a inicializar o loader XR — headset ligado e Quest Link ativo?");
                return;
            }
        }
        mgr.StartSubsystems();
        Debug.Log("[AutoIniciarXR] ✅ XR inicializado (cena de teste).");
    }

    void OnDestroy()
    {
        // Em cenas de teste isoladas, desliga ao sair de Play Mode para deixar
        // o editor limpo. (No jogo real é o GestorXR que gere o ciclo de vida.)
        if (GestorXR.Instancia != null) return; // o jogo é o dono — não tocar
        var mgr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (mgr != null && mgr.activeLoader != null)
        {
            mgr.StopSubsystems();
            mgr.DeinitializeLoader();
        }
    }
}
