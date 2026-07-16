# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projeto

**PrevenGame** — Serious game adaptativo para reabilitação do membro superior.
Projeto final da Licenciatura em Engenharia de Desenvolvimento de Jogos Digitais (IPCA).

- **Estudante:** Lucas Ferreira Alves (nº 27922) — a27922@alunos.ipca.pt
- **Orientadores:** Pedro Lobo (pjlobo@ipca.pt) e João L. Vilaça (jvilaca@ipca.pt)

**Objetivo:** Jogo sério que transforma exercícios de fisioterapia em desafios orientados a tarefas do dia-a-dia, com dificuldade adaptada em tempo real a partir dos dados do sensor Ommo. Inclui interface para o fisioterapeuta monitorizar o progresso do paciente.

**Estado atual:** A camada de integração Ommo (sensor → Unity) está completa. O jogo é o modo **Gamification** (lançamento de dardos), com o loop principal implementado: main menu → calibração (guiada por personagens-ajudantes) → seleção de minijogos → minijogos → score. O antigo modo Clinical Trial e os scripts de diagnóstico foram removidos.

## Idioma

Todo o código, comentários e documentação deste repositório estão em **Português de Portugal**.

## Estrutura do Repositório

```
PrevenGameProject/   # Projeto Unity (Unity 2022.3.62f3)
  Assets/
    Scripts/         # Scripts organizados em duas camadas:
      Ommo/          #   Integração sensor→Unity (SDK, gRPC, hardware, diagnóstico)
        Editor/      #     OmmoPairingHelper (tool de emparelhamento SIU)
      PrevenGame/    #   Lógica do jogo (fluxo, minijogo, waypoints, calibração, esqueleto, UI)
        Editor/      #     OmmoSceneBuilder (construção automática das cenas)
    Prefabs/         # OmmoDevice.prefab, SensorPrefab.prefab, CuboSensor.prefab, TrackedDevicePrefab_TEMP
      PrevenGameAssets/
        Dardos/      #   Alvo1-5.prefab, Dardo.prefab, Sala.prefab (mundo do minijogo)
        UIAssets/    #   Fundos, botões, cards, cursor, balão, helpers Jane/Patrick, Exercises/
    Scenes/          # Menu.unity (hub), MinijogoDardos.unity
    Plugins/
      ommo.sdk/      # Binários do serviço Ommo (ommo_service_v0.22.0.exe, DLLs Qt)
      Cysharp.Net.Http.YetAnotherHttpHandler.Native/  # DLL nativa gRPC HTTP/2
    Packages/        # Packages NuGet (Grpc, Google.Protobuf, Microsoft.Extensions)
    NuGet/           # Plugin NuGetForUnity (editor only)
  Packages/
    manifest.json    # Dependências Unity Package Manager
OMMO/                # SDK Ommo de referência (não faz parte do jogo)
Documentos/          # Documentos académicos (PDF)
TextFiles/           # Entregas escritas e revisão bibliográfica
```

## Arquitetura Ommo (Sensor → Unity)

O fluxo de dados do sensor até ao jogo segue esta cadeia:

```
Hardware Ommo  →  ommo_service_v0.22.0.exe  →  gRPC (localhost:50051)  →  Unity
```

O serviço `.exe` é **obrigatório** como intermediário. O `OmmoServiceLauncher` lança-o automaticamente ao entrar em Play Mode e mata-o ao sair.

### Scripts de integração Ommo (Assets/Scripts/Ommo/)

| Script | Função |
|--------|--------|
| `OmmoAPI.cs` | Singleton `Ommo.Client` — gere o canal gRPC e o event stream de dispositivos; expõe `StreamTrackingDeviceData` e `StreamDataFrame` |
| `OmmoServiceLauncher.cs` | Lança/mata o `ommo_service_v0.22.0.exe`; emite `OnServiceReady` e `ServiceReady` (bool estático) |
| `OmmoServiceApi.cs` | Classes geradas por Protobuf (tipos de dados da API) |
| `OmmoServiceApiGrpc.cs` | Stubs gRPC gerados |
| `OmmoCoreServiceExtension.cs` | Extensão ao `CoreServiceClient` com métodos não incluídos no SDK: `GetHardwareStates` e `SetBaseStationMotorRunning` (inferidos do binário v0.22.0) |
| `OmmoDevice.cs` | MonoBehaviour de um dispositivo — abre stream `TrackingDeviceData` e actualiza os seus GameObjects filhos (um por sensor) |
| `OmmoDeviceManager.cs` | Instancia/destrói `OmmoDevice`s quando sensores ligam/desligam; expõe `StartTracking()` / `StopTracking()` |
| `OmmoHardwareMonitor.cs` | Polling a cada 1.5s ao `GetHardwareStates`; expõe `ServiceInfo` com listas Connected/Disconnected/Blocked; emite `OnHardwareUpdated` |
| `OmmoSensorManager.cs` | **Gestor de hardware para o jogo** — arranca o tracking via `IniciarTracking(n)`, para o motor em segurança, expõe `SensoresConectados`, `NumeroSensores` e evento `OnNumeroDeSensoresMudou` |
| `OmmoSensorFilter.cs` | Filtro por sensor — descarta (0,0,0), throttle, jump filter, lag de 1 amostra |
| `OmmoBootstrap.cs` | Bootstrap persistente (DontDestroyOnLoad) — `UnityMainThreadDispatcher` + `OmmoServiceLauncher` |
| `UnityMainThreadDispatcher.cs` | Singleton persistente — fila de `Action`s para despachar callbacks gRPC (threads) para o main thread Unity |

