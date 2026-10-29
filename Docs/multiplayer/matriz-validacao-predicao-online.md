# Matriz de validação manual da predição online

Esta matriz complementa o [checklist de desenvolvimento](progresso-predicao-online.md)
e o [plano completo](plano-aplicacao-predicao-online.md). Ela define a validação
manual ainda necessária; **nenhum cenário desta matriz foi executado entre dois
peers nesta retomada**. Criar o roteiro não conclui um ID nem comprova ganho.

## Preparação e evidência

1. Registrar build e protocolo do offline, servidor e clients. Confirmar a
   publicação no checkpoint/pipeline; commit enviado não comprova deploy.
2. Repetir mapa, loadout, direção e sequência de ações no offline e no online.
   Usar host, owner e observador; inverter os jogadores e repetir com dois
   clients humanos em servidor dedicado quando disponível. COM é caso adicional.
3. Cobrir início, continuação e término; depois pausa/retomada, morte, troca de
   posse/montaria, desconexão e nova rodada/cena conforme o mecanismo permitir.
4. Capturar JSONL com o diagnóstico existente nos peers envolvidos. Guardar os
   cabeçalhos, protocolo/build, cenário, papel e condições de rede. Para rede
   degradada, registrar a configuração de um ambiente de teste controlado, se
   disponível; não alterar o transporte da homologação para simular perda.
5. Correlacionar tick da ação, netId e revisão quando presentes. Medir durações
   dentro do mesmo relógio; não subtrair UTC de máquinas distintas como medida
   de input lag. Diferenciar espera de confirmação, correção visual e frame hitch.

Registrar cada execução com ID, builds, cenário, papéis, entrada, resultado,
arquivos de evidência e falha observada. Uma evidência que falta continua pendente.
Testes EditMode e compilação não substituem esta matriz.

O pacote Windows inclui `Start-Online-Diagnostics.cmd`; o runtime também aceita
`-gameplay-latency-logs` ou `-online-motion-logs`. JSONL fica em
`Application.persistentDataPath/Diagnostics/Movement`. A captura para após10 minutos
ou8MiB e tem orçamento de180 eventos/segundo; conferir `capture_limit`/`dropped`
antes de interpretar uma ausência como falha de gameplay. Para um teste novo,
iniciar uma captura nova com o mecanismo existente de ativação/reinício do jogo.

O comparador existente aceita mais de um arquivo online, inclusive o servidor;
mantém separados build, protocolo, papel, player e captura:

```powershell
python Tools/Diagnostics/analyze_prediction.py --online client.jsonl server.jsonl --offline offline.jsonl --output comparacao.json
```

Os nomes acima são exemplos. O resumo conta os eventos novos de SuddenDeath;
ele não calcula automaticamente latência dessa habilidade nem substitui a
correlação de células/hash e durações descrita abaixo.

## Cenários por ID

Todos os critérios abaixo aguardam comparação manual. IDs parcialmente
implementados também têm lacunas de código listadas no checkpoint; o roteiro
não autoriza considerar essas lacunas resolvidas.

