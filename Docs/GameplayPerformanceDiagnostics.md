# Captura de desempenho de gameplay

Use `Ctrl+Shift+F` para iniciar/encerrar a captura no Play Mode ou no Player.
O atalho funciona em `BattleMode_*` e `Stage_*`; cenas de menu são identificadas
como `OutsideGameplay`. VSync, qualidade e limite de FPS não são alterados.

## Leitura dos resultados

- `[BattlePerf]` resume aproximadamente dois segundos de frames concluídos.
  Em taxas muito altas, o limite de 1024 amostras antecipa o relatório.
  `p95`/`p99` usam o percentil por posição (nearest rank); `over16.67` conta
  frames acima do orçamento de 60 FPS. A média de FPS sozinha não comprova estabilidade.
- `SLOW` detalha até três frames de gameplay com duração de pelo menos 20 ms.
  Inclui main thread, alocações em KB, scripts compartilhados, fases COM e
  estado observado no LateUpdate daquele frame. `stateAvailable=False` sinaliza
  ausência desse estado, sem remover a travada das estatísticas.
- `HOT` mostra até três chamadas de pelo menos 0,5 ms em cada frame `SLOW`,
  com método, jogador e alocações da thread principal durante a chamada.
  Os custos são inclusivos: um candidato pode incluir as buscas de caminho
  listadas abaixo dele. Os wrappers gerais `Think`/`BuildCandidates` não
  disputam essas posições. `SceneQueries` mede buscas globais de itens/montarias.
- Os recorders permanecem ativos entre coletas, para preservar `Main Thread`
  e o trabalho de `FixedUpdate` anterior ao Update do diagnóstico. `mainMs=0`
  é tratado como indisponível; não representa uma main thread sem custo.
  `gcCollectMs`, `playerLoopMs`, `editorLoopMs` e `presentWaitMs` são contadores
  adicionais quando disponíveis, para investigar tempo fora dos scripts COM.
- `wallGap` é o intervalo real entre duas coletas. Pode diferir do tempo de
  frame do Unity porque inclui trabalho anterior ao Update da coleta seguinte.
  Não deve ser somado aos tempos dos scripts.
- Cada fase mostra `totalMs/maxCallMs/calls`. Categorias diferentes são
  inclusivas: `ComUpdate` contém `ComThink`, que pode conter `Candidates`,
  `Pathfinding` e `Danger`. Não somar essas categorias. Chamadas aninhadas da
  mesma categoria contam como chamadas, mas não duplicam seu tempo acumulado.
- `Capture(allFrames)` e `Report(allFrames)` medem coleta e emissão do log,
  inclusive durante interrupções. O custo de um relatório aparece na janela
  seguinte, pois ele pertence ao frame em que foi emitido. A instrumentação
  detalhada também tem custo: comparar capturas ligadas/desligadas.
- `N/A` indica contador indisponível. Um marcador de script válido sem chamadas
  no frame vale zero. `gcKB` mede alocação, não duração de uma coleta de lixo.
- `INTERRUPTION` registra frames, duração real e motivo confirmado: perda de
  foco, pausa do aplicativo/Editor, troca de cena ou `timeScale=0`. `RESUME`
  informa a retomada mesmo se ela cair na janela seguinte. Esses intervalos
  ficam separados das estatísticas de gameplay. Um pico grande sem motivo
  confirmado permanece no resumo e nos candidatos a `SLOW`.
- A inicialização da captura e seu primeiro frame parcial são descartados.
  Os relatórios seguintes usam buffers reutilizáveis e uma entrada de Console
  por janela, sem stack trace. Não há busca global de entidades por relatório.

## Comparação manual pendente

1. Registrar a revisão/configuração de referência. Usar o mesmo PC, resolução,
   VSync, foco na Game View e configurações do Editor. Evitar Deep Profile e
   alterações no projeto durante as medições. A linha inicial registra o ambiente.
2. Comparar três partidas de pelo menos dois minutos antes/depois em
   `BattleMode_1`, seis COMs Hard, com os mesmos itens e regras. Manter todas
   as janelas; não escolher somente os melhores resultados. Incluir explosões
   encadeadas, montarias, itens, mortes, Sudden Death e recarga de rodada.
3. Repetir em `Stage_*`, com um jogador e multiplayer. Conferir movimento,
   entradas humanas, bombas, HUD e animações. Comparar captura ligada/desligada.
4. Pausar/retomar o Editor e o jogo, alternar foco e trocar de cena. Verificar
   `INTERRUPTION`/`RESUME`; uma travada espontânea não pode desaparecer.
5. Após coletar/perder habilidades, montar/desmontar e reiniciar uma rodada,
   verificar fuga de explosões e Sudden Death: os colliders e as habilidades
   devem continuar atualizados. Conferir também Easy/Normal e troca Man/COM.
6. Avaliar p99 contra 16,67 ms e investigar cada pico de gameplay. Não declarar
   60 FPS estáveis apenas porque `fpsAvg` ultrapassa 60. Comparar também os
   tempos COM e alocações por frame.

Os testes direcionados estão em `BattleModeComEditModeTests`: reset/isolamento
das configurações, percentis e limites do buffer, preservação de travadas,
interrupções e escopos aninhados. Executar os testes Unity e compilar somente
quando solicitado. A revisão estática não substitui essas medições.