> Os scripts de diagnóstico (`OmmoUIManager`, `OmmoGridVisualizer`, `OmmoDeviceRow`, `OmmoDiagnostic`) e o legado `OmmoSIU` foram removidos no refactor do modo Gamification.

### Cenas — Build Cenas (Menu + Minijogo)

O `OmmoSceneBuilder` tem **um único** menu: **Ommo → PrevenGame → Build Cenas (Menu + Minijogo)**, que cria e regista no Build Settings duas cenas:

- **`Menu` (hub)** — Splash → Calibração → Seleção → Score, geridas pelo `GameFlowManager`. O Ommo só é necessário aqui para a calibração.
- **`MinijogoDardos`** — o mundo dos dardos (sala + alvo + dardo) com `GamificationManager` (runner) + `PauseMenu`. Cada minijogo é a sua própria cena, carregada ao entrar no jogo (melhor performance, mundo 3D isolado).

Scaffold Ommo partilhado (`ConstruirScaffoldOmmo`):
```
OmmoBootstrap  ← OmmoBootstrap (+ dispatcher + launcher) + SessionManager + CursorManager (persistentes)
AppManager     ← OmmoHardwareMonitor, OmmoDeviceManager, OmmoSensorManager [, OmmoCalibracaoManager só no Menu]
BaseStation    ← origem do espaço de tracking (invisível)
TrackedDevicePrefab_TEMP  ← prefab inativo para OmmoDeviceManager
EsqueletoJogador ← OmmoEsqueletoJogador (membro superior)
```

### Scripts de jogo (Assets/Scripts/PrevenGame/)

| Script | Função |
|--------|--------|
| `SessionManager.cs` | Singleton persistente — guarda a **calibração**, a **fila de minijogos** e os **scores** entre cenas; carrega a próxima cena |
| `GameFlowManager.cs` | Máquina de fases do hub: Splash→Calibração→Seleção→Score, com fades e helpers |
| `ScreenFader.cs` | Fade in/out full-screen (CanvasGroup) |
| `HelperDialogueManager.cs` | Personagem (Jane/Patrick por emoção) + balão + texto; linha fixa (calibração) ou sequência com clique (tutorial/score) |
| `MinigameSelectionUI.cs` / `SelectionCard.cs` | Ecrã SELECT MINI GAME — cards por exercício com reps L/R, START/EXIT |
| `MinigameController.cs` | Numa cena de minijogo: reidrata o esqueleto da calibração persistida, arranca o `GamificationManager`, regista o score e avança |
| `GamificationManager.cs` | **Runner** dos dardos — corre reps L/R de um exercício, gera a trajetória via `ExerciciosWaypoints`, lança dardos, emite `OnConcluido(pct)` |
| `ExerciciosWaypoints.cs` | Geradores dos 4 exercícios (flexão, elevação, abdução, cotovelo) → `Vector3[5]` |
| `GamificationTarget.cs` / `GamificationDart.cs` / `PrevenGameWaypoint.cs` | Alvo de 5 aros, dardo, zonas de pontuação por waypoint |
| `PauseMenu.cs` | ESC nas cenas de minijogo → overlay Continue/Main Menu/Exit Game |
| `CursorManager.cs` | Cursor do jogo (`mouse.png`), persistente |
| `OmmoCalibracaoManager.cs` | Calibração (1 sensor); arranca via `GameFlowManager`, instruções pelos helpers, grava no `SessionManager` |
| `OmmoEsqueletoJogador.cs` | Visualização do membro superior; `AplicarCalibracao(...)` reidrata sem recalibrar |
| `OmmoCameraSetup.cs` | Reposiciona a câmara relativamente ao ombro calibrado |
| `AjusteImagemBorda.cs` / `ExercicioDemoLoop.cs` / `CardAnimacaoHover.cs` | Utilidades de imagens de exercício (borda ajustada, loop, hover) |

### Loop do jogo
`Splash (tap→fade)` → `Calibração (helper guia; grava no SessionManager)` → `Seleção (tutorial do outro helper; escolhe exercícios + reps L/R; START)` → carrega `MinijogoDardos` por cada minijogo (ESC=pausa) → ao terminar volta a `Menu` em `Score` (helpers comentam) → tap → `Splash`. Já calibrado, o próximo START salta a calibração.