| ID | Cenário mínimo | Critério observável |
| --- | --- | --- |
| P0 | Capturas offline/online comparáveis; início/fim de ação e pausa | Eventos correlacionáveis, esperas intencionais separadas de travamentos e nenhuma latência inferida de ação não instrumentada. |
| A01 | Caminhar montado em quatro direções, curvas e paredes | Resposta local imediata onde elegível; replay conserva contexto e observador acompanha a montaria. |
| A02 | Plantar, esgotar capacidade, ocupar célula e rejeitar pedido | Proxy confirma ou desaparece sem bomba duplicada; estoque e bloqueio virtual são liberados. |
| A03 | Green dash livre, obstáculo próximo, pausa e interrupção | Gesto/trajeto contínuos; confirmação não reinicia dash, rejeição restaura estado. |
| A04 | Pink jump com sombra, pouso, morte e troca de mount | Chão, altura e sombra permanecem alinhados; client não ganha proteção local. |
| A05 | Black dash em parede, contato e hold de impacto | Não atravessa parede por suavização; push/stun ficam confirmados pelo servidor. |
| A06 | Red punch acertando e errando | Gesto começa localmente; som de acerto e consequências só após confirmação, sem áudio duplicado. |
| A07 | Preparação Mole, transição e cancelamento | Preparação não espera nova ida/volta; destino aleatório e proteção não são escolhidos no client. |
| A08 | Tank nas quatro direções, cooldown e alvo próximo/móvel | Proxy transfere para projétil real sem salto; pausa, impacto e rejeição não duplicam tiro/dano. |
| A09 | Line/Purple, estoque parcial e variantes de bomba | Cada célula confirma separadamente; comando não duplica som/estoque nem usa tick de outra ação. |
| A10 | ControlBomb com seleção e detonação, inclusive bomba ausente | Feedback responde ao comando correto; servidor decide detonação e não há ativação por replay. |
| A11 | Montar/desmontar, disputar pickup, fila de ovos e trocar posse | Arcos e seguidores mantêm fase, visibilidade e sorting; posse/ovo não são concedidos por previsão. |
| A12 | Revenge: entrada permitida, mira, movimento, lançamento e retorno | Confirmar spawn/netId antes do arco Revenge, pausa/stop e visual com owner eliminado; depois transporte de input/elegibilidade/carrinho. Apresentação não reativa player eliminado. |
| A13 | Alterar velocidade/debuff/pass com inputs ainda pendentes | Replay usa contexto histórico confirmado; rejeição não apaga intenção direcional real. |
| A14 | Kick, punch, glove, Yellow/Blue, Search/Magnet, Rubber e bounce | Início contínuo, sem A duplicado, término ordenado e rollback completo em rejeição/interrupção. |
| C01 | Esteira reta/curva, mudança de fase e saída bloqueada | Força aplicada uma vez por tick; replay não altera fase global nem empurra duas vezes. |
| C02 | Canhão, mola e geyser com preparação, voo e pouso | Owner não salta entre reconciliações; curvas, efeitos e consequências permanecem aprovados. |
| C03 | Corda com ocupação concorrente e voo interrompido | Preparação/voo contínuos; deformação ambiental corresponde ao ator transportado. |
| C04 | Carrinho nas arenas 9/12, curva, embarque/saída e portal | Passageiro, cart, escala, estrelas e som se alinham; curva não corta diagonal e ocupação não é inventada. |
| C05 | Portal7/World3 com bomba carregada, máscara e saída ocupada | Teleporte tem corte explícito; escala/máscara/estrelas limpam em cancelamento e não deixam bomba duplicada visível. |
| C06 | Correnteza com freio/barreira de saída, plataforma multi-rider e barco | Suporte e rider usam a mesma timeline; saída/replay respeitam barreira e barco não concede pass local sem contexto. |
| C07 | Pusher enquanto o player também caminha, parede e recuperação | Compor força e input sem aplicar duas vezes; bounce posterior não apaga ou reinicia o empurrão. |
| C08 | Campo magnético, explosão que muda direção e redirecionamento | Client não sorteia alvo nem inicia simulação nativa; sequência aprovada não perde continuidade. |
| C09 | Caldeira, swap/blackout, dinamite, foguetes e tiles de força | Fase visual corresponde ao servidor; nenhuma edição, explosão ou recompensa nasce de hipótese local. |
| C10 | FallingBomb, teto aberto/transições, pólvora e Sudden Death | Aviso/alvo/fase aprovados; sem relógio ou ignição client independentes, sem decisão local de dano/RNG e sem teto fora de fase. |
| R01 | Dois players remotos, troca de mount, cabeça/pose e pausa | Transform e pose compartilham tempo; histórico não cruza gerações de montaria. |
| R02 | Co-op com paredes, props móveis e várias famílias de sprites | Geometria de consulta aprovada pronta antes da previsão; medir custo/atraso por família e preservar forma das curvas. |
| R03 | Frog/hop/snowman/korobokkuru: caminhada, salto e avanço | Trajeto aprovado contínuo sem prever alvo, pathfinding ou morte. |
| R04 | Hurricane, Magnet e bolha: captura/puxão/liberação com input | Força compõe com o motor quando caminhar é permitido; captura/dano/libertação são autoritativos. |
| R05 | Spear, rocket, ice, star e heart: spawn, curva/homing e impacto | Só percurso aprovado é apresentado; impacto encerra explicitamente sem collider de dano client. |
| R06 | SunMask, ClownMask, FreezerVenus e Mecha: telegraph/ataques | Pose, trajetória, sombra e som se alinham; Heart/Star não escolhem alvo/orbit/contato no client. Confirmar intro/transformação/telegraph e matriz de colisões; client não adivinha ataque. |
| R07 | Bomba em voo com owner inativo/destruído; itens, ovos e transferências | Motion confirmado continua, previsão cancela; VFX recebido sobrevive ao owner e cleanup devolve estado. Eventos sem owner são caso separado. |
| R08 | Barris, snowballs, bubbles e cenário móvel de World3 | Penguin escolhe alvo/fase no servidor; neve e barril não iniciam sweep/queda no client. Medir trajetória/sombra/áudio e pausa; visual não promete geometria diferente da aprovada. Limpar ao sair. |

