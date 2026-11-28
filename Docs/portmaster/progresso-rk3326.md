# PortMaster no RK3326 — checkpoint de desempenho

## Critério de conclusão

Validar no portátil físico título, menus, seleção de personagem quente e fria, World Map, entrada na fase, campanha e BattleMode_1 com um jogador e cinco COM Hard ativos. Preservar resolução nativa do painel (1024×768 no R36H Pro Max observado), áudio, controles, regras e saves. Registrar a duração, número de quadros, FPS, média, p95, p99, pior quadro e quadros acima de 16,67 ms por cenário. Separar carregamento, pausa e gameplay sem ocultar a duração das transições. Medir com e sem telemetria pesada. Objetivo aberto até confirmar 60 FPS reais. Registrar separadamente o R36S caso seja medido.

## Evidência de partida

- O pacote usa o APK IL2CPP ARM64 com ua_host64. Toque oculto e áudio via pipe confirmados pelo jogador. Perfil nativo P1, layout Nintendo e atalho de captura L3 + R3 + Select no launcher de diagnóstico.
- Build inicial rodou a aproximadamente 20,3 FPS na intro. Com pedido de 60 FPS observou-se um patamar próximo de 30,5 FPS num painel de 61 Hz. Reduzir para 512×384 piorou a aparência sem eliminar o patamar. O swap interval zero também não eliminou o patamar. Retomada em resolução nativa.
- A captura física arquivada em `Build/PortMaster/captura-20261124-194925` identifica BattleMode_1, seis jogadores e cinco COM. Cinco janelas de gameplay entre 6,3 e 10,5 FPS. Na última, 13 quadros, 146,28 ms por quadro, 82,98 ms de ComUpdate e 58,96 ms de ComThink por quadro. Fases inclusivas. Não somar custos aninhados.
- Build de seleção em `Build/PortMasterPlayers/SB6-selection.apk`. Removidos flush global do Canvas por quadro, atualização redundante da lista e carregamento de frames para prévias invisíveis. Cache de retratos e frames com chave de valor. O jogador ainda relata travamento. Baseline Unity anterior: 1187/1191; falhas do navegador de salas, retorno do lobby, introdução do Clown e cancelamento do minecart. Python: 16 + 219 aprovados.
- Em 24/11 na retomada, F: não apareceu entre os volumes montados. Não usar a captura de 19h49 como evidência do último teste. Conferir rótulo EASYROMS e arquivar log.txt/performance.txt quando o volume retornar.

## Matriz de trabalho

| ID | Trabalho e owner | Estado | Evidência necessária para fechar |
| --- | --- | --- | --- |
| PM01 | Runtime, áudio, controles, viewport | Parcial físico | Confirmar navegação, ações, pausa e saída nas telas afetadas com a build final |
| PM02 | Sandbox PC ARM64 com 4 CPUs, 1 GiB, resolução original e saves isolados | Boot confirmado no PC (79 frames) | Executar o mesmo APK/host, obter frames, alimentar sequência de entrada e comparar imagem/contagem de operações. Mesa software e QEMU não reproduzem tempos Mali/microSD |
| PM03 | Título e cadência de apresentação | Aberto | Registrar atualização solicitada e efetiva, Swappy, tempo de render e de apresentação; identificar por que 61 Hz entrega ~30,5 |
| PM04 | SkinSelect e retratos | Primeira redução entregue; relato físico ainda ruim | Instrumentar troca fria/quente, custo de LoadAll, atualização de UI e primeiro frame visível. Avaliar atlas de prévias ou paleta indexada URP antes de remover sheets; validar sprites, HUD, efeitos, montarias e personagens com paletas próprias |
| PM05 | WorldMap e início de fase | Aberto | Cronometrar confirmação, início/fim do fade de 1 s, carga/ativação da cena e primeiro input. Sobrepor carga offline ao fade preservando rota do servidor |
| PM06 | COM Hard: custo de perigo por tile | Mudança em fonte | Cache por tile durante uma única decisão síncrona; perigo de bomba hipotética permanece específico da busca. Validar expiração entre decisões, habilidades, dano e explosões e comparar contagem de queries |
| PM07 | COM Hard: coleta, busca de rotas e referências | Aberto | Medir consultas à cena e expansões repetidas, reusar resultados apenas se obstáculos e habilidades forem os mesmos, manter prioridades e RNG do Hard |
| PM08 | Gameplay fora de ComUpdate | Aberto | Capturar intervalo restante da main thread, FixedUpdates acumulados, animações, HUD e física na campanha e Battle |
| PM09 | Regressões e entrega | Pendente | Build fonte/DLL/APK/protocolo identificados, EditMode completo, ambas as suítes Python, offline, servidor e dois clientes, captura física no aparelho |

