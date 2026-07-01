# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projeto

**PrevenGame** — Serious game adaptativo para reabilitação do membro superior.
Projeto final da Licenciatura em Engenharia de Desenvolvimento de Jogos Digitais (IPCA).

- **Estudante:** Lucas Ferreira Alves (nº 27922) — a27922@alunos.ipca.pt
- **Orientadores:** Pedro Lobo (pjlobo@ipca.pt) e João L. Vilaça (jvilaca@ipca.pt)

**Objetivo:** Jogo sério que transforma exercícios de fisioterapia em desafios orientados a tarefas do dia-a-dia, com dificuldade adaptada em tempo real a partir dos dados do sensor Ommo. Inclui interface para o fisioterapeuta monitorizar o progresso do paciente.

**Estado atual:** A camada de integração Ommo (sensor → Unity) está completa. A lógica do jogo PrevenGame ainda não foi implementada.

## Idioma

Todo o código, comentários e documentação deste repositório estão em **Português de Portugal**.

## Estrutura do Repositório

```
PrevenGameProject/   # Projeto Unity (Unity 2022.3.62f3)
  Assets/
    Scripts/         # Scripts organizados em duas camadas:
      Ommo/          #   Integração sensor→Unity (SDK, gRPC, hardware, diagnóstico)
        Editor/      #     OmmoPairingHelper (tool de emparelhamento SIU)
      PrevenGame/    #   Lógica do jogo (managers, waypoints, gamification, menu, calibração, esqueleto)
        Editor/      #     OmmoSceneBuilder (construção automática das cenas)
    Prefabs/         # OmmoDevice.prefab, SensorPrefab.prefab, *_TEMP.prefab
    Scenes/          # MainMenu.unity, ClinicalTrial.unity, Gamification.unity
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
| `OmmoSIU.cs` | Alternativa legada — usa `StreamDataFrame` (todos os dispositivos num único stream) em vez de streams individuais por dispositivo |
| `OmmoHardwareMonitor.cs` | Polling a cada 1.5s ao `GetHardwareStates`; expõe `ServiceInfo` com listas Connected/Disconnected/Blocked; emite `OnHardwareUpdated` |
| `OmmoSensorManager.cs` | **Gestor de hardware para o jogo** — inicia tracking automaticamente ao `OnServiceReady`, para o motor em segurança, expõe `SensoresConectados`, `NumeroSensores` e evento `OnNumeroDeSensoresMudou` |
| `OmmoUIManager.cs` | Painel de diagnóstico/fisioterapeuta — hardware panel + 3D grid view; **não é usado na cena do jogo** mas preserva `StartTracking`/`StopTracking`/`StopBaseStationMotor` para uso futuro na interface do fisioterapeuta |
| `OmmoGridVisualizer.cs` | Camera component — grelha 3D wireframe via GL + marcadores de sensores (diagnóstico) |
| `OmmoDeviceRow.cs` | Linha de UI do hardware panel: botões Start/Stop Motor e Block/Unblock SIU |
| `OmmoDiagnostic.cs` | Script de diagnóstico — imprime no Console dispositivos conectados e instâncias `OmmoDevice` |
| `UnityMainThreadDispatcher.cs` | Singleton persistente — fila de `Action`s para despachar callbacks gRPC (threads) para o main thread Unity |

### Hierarquias de GameObjects — Build 3 Cenas

O `OmmoSceneBuilder` tem **um único** menu: **Ommo → PrevenGame → Build 3 Cenas (Menu + Clinical + Gamification)**, que cria e regista no Build Settings as três cenas (`MainMenu`, `ClinicalTrial`, `Gamification`). O scaffold Ommo partilhado:
```
OmmoBootstrap  ← UnityMainThreadDispatcher + OmmoServiceLauncher persistentes entre cenas
AppManager     ← OmmoHardwareMonitor, OmmoDeviceManager, OmmoSensorManager, OmmoCalibracaoManager
BaseStation    ← origem do espaço de tracking (invisível na Gamification)
TrackedDevicePrefab_TEMP  ← prefab inativo para OmmoDeviceManager
EsqueletoJogador ← OmmoEsqueletoJogador (visualização do membro superior)
```
`OmmoSensorManager` inicia o tracking automaticamente; a calibração corre antes do jogo.

> Os builders standalone antigos (`Build Scene (Jogo)`/`(Diagnóstico)`) e os scripts de diagnóstico de cena foram removidos — o workflow é só `Build 3 Cenas`.

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

## Configuração de uma scene de jogo

Usar o menu **Ommo → PrevenGame → Build 3 Cenas** para criar/gravar as três cenas automaticamente.

Para uma cena manual mínima do jogo:
1. `AppManager` vazio → `OmmoServiceLauncher` + `UnityMainThreadDispatcher`
2. `AppManager` → `OmmoHardwareMonitor` + `OmmoDeviceManager` + `OmmoSensorManager`
3. `OmmoSensorManager` referencia `DeviceManager` e `HardwareMonitor` no Inspector
4. O tracking inicia automaticamente — não é necessário chamar `StartTracking()` manualmente

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
- A interface do fisioterapeuta é uma funcionalidade de primeira classe, não um extra
- A adaptação de dificuldade deve responder aos dados do sensor dentro da mesma sessão
- O modo de fusão recomendado para reabilitação é `FullFusion` (combina IMU + magnetómetro)
- `OmmoSIU.cs` é código legado — preferir `OmmoDevice` + `OmmoDeviceManager` para novos scripts