## Verificações específicas dos últimos lotes

- **Owner da bomba:** confirmar arco já recebido, desativar/destruir owner,
  interromper o client e voltar à cena. Verificar continuidade, collider, stop
  ordenado e ausência de nova habilidade local. Receber evento antes de destruir
  o owner é diferente de tentar enviá-lo depois que seu endpoint já desapareceu.
- **Teto:** entrar durante cada fase, pausar numa transição e acompanhar ambos
  os peers. Incluir teto aberto com tile nulo e uma nova rodada. Nenhum timer
  client deve escolher a próxima fase.
- **Pólvora:** uma ignição, nova ignição durante a anterior, término, pausa e
  desativação. Piso/matrizes devem voltar ao original; client não cria hitboxes
  nem inicia o estado nativo IgnitionRunning.
- **Barco/Submarine:** após embarque confirmado, caminhar/virar nas quatro
  direções, com e sem Louie, comparar owner/observador e repetir pausa, scope de
  portal/transporte, desembarque, nova cena e desconexão. Conferir cabeça, sombra,
  ausência de barco duplicado, perda da permissão de água no tick correto e
  posição final do desembarque. Capturas45 incluem boat=True/False; o comparador
  separa boat/boat_mounted. Capturas antigas sem esse campo não isolam barco.
- **Sudden Death:** comparar `sudden_death_begin` (`path_count`, `path_hash` e
  `music_wait`) e `sudden_death_drop_begin/finished` por célula e papel. Esses
  eventos ajudam a investigar a timeline; não confirmam por si o commit de
  terreno nem encerram a auditoria das quedas.

- **Pusher/C07 (46):** Stage_2-4 e demais instâncias configuradas; caminhar
  durante contato, soltar o direcional, entrada lateral/parcial, duas shapes no
  mesmo corpo e dois pushers. Repetir endpoint/troca de sentido, pausa, parede,
  hole e bounce. Comparar pusher_step com pusher_resolve_grid_snap, depois
  movement_world_snapshot e movement_reconcile pelo mesmo player/tick/build.
  Conferir que posição muda mesmo sem ack novo e que a física continua aplicando
  uma força por passo de mundo. Continuação owner usa contato/célula aprovados
  por até150ms; saída de contato, nova célula, endpoint/reversão e pacote antigo
  precisam encerrar/renovar o contexto corretamente. Bounce e dano são do servidor.

- **Forças de inimigos/R04 (46):** Hurricane, Magnet e bolhas de Submarine:
  contato parado, caminhar junto/contra/lateral, fugir e soltar direção. Hurricane
  deve manter a redução0.5 do offline; sair de um slow não pode inventar pull.
  Incluir fim de frames12/13, saída de corredor/range, distância final da bolha,
  pause, wall/hole, source morto/desativado, duas fontes e pusher simultâneos,
  Scene/client stop e atraso maior que150ms. Conferir actor/owner/observador e COM.
  Capturas46 distinguem enemy_force; comparar a contribuição da força com o mesmo
  motor sem força quando houver centralização. Sem dano/captura/IA/RNG client.