## Como retomar

1. Ler este arquivo, git status, os logs arquivados e a matriz de validação offline/online antes de editar. Copiar nova evidência do cartão sem tocar nos saves.
2. Rodar o sandbox a partir da raiz: `python Tools/PortMaster/sandbox.py --game Build/PortMasterPlayers/selection-game --host Build/PortMaster/superbomberman6/ua_host64 --output Build/PortMaster/Sandbox --seconds 120`. Requer Docker Linux e emulação ARM64 no host x86. Cada execução cria um diretório próprio. Identificar APK e host antes de comparações.
3. Priorizar o maior custo medido de cada cenário. Anexar a cada rodada: ID, alteração, revisão de fonte, hash do APK/host, firmware, resolução, carga, resultado e falhas. Um novo APK exige repetir a extração das bibliotecas e dos metadados correspondentes.
4. Preservar dados brutos e arquivo de rollback. Repetir a comparação final no aparelho, em resolução nativa e com os cinco COM Hard, antes de marcar PM09 concluído.

## Execução do sandbox confirmada

O APK da build de seleção e ua_host64 iniciaram no PC em Linux ARM64, com teto de quatro CPUs e 1 GiB. Unity encerrou após 79 quadros. Log em Build/PortMaster/Sandbox/run-12uuax94/sandbox.log. A janela offscreen usa 1024×768, dimensão do R36H Pro Max medido. A execução valida inicialização e entrega de quadros. Menus, arena e equivalência visual ainda não foram validados nessa sessão.

## Intro — teste de cadência e áudio

Captura física em `Build/PortMaster/intro-20261125-211717`: TitleScreen a 30,5 FPS no painel de 61 Hz, quadro de 1476,7 ms aos 11,4 s. O launcher do cartão pede 61 FPS para verificar a hipótese de arredondamento por divisor. Manter a resolução nativa. No projeto, somente o tema do título ganha override Android Compressed In Memory e preload, para tirar a descompressão integral do primeiro Play. A mudança de áudio precisa de novo APK. Medição comparativa pendente.

## Rodada BattleMode_1 — cinco COM, cartão novo de 25/11

Evidência em `Build/PortMaster/cinco-com-20261125-230052`. Título com 59,8–60 FPS em janelas da intro e pausa de 1624,5 ms aos 11 s. Ao entrar na arena, dois intervalos de 17983,7 e 6830,2 ms. Na última janela: 26 quadros, 6 jogadores, 5 COM, 13,2 FPS, média de 75,641 ms, p95 de 100 ms. CPU de COM: 38,53 ms/quadro; referências: 10,16 ms; consultas: 7,61 ms (fases inclusivas). Alterações em avaliação: uma varredura compartilhada por tipo e quadro com invalidação por ciclo de vida; perigo por tile por decisão; validação dos modos Man/COM/Off sem reflexão; índice dos renderers reconstruído por aplicação de skin. Manter preset do título em 61, resolução nativa, mesma arena, cinco COM e HUD de diagnóstico para comparar. Medição física desta revisão pendente.

Build Android 59ba6d05 testada em compilação e instalada no cartão; SHA-256 do APK 845c0e1755b2d6833f27ec536dbbb6db17ec30c65868b7992bcd89b875a582e8. Seis arquivos verificados; save preservado. Build: zero erros, sete avisos. Python: 235 aprovados. EditMode: 1186/1192, relatório em D:/sb6-tools/portmaster-five-com/recheck.xml. A/B de desempenho físico e sessão dedicada com dois clientes ainda pendentes.
## Rodada de 26/11 — seleção e BattleMode_1 com cinco COM

O teste físico da versão anterior registrou janelas de 13,6 a 19,5 FPS na arena. O quadro 1740 gastou 37,9 ms com os cinco COM. A coleta de itens de P2 e P4 somou 14,1 ms nesse quadro. O jogador confirmou que o título ficou suave.

Na revisão de build eedd0896, cada COM reutiliza as consultas de colisão da casa dentro de uma decisão e a descoberta de habilidades dentro do quadro se a versão do AbilitySystem continuar igual. A seleção carrega folhas compactas com os quadros de caminhada e confirmação. As 313 paletas incluem 3.707 quadros verificados pixel a pixel. As folhas completas continuam disponíveis para quadros personalizados. Para regenerar as prévias depois de editar skins, executar `python Tools/PortMaster/generate_selection_previews.py` com Pillow instalado.

