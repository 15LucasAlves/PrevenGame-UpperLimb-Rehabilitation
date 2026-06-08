using UnityEngine;

/// <summary>
/// OmmoBootstrap — Camada de ligação Ommo persistente entre cenas.
///
/// Mantém vivos os únicos componentes que NÃO podem ser recriados a cada troca de cena:
///   • <see cref="UnityMainThreadDispatcher"/> — fila de callbacks gRPC → main thread.
///   • <see cref="OmmoServiceLauncher"/> — o processo ommo_service.exe e o flag estático
///     <c>ServiceReady</c>. Se fosse destruído numa troca de cena, o seu OnDestroy mataria o
///     serviço (KillOnExit) e o jogo teria de o relançar.
///   • <see cref="Ommo.Client"/> (estático em OmmoAPI) — persiste por ser <c>static</c>.
///
/// Tudo o resto (HardwareMonitor, DeviceManager, SensorManager, BaseStation, devices, esqueleto,
/// calibração, jogo, UI) é construído POR CADA CENA. Esses gestores re-inicializam-se sozinhos:
/// veem <c>OmmoServiceLauncher.ServiceReady == true</c> e o <see cref="OmmoDeviceManager"/>
/// recupera os sensores já ligados via <c>BootstrapConnectedDevices()</c> (GetTrackingDevices).
///
/// Padrão de uso: existe um GameObject com este componente em TODAS as cenas (menu e jogo). O
/// guard de singleton garante que só o primeiro sobrevive — entrar pelo menu cria-o uma vez;
/// abrir uma cena de jogo isolada (testes no editor) cria-o nessa cena. Reentrar numa cena cujo
/// bootstrap embutido encontra um já existente destrói o duplicado sem tocar no persistente.
/// </summary>
[DisallowMultipleComponent]
public class OmmoBootstrap : MonoBehaviour
{
    public static OmmoBootstrap Instancia { get; private set; }

    [Header("Configuração do serviço")]
    public string ServiceExeName = "ommo_service_v0.22.0.exe";
    public float  WarmupSeconds  = 2.5f;

    void Awake()
    {
        // Guard de singleton — só o primeiro bootstrap sobrevive e persiste.
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        DontDestroyOnLoad(gameObject);
        gameObject.name = "OmmoBootstrap";

        // Dispatcher (tem o seu próprio DontDestroyOnLoad + guard).
        if (GetComponent<UnityMainThreadDispatcher>() == null)
            gameObject.AddComponent<UnityMainThreadDispatcher>();

        // Launcher do serviço — mantém o .exe vivo e ServiceReady=true entre cenas.
        if (GetComponent<OmmoServiceLauncher>() == null)
        {
            var launcher = gameObject.AddComponent<OmmoServiceLauncher>();
            launcher.ServiceExeName = ServiceExeName;
            launcher.WarmupSeconds  = WarmupSeconds;
            launcher.KillOnExit     = true; // só dispara em OnApplicationQuit (objeto persistente)
        }

        Debug.Log("[OmmoBootstrap] Camada de ligação Ommo inicializada e persistente.");
    }
}