## Critério de fechamento

Uma execução aprovada precisa de evidência dos papéis e interrupções relevantes,
comportamento offline preservado e ausência de dano/posse/terreno especulativos.
Se houver falha, registrar o ID e o evento de início/fim/correção, corrigir a
causa e repetir o cenário afetado. Atualizar o checkpoint com o resultado real;
não marcar todos os cenários como aprovados por uma única partida sem sintomas.

### Continuação C03 / protocolo 47 — corda

Automação cobre wire, frame aprovado, collider original intacto, overlay sem
collider/prefab, pausa, ordenação temporal, revisão/cena/caminho, entrada inválida,
histórico limitado, repouso, clear, disable e substituição de terreno. A execução
com dois peers ainda deve confirmar: preparação -> deformação -> impulso -> pouso,
soltar direção antes do disparo, acionamento simultâneo nas duas pontas, bomba
contra a corda, montado/desmontado, pausa durante preparação e fim por Sudden Death.
Capturar logs correspondentes e vídeo online/offline para avaliar a sensação.

### Continuação C02/R05 /47

Comparar Battle4 montado/desmontado, ascend/hold/descend, pausa e devolução ao
motor; aprovação de20 unidades vale apenas para apresentação de filhos. Testar
também portal/teleport (root deve cortar). Para R05 comparar nascimento, voo,
impacto, trilha e cleanup de Heli/Icicle/FishMan/Matango/FreezerVenus/Queen,
incluindo dono morto, homing e pausa. Capturar efeitos de100 ms e verificar o
limite ainda existente para efeitos menores que50 ms. Demais famílias pendentes.

### Continuação C09 /48 — caldeira, tiles e blackout

Validar online/offline2P: bomba capturada no boiler,80ms de puxão, freeze/unfreeze de
animação, porta/ignição/explosão, interrupção e dono morto; Rubber não deve saltar
após o pull. Validar swap/puzzle/plataforma, dinamite e foguete (alvo, fase, SFX e
impacto) sem RNG/edição local de terreno. Conferir colisão do owner enquanto a
apresentação da bomba termina.

World3: blackout ao entrar/trocar sala, luz P1/P2 seguindo movimento/facing, raio
aprovado, torches ligando/desligando, explosões, olhos com paleta, barrel em frente,
flip e mudança de sprite. Pausar durante fade: alpha continua como no offline,
movimento não. Legacy2-5: Image GO desativado deve reaparecer por estado aprovado.
Sair/reentrar e observar durante blackout ligado/desligado; não sobrar materiais ou
luzes. Confirmar num cliente gráfico: EditMode/headless não comprova pixels finais.

### R03/49 — salto e pausa das famílias prioritárias

Comparar Frog/Hop/SnowMan/Korobokkuru: sombra no chão desde o início, corpo no arco,
ondas/gotas no mesmo relógio, frame/facing de pouso, água e obstáculos. Pausar em
ascend/descend e conferir que nenhum buffer avança/rebobina a pose já exibida;
blackout continua seu fade unscaled. Testar wrap e entrega interrompida: corte no
ponto aprovado, sem atravessar a sala. Testar morte, stun e saída de cena, olhos
e barrel. Cenas/prefabs personalizados com jump maior precisam nova calibração.


## A12 adicional em51 (ainda não executado entre dois peers)

- Atribuir ao owner um slot diferente de1, morrer e controlar o carrinho com o
  hardware local1. Repetir teclado, controle e mobile; testar C, oito segmentos,
  cantos, direções opostas e troca rápida. Observar o mesmo cart no outro client.
- Apertar/soltar A rapidamente e manter carga; simular atraso somente em ambiente
  de teste. Timeout/overflow devem cancelar, nunca disparar bomba por release falso.
- Pausar em caminhada, entrada/saída e recoil. Caminhada/frame ficam parados;
  entrada/saída/recoil unscaled seguem a regra offline. Cursor não deriva do tile.