### Assets de UI (Assets/Prefabs/PrevenGameAssets/UIAssets/)
`firstMenu` (splash), `mainMenuBackground`, botões `start/exit/continue/mainMenu/exitGame` (+ `Hover`), `selectionCard`, `balãoDeFala`, `mouse`, helpers `Jane/` e `Patrick/` (7 emoções cada). Animações de exercício em `Exercises/<prefixo>_1..5.png` (prefixos do artista: `flexãoDoBraço`/`elevaçãoTotal`/`abduçãoLateral`/`flexãoHorizontal` → enum `FlexaoBraco`/`ElevacaoTotal`/`AbducaoLateral`/`FlexaoCotovelo`; o builder copia-as para `Assets/Resources/Exercises/<Tipo>/<n>.png` para o HudVR carregar em runtime). Fontes Poppins esperadas em `Assets/Fonts/Poppins-ExtraBold SDF.asset` e `Poppins-Medium SDF.asset` (fallback LiberationSans).

### Dados expostos por sensor

Cada pacote `TrackingDeviceData` contém:
- `Positions[]` — posição 3D em centímetros (eixos Ommo: X, Y, Z → Unity: X, Z, Y)
- `Quaternions[]` — orientação (quaternião invertido para o sistema de coordenadas Unity)
- `RawSensorData[]` — acelerómetro, giroscópio, magnetómetro brutos

Conversão de coordenadas usada em todo o código:
```csharp
// Posição
new Vector3(p.X, p.Z, p.Y) / escalaEmCM

// Rotação
Quaternion.Inverse(new Quaternion(q.X, q.Z, q.Y, q.W))
```

### Padrão de chamada gRPC

Não há cliente gRPC partilhado para chamadas unárias — cada chamada cria e destroi o seu próprio `GrpcChannel`:
```csharp
var handler = new YetAnotherHttpHandler { Http2Only = true };
var channel = GrpcChannel.ForAddress("http://localhost:50051",
                  new GrpcChannelOptions { HttpHandler = handler });
var client  = new Ommo.CoreService.CoreServiceClient(channel);
// ... chamada ...
await channel.ShutdownAsync();
```
O `Ommo.Client` singleton (em `OmmoAPI.cs`) é a excepção — mantém canal persistente para os event streams.

### Sincronização entre threads

Callbacks gRPC chegam em threads separadas. Para actualizar UI ou GameObjects, usar sempre:
```csharp
UnityMainThreadDispatcher.Enqueue(() => { /* código Unity */ });
```

## Configuração das cenas de jogo

Usar o menu **Ommo → PrevenGame → Build Cenas (Menu + Minijogo)** para criar/gravar as duas cenas automaticamente.

Para uma cena manual mínima do jogo:
1. `OmmoBootstrap` → `OmmoBootstrap` + `SessionManager` + `CursorManager`
2. `AppManager` → `OmmoHardwareMonitor` + `OmmoDeviceManager` + `OmmoSensorManager`
3. `OmmoSensorManager` referencia `DeviceManager` e `HardwareMonitor` no Inspector
4. O tracking é iniciado por `OmmoCalibracaoManager.IniciarCalibracao()` (no hub) via `IniciarTracking(1)`

### Utilizar dados dos sensores nos scripts do jogo

```csharp
// Subscrever ao estado de conectividade
void Start()
{
    var sensorMgr = FindObjectOfType<OmmoSensorManager>();
    sensorMgr.OnNumeroDeSensoresMudou += AoSensoresMudarem;
}

void AoSensoresMudarem(int count)
{
    if (count == 0) /* pausa o jogo / mostra "reconectar" */
    else            /* resume / inicia round */
}

// Verificar em qualquer momento
bool prontoParaJogar = sensorMgr.SensoresConectados;
```

## Dependências externas

| Dependência | Versão | Localização |
|-------------|--------|-------------|
| Unity | 2022.3.62f3 | — |
| Grpc.Net.Client | 2.76.0 | Assets/Packages/ |
| Google.Protobuf | 3.34.0 | Assets/Packages/ |
| YetAnotherHttpHandler | git | Packages/manifest.json |
| NuGetForUnity | — | Assets/NuGet/ (editor) |

O `Library/` não está no git — o Unity regenera-o na primeira abertura.

## Contexto técnico chave

- A integração com o **PrevenCare** (plataforma de saúde externa) ainda não está implementada — é um requisito futuro
- A interface do fisioterapeuta é um requisito futuro (a UI de diagnóstico foi removida no refactor)
- A adaptação de dificuldade deve responder aos dados do sensor dentro da mesma sessão
- O modo de fusão recomendado para reabilitação é `FullFusion` (combina IMU + magnetómetro)
- A calibração é feita **uma vez** no hub e persiste no `SessionManager`; as cenas de minijogo reidratam-na via `OmmoEsqueletoJogador.AplicarCalibracao(...)` sem recalibrar
- Escala: `UnityScaleInCM = 10` → **1 unidade Unity = 10 cm**; `BaseStation` na origem (0,13,0)