APK Android ARM64 para o PortMaster com SHA-256 `70e0ce6b4e26012bdb3797c8208436e9d794ce2f77ed91852277e26b61577fd2`. Evidências, cópia de rollback e manifesto da instalação em `Build/PortMaster/rodada-20261126-000129`. Compilação C# sem erros ou avisos. EditMode 1186/1192 com as mesmas seis falhas da rodada anterior. Python 235/235. Validar no aparelho a primeira troca de cada paleta, confirmação, início de partida e pelo menos um minuto na primeira arena com cinco COM Hard. Registrar tempo médio e p95 por janela e comparar o custo de pathfinding e de referências. Meta física de 60 FPS pendente.

## Arena — revisão de empacotamento após a seleção ficar suave

O último teste em BattleMode_1 registrou pausas de 15,84 s na troca de cena e 6,34 s antes de aparecerem os seis jogadores. Na última janela de 36 quadros, a média foi 18,8 FPS e o p95 foi 66,7 ms. Os cinco COM juntos consumiram 24,8 ms por quadro nessa janela, incluindo instrumentação. O diagnóstico de movimento gravou 2.502 linhas durante a execução.

O pacote exclusivo do ua_host64 deixa a captura de movimento desligada por padrão. O log de FPS continua disponível. Os 111 arquivos de cena e dados compartilhados anteriormente compactados usam armazenamento direto no ZIP alinhado. As 15 músicas de batalha mantêm o áudio Vorbis comprimido em memória e usam os mesmos dados de áudio. O contêiner ocupa mais 33,8 MiB. Gerar a partir do APK Android original com Tools/PortMaster/prepare_runtime_apk.py (UnityPy), seguido de zipalign -P 16 4. Usar o contêiner resultante apenas no port. O APK Android assinado permanece como fonte.

Os dados brutos, cópia anterior, manifesto e logs de inicialização no sandbox ficam em Build/PortMaster/arena-20261126-063323. Repetir BattleMode_1 com cinco COM Hard e medir o tempo da confirmação da arena ao primeiro movimento, fpsAvg, p95 e ComUpdate por quadro. Se o COM continuar acima de 16,7 ms sozinho, priorizar as varreduras de TankShot em BattleModeComTankThreatAwarenessAbility e as reavaliações por quadro em perigo. Validar reação a uma nova ameaça, fuga e montarias antes de mudar a frequência de decisão.

## Perfil do portátil após restaurar o save

Revisão de build 6b991e42, com manifesto e backup local em Build/PortMaster/reset-defaults. SHA-256 do APK instalado: c7e0a244c1305c47a2074e712c47e403a8f401ed66e572f0e5f9f018536f1ced. O launcher identifica o portátil fora de SaveData. O perfil P1 padrão usa o direcional e os índices A=1, B=0, ação C=2, L=4, R=5, Start=8, Select=9. O menu de controles permite segurar B durante o remapeamento para cancelar ao soltar. Vídeo está disponível e o bootstrap dos controles touch é dispensado nesse ambiente. O modal de exclusão começa em Cancelar e informa sobre os slots, desbloqueios, conquistas, recordes e configurações atingidos.

Validação: build Android bem-sucedida, compilação C# sem erros ou avisos, 235 testes Python aprovados, 1187/1193 no EditMode (as mesmas seis falhas da rodada anterior). O novo teste de restauração do perfil passou. Inicialização confirmada em ARM64 emulado. Verificação visual e navegação de ponta a ponta no portátil pendentes. Para verificar o diálogo sem perder progresso, abrir Opções, escolher a opção de apagar o save e voltar com B enquanto Cancelar estiver selecionado.

## Rodada de vídeo, touch e primeira arena

Captura em Build/PortMaster/video-20261127-231553. Considerando somente as janelas da primeira arena com seis jogadores e cinco COM, houve 316 quadros de gameplay. Média ponderada de 43,72 ms por quadro (22,87 FPS), dos quais 18,69 ms em ComUpdate. As fases são inclusivas: Think 6,85 ms, referências 3,37 ms, candidatos 3,93 ms e consultas de cena 2,34 ms. A última janela do log já tinha três COM. Para comparar, manter o mesmo número de jogadores vivos.

No APK, o menu de vídeo usa a superfície nativa e muda o retângulo compartilhado por câmera e UI. O modo de janela limita a apresentação ao multiplicador escolhido. O modal de reset usa o centro do quadro da interface. No portátil o touch inicia oculto em save novo, pode ser reativado nos controles e o launcher não deve sobrescrever a preferência. Os COM compartilham os projéteis de tanque encontrados no quadro com invalidação ao ativar ou desativar um projétil. Confirmar o ganho no portátil; a meta é 16,67 ms por quadro.