- Fazer uma eliminação Revenge: retorno começa visualmente ainda com life0/dead e
  collider bloqueado; fim confirmado restaura posição/stats corretos e permite o
  bounce seguinte. Não repetir A antigo/PowerGlove após reativar o Player.
- Usar loadout offline diferente do servidor; confirmar estatísticas recebidas,
  vítima indo para seu cart, troca de dono, nova geração, nova rodada e disconnect.
- Capturar os eventos revenge_cart_authority e revenge_cart_present com protocolo51
  dos peers. Não inferir input-to-first-motion exato com amostragem visual de10Hz.


## Integração automática em29/10 — protocolo55

Executados em processos separados: servidor dedicado e2clientes Mirror reais,
com Development, atores duráveis e renderização desativada. Isso cobre fluxo de
rede/erros, não substitui a comparação manual dos critérios acima.

| Evidência | Cenário/entrada | Resultado observado | Limite |
| --- | --- | --- | --- |
| Logs/PredictionFullClients55Run2/result.json | BattleMode_15,210s, Select pelo caminho de input normal |2cargas de arena; Skull/LineBomb apresentados nos2clientes; dismount Mole/Tank confirmado;0erros/avisos | Não mede pixels, hardware físico ou latência percebida; fixtures de Development |
| Logs/PredictionCoopClients55Run1/result.json | NormalGame/Stage_1-1,90s,2clientes sem input | Cena nos3logs;0erros/avisos;82reports de custo no servidor | Só essa cena/família; sem movimento humano nem comparação offline |
| Logs/PredictionCoopClients55Run1/analysis.json | Agregação por captura/build/protocolo/papel |23025 amostras de EnemyMovementController,121,3574ms CPU de captura e2788125bytes de frames | Inclui aquecimento; exclui envelopes, assets, fanout, transporte, diagnóstico e GPU |
| Logs/PredictionRecovery55.xml | EditMode881casos |881aprovados,0falhas/0skips; inclui sorting, reconexão e teardown | Últimos3casos posteriores aos binários dos smokes; sem validação gráfica |

Run1 de Battle falhou por limpeza após destruição do manager e envio após parada
do servidor. Corrigido e repetido no Run2: preservar ambas as tentativas como
histórico, não somar Run1 às execuções aprovadas.

Os smokes usam fontes locais modificadas com o stamp0b2f10852 do runner; o hash
real da DLL está em binary-provenance.json. Não usar o stamp como prova de deploy.
O script passa a gravar hashes em config.json antes de iniciar os processos.

Para repetir com um player Development criado por BuildPerformancePlayer:

```powershell
python Tools/RoomDirectory/smoke_full_clients.py --server <SB6.exe> --client <SB6.exe> --output <nova-pasta> --mount-input --prediction-items --seconds 210
python Tools/RoomDirectory/smoke_full_clients.py --server <SB6.exe> --client <SB6.exe> --output <nova-pasta-coop> --mode NormalGame --scene Stage_1-1 --diagnostics --seconds 90
```

A ferramenta só encerra seus
próprios processos. Libere o runner antes de compilar/substituir o executável.


## Co-op55 ampliado: intro e tiles (Run2)

O Run1 de Clown conectou sem Exception, mas seus logs mantinham input_locked até
encerrar: não comprovou combate. Corrigido cancelamento da própria coroutine após
a espera online. World3 Run1 tinha3avisos de tiles de esteira Runtime ausentes;
corrigido alias de catálogo e reaplicação da fase visual, com cor/matriz aprovadas.

| Execução | Checkpoint adicional | Resultado |
| --- | --- | --- |
| PredictionClownClients55Run2 | NormalGame/Stage_1-7,90s,2clients; intro libera movimento no servidor e cada owner |0erros/avisos,82reports de ClownMaskMovement,6717frames alterados em7750amostras |
| PredictionWorld3Clients55Run2 | NormalGame/Stage_3-6,90s; owners liberados; catálogo sem tiles faltantes |0erros/avisos,896reports em11famílias observadas, sem registros de custo inválidos |
| PredictionSunClients55Run2 | BossRush/Stage_2-7,90s; owners liberados |0erros/avisos,139reports, logs e análise por captura preservados |

