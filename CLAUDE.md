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
    Scripts/         # Todos os scripts (estrutura plana, sem subdirectorias)
    Prefabs/         # OmmoDevice.prefab, SensorPrefab.prefab, *_TEMP.prefab
    Scenes/          # SampleScene.unity, SampleScene/DeviceScene.unity, SampleScene/SiuScene.unity
    Plugins/
      ommo.sdk/      # Binários do serviço Ommo (ommo_service_v0.22.0.exe, DLLs Qt)
      Cysharp.Net.Http.YetAnotherHttpHandler.Native/  # DLL nativa gRPC HTTP/2
    Packages/        # Packages NuGet (Grpc, Google.Protobuf, Microsoft.Extensions)
    NuGet/           # Plugin NuGetForUnity (editor only)
    Editor/          # Scripts de editor (OmmoSceneBuilder)
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

### Scripts de integração Ommo (Assets/Scripts/)

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
| `OmmoUIManager.cs` | Gere os dois painéis: Hardware Panel (MainCanvas) e 3D Grid View (HUDCanvas + GridCamera); chama `DeviceManager.StartTracking()` ao abrir a vista 3D |
| `OmmoGridVisualizer.cs` | Camera component — desenha grelha 3D wireframe via GL e marcadores dos sensores em tempo real |
| `OmmoDeviceRow.cs` | Linha de UI para um dispositivo; botões para Start/Stop Motor (Base Station) e Block/Unblock (SIU wireless) |
| `OmmoDiagnostic.cs` | Script de diagnóstico — imprime no Console os dispositivos conectados e instâncias `OmmoDevice` (usado em desenvolvimento) |
| `UnityMainThreadDispatcher.cs` | Singleton persistente — fila de `Action`s para despachar callbacks gRPC (threads) para o main thread Unity |

### Hierarquia de GameObjects na cena

O `OmmoSceneBuilder` (menu **Ommo → Build Scene**) constrói automaticamente:

```
AppManager                  ← UnityMainThreadDispatcher, OmmoServiceLauncher,
                               OmmoHardwareMonitor, OmmoDeviceManager, OmmoUIManager
BaseStation                 ← origem do espaço de tracking (posição 0,0,0)
MainCanvas                  ← Hardware Panel UI (lista Connected/Disconnected/Blocked)
HUDCanvas                   ← Overlay 3D com dados de sensores em tempo real
GridCamera                  ← Camera + OmmoGridVisualizer (grelha wireframe + marcadores)
TrackedDevicePrefab_TEMP    ← prefab inativo usado pelo OmmoDeviceManager
DeviceRowPrefab_TEMP        ← prefab inativo para linhas do Hardware Panel
```

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

## Configuração mínima de uma scene

Usar o menu **Ommo → Build Scene** para criar toda a cena automaticamente.

Para uma cena manual mínima:
1. `AppManager` vazio → `OmmoServiceLauncher` + `UnityMainThreadDispatcher`
2. `AppManager` → `OmmoDeviceManager` (aponta `BaseStation`, define `DeviceTypePrefabs`)
3. `OmmoDeviceManager.StartTracking()` é chamado explicitamente (não no `Start`)

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
