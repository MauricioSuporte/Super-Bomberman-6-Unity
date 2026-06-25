# Normal Game AI (dev-only) — IA para P2/P3/P4

IA simplificada para controlar players no **Normal Game** e gravar gameplays de
4 jogadores estando sozinho. É **opcional e removível do build final**.

## O que ela faz

- Planta bomba para **farmar** (destruir blocos `Destructibles`).
- **Coleta itens** (Layer `Item`) indo até eles.
- **Foge** de bombas, explosões e inimigos (Layer `Enemy`) para não morrer.
- Planta bomba a **distância segura** para matar inimigos **sem encostar** neles.

Controla o player injetando *input sintético* no `PlayerInputManager` (o mesmo
mecanismo do `BattleModeComController`). Não troca o `MovementController` nem
mexe no prefab do player.

## Arquivos

- `NormalGameAIController.cs` — a IA por player (toda sob `#if ENABLE_NORMAL_GAME_AI`).
- `NormalGameAIManager.cs` — carrega-se sozinho em todas as cenas e anexa a IA
  aos players escolhidos. **Não precisa adicioná-lo a nenhuma cena.**

## Como ATIVAR (para gravar)

1. **Adicionar o define (uma vez):** `Edit > Project Settings > Player > Other
   Settings > Scripting Define Symbols` → adicionar `ENABLE_NORMAL_GAME_AI` →
   Apply. (Espere o Unity recompilar.)
2. **Jogar:** dê Play em qualquer cena de Normal Game (`Stage_*`). Se a partida
   estiver com só 1 jogador, **P2/P3/P4 são spawnados automaticamente** e a IA
   assume os três; você controla P1.

> **Spawn automático:** com o define ativo e `EnableAI = true`, o `PlayersSpawner`
> completa a partida para 4 jogadores quando há apenas P1 (não afeta Battle Mode
> nem Boss Rush). O `GameSession` é sincronizado, então HUD e condições de jogo
> enxergam os 4. Se `EnableAI = false`, nada é spawnado e a partida segue normal.

Para ligar/desligar rapidamente, mude o bool no topo de `NormalGameAIManager.cs`:

```csharp
public static bool EnableAI = true;   // true = IA ligada | false = desligada
```

Esse bool **não é serializado** (não aparece no Inspector); o valor vem do
código e pode ser alterado até em runtime.

## Configuração (no topo de NormalGameAIManager.cs)

- **EnableAI** — liga/desliga a IA (remove dos players quando desligado).
- **AiPlayerIds** — quais players a IA controla (padrão `{ 2, 3, 4 }`).
- **FarmDestructibles / HuntEnemies / CollectItems** — liga cada comportamento.
- **RescanInterval** — frequência (s) de procurar players novos para anexar.
- **DebugLogs** — logs no Console.

Ajustes finos ficam no `NormalGameAIController` (think rate, raio de fuga de
inimigos `Enemy Touch Radius Tiles`, profundidade de busca, cooldown de bomba etc.).

## Como REMOVER do build final (distribuição)

Basta **remover o define** `ENABLE_NORMAL_GAME_AI` das Scripting Define Symbols.
Sem ele, toda a lógica da IA deixa de ser compilada, o auto-carregamento não
acontece e **nada vai para o build**. Não há GameObject para limpar na cena.

## Notas

- É uma ferramenta **dev-only**: prioriza sobreviver e farmar com segurança, não
  joga de forma "perfeita".
- Se a IA for cautelosa demais perto de inimigos, baixe `Enemy Touch Radius Tiles`;
  se morrer encostando em inimigos, aumente para 2.
- Performance: o BFS de fuga/navegação roda por player. Com muitos players e
  inimigos, aumente `Think Interval Safe`/`Danger` se notar queda de FPS.
- O Unity gera os arquivos `.meta` ao abrir o projeto. Não foram criados à mão.