Os3usam Development headless com atores duráveis/sem hardware físico. Config guarda
SHA256 das DLLs. Não comprova pixels, telegraphs, áudio, colisão sentida, curvas ou
comparação offline. A suíte887e os2focados seguintes cobrem correções de estado e
metadados; não somar casos repetidos como novos testes únicos.

A ferramenta aceita --require-unlocked junto de co-op/--diagnostics para exigir
esse checkpoint. Avisos de catálogo de assets faltantes também falham o smoke.


## R07: remoção do presenter durante arco nativo

PredictionPresenterClients55Run2: servidor+2clients reais, Stage_1-1, Punch nativo de
1,2s; remoção da origem após250ms. Cada client registrou8samples móveis depois da
transferência e antes do próximo apply,0erros/avisos. Entrada da fixture suspensa com
ack/drenagem antes da remoção. SHA256, logs e análise salvos na pasta do teste.
Suíte894/894 inclui stop/disable/destroy, fila já recebida, cancelamento de comando
não confirmado/Carry e preservação de gesto próprio ao esquecer worldMotion.

R07 está concluído em código/testes; os critérios visuais/manuais continuam pendentes.
Usar --presenter-removal com NormalGame/Stage_1-1 e --diagnostics para repetir o probe
Development. Não executar essa fixture como partida normal ou comparar seus inputs
suspensos com latência humana. Orçamento --seconds inclui lobby/carregamento; o campo
seconds deRun1 deste probe está inválido, corrigido emRun2. Nenhum vídeo/GPU foi medido.


## A12 — integração automática de input em29/10 (manual continua pendente)

PredictionRevengeInputs55Run2/result.json registra2clientes Mirror reais com
inputs sintéticos no hardware local1: morte nativa, entrada do kart, avanço comC,
indicador carregado até7tiles e3releases A porowner. Cada release teve um único
input/first_visual/confirm correlacionado por cart/generation/tick; zero reject,
timeout, erros ou avisos. Cada cliente apresentou os2carts. As3capturas foram
copiadas somente após encerrar/flush dos clientes e incluem hash do binário.

Medianas do presenter CPU0,4232ms/P1 e0,5697ms/P2; confirmações132,8546ms e132,8911ms.
Três amostras porowner não permitem p95. Headless/atores duráveis/local loopback;
não equivale à verificação visual/offline, teclado/controle/mobile ou rede adversa
listada acima. Run1 não aprovado: cópia de arquivo ainda aberto cortou a última
linha; o encerramento gracioso da fixture foi corrigido e Run2 repetido com sucesso.


## C02 — cancelamento automático de lançadores em29/10

PredictionCannonLifetime55Full901/901 e PredictionSpringLifetime55Full902/902
passaram. Os casos novos incluem PlayMode real, desativação de GO ou componente
na preparação/voo, trigger repetido com mecanismo desativado e restauração de
input/collider/BombController/proteção inicial. Canhão também cobre descarte do
iterator aninhado, reutilização e cancelamento de dash Louie antes de capturar o
lock anterior. Nenhum desses testes prova curva/pixels/hardware ou comparação
visual entre peers. C02/C03 continuam parciais; os cenários manuais acima permanecem.


## Aceite A12 em29/10/2026

O usuário confirmou validação do A12 com2clientes reais e solicitou sua conclusão.
A12 está concluído no checklist. As capturas e testes automáticos anteriores são
evidência separada desse aceite; os demais roteiros continuam como regressões.


## C03 — interrupção da corda,29/10/2026

PredictionRopeLifetime55Focused passou em PlayMode, cena isolada BattleMode_11:
preparação/voo, GO/componente, flags/pose/proteção anterior, cancelamento sem
teleporte ao destino e reutilização. A baseline com a mesma fixture corrigida
falhou no input preso após desativar. PredictionRopeLifetime55Immediate também
exige preparação no frame da chamada. Isso verifica código nativo e lifetime;
não substitui curva/renderização/2peers em uma arena completa.
