# PrevenGameAssets — onde largar os assets

Larga os ficheiros nos caminhos abaixo e corre o menu
**`Ommo → PrevenGame → Build 3 Cenas`**. O builder carrega-os automaticamente.
Se algum faltar, é usado um **placeholder** (nada parte).

> Em alternativa, **dá-me os nomes/objetos** e eu ligo-os no código.

## Assets

| Ficheiro (caminho exato) | O que é | Onde entra | Cena |
|---|---|---|---|
| `Dardo.prefab` | Dardo lançado (modelo) | `GamificationManager.DardoPrefab` | Gamification |
| `Alvo1.prefab` … `Alvo5.prefab` | Os 5 aros (Alvo1=exterior … Alvo5=bullseye), concêntricos | `GamificationTarget.AroPrefabs` | Gamification |
| `Sala.prefab` | Ambiente/sala (substitui o fundo preto) | Instanciado na cena | Gamification |
| `CardExercicio.png` | Imagem do card de seleção | `ImagemExercicio` | Gamification |
| `Demo/1.png, 2.png, …` | Imagens da demo do exercício (loop, canto inf. esq.) | `ExercicioDemoLoop.Sprites` | Ambos |

> O **objeto controlado pelo Ommo** já NÃO é um asset: no **Clinical** é um cubo (gerado);
> na **Gamification** é o **dardo** acoplado à mão (o cubo de tracking fica escondido).

## Notas importantes

- **Aros (`Alvo1…Alvo5`)**: a pontuação (`PontoNoAro`) deriva das escalas/bounds reais dos aros —
  não precisas de alinhar nada a um raio fixo. Mantêm-se concêntricos (mesma pose autorada).
- **Dardo (`Dardo.prefab`)**: é envolvido num holder; afina a pose do modelo no
  `GamificationManager` via `OffsetDardoLocal` / `RotacaoDardoEuler` / `EscalaDardo` para a
  **ponta apontar a +Z** (sentido do voo). O brilho RimGlow é aplicado enquanto o dardo é
  controlado e em voo, e desliga-se ao cravar no alvo.
- **Demo**: numera as imagens `1.png, 2.png, …` (sem saltos) em `Demo/`. Sem esta pasta, a demo
  usa as imagens de `Assets/Execercises/ex1/` como fallback.
- **Card**: `CardExercicio.png` é usado no card da Gamification (e podes reutilizá-lo no Clinical).

## Posições a afinar em Play Mode (sem mexer no código, via Inspector)
- Posição/rotação do `Alvo` e da câmara; posição/escala da `Sala`.
- `GamificationManager.OffsetDardoLocal / RotacaoDardoEuler / EscalaDardo` (alinhar a ponta do dardo).
- `OmmoEsqueletoJogador.EixoAntebracoLocal` (eixo do cotovelo/antebraço conforme a montagem do sensor).
