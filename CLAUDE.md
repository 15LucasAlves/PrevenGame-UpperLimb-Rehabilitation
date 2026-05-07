# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projeto

**PrevenGame** — Serious game adaptativo para reabilitação do membro superior.
Projeto final da Licenciatura em Engenharia de Desenvolvimento de Jogos Digitais (IPCA).

- **Estudante:** Lucas Ferreira Alves (nº 27922) — a27922@alunos.ipca.pt
- **Orientadores:** Pedro Lobo (pjlobo@ipca.pt) e João L. Vilaça (jvilaca@ipca.pt)

**Objetivo:** Jogo sério que transforma exercícios de fisioterapia em desafios orientados a tarefas do dia-a-dia, com dificuldade adaptada em tempo real a partir dos dados do sensor Ommo. Inclui interface para o fisioterapeuta monitorizar o progresso do paciente.

## Idioma

Todo o código, comentários e documentação deste repositório estão em **Português de Portugal**.

## Estrutura do Repositório

```
PrevenGame/          # Projeto Unity (Unity 2022.3.62f3)
  Assets/
    Scripts/
      Ommo/          # SDK Ommo — não editar (código de terceiros)
      PrevenGame/    # Scripts do jogo — aqui é desenvolvido o jogo
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

### Scripts Ommo (Assets/Scripts/Ommo/) — não editar

| Script | Função |
|--------|--------|
| `OmmoAPI.cs` | Singleton `Ommo.Client` — gere o canal gRPC e o event stream de dispositivos |
| `OmmoServiceLauncher.cs` | Lança/mata o `ommo_service_v0.22.0.exe`; emite `OnServiceReady` |
| `OmmoServiceApi.cs` | Classes geradas por Protobuf (tipos de dados da API) |
| `OmmoServiceApiGrpc.cs` | Stubs gRPC gerados |
| `OmmoDevice.cs` | MonoBehaviour que lê dados de um sensor e actualiza GameObjects filhos |
| `OmmoDeviceManager.cs` | Instancia/destrói `OmmoDevice`s automaticamente quando sensores ligam/desligam |
| `OmmoHardwareMonitor.cs` | Estado da base station, bateria e canais wireless |

### Dados expostos por sensor

Cada pacote `TrackingDeviceData` contém:
- `Positions[]` — posição 3D em centímetros (eixos Ommo: X, Y, Z → Unity: X, Z, Y)
- `Quaternions[]` — orientação (quaternião invertido para o sistema de coordenadas Unity)
- `RawSensorData[]` — acelerómetro, giroscópio, magnetómetro brutos
- `Buttons[]`, `BatteryState`, indicadores de qualidade

Conversão de coordenadas usada em todo o código:
```csharp
// Posição
new Vector3(p.X, p.Z, p.Y) / escalaEmCM

// Rotação
Quaternion.Inverse(new Quaternion(q.X, q.Z, q.Y, q.W))
```

### Script do jogo (Assets/Scripts/PrevenGame/)

**`OmmoObjectController.cs`** — liga qualquer GameObject ao sensor. Configurável no Inspector:
- `AlvoDoControlo` — Transform a mover/rodar (null = o próprio GameObject)
- `SiuUuid` / `PortId` — identificam o sensor (0 = primeiro disponível)
- `ControlarPosicao` / `ControlarRotacao` — activar independentemente
- `EscalaCM` — centímetros por unidade Unity (padrão: 10)
- `SuavizacaoPosicao` / `SuavizacaoRotacao` — Lerp/Slerp em Update
- `Calibrar()` — pode ser chamado por código para zerar a origem

## Configuração mínima de uma scene

1. GameObject vazio → `OmmoServiceLauncher` (aponta para `Assets/Plugins/ommo.sdk/ommo_service_v0.22.0.exe` automaticamente)
2. GameObject alvo → `OmmoObjectController`

Para múltiplos sensores usar `OmmoDeviceManager` em vez de `OmmoObjectController`.

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
