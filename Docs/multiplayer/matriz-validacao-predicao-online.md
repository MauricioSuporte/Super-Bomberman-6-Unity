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


### Evidência automatizada C03 — protocolo59

PredictionRope59Full3:995/995; PredictionRope59Peers3:90s, dois clientes Mirror
renderizados. Ambos apresentaram início/fim, fases sem recuo e endpoints750ms
(voo a pé),20ms (pilar) e240ms (cancelamento com Pink), voltando ao motor livre.
Capturas URP inspecionadas, zero erros/avisos de servidor. Native lifetime/pausa/
timeout continuam cobertos na suíte. Não substitui o roteiro humano em offline
vs owner/observer, posse concorrente, dano, inputs variados e rede degradada.


### C04 — roteiro de carrinhos, protocolo60

Validar BattleMode_9, BattleMode_12 e Stage_3-2 (salto/wrap), com passageiro
owner e observador, a pé e montado. Observar curva sem diagonal, cabeça/corpo
presos ao mesmo referencial, hop de entrada/saída e liberação para caminhar.
Na arena12, conferir sink/estrelas, intervalo oculto e rise no destino aprovado;
nenhum movimento visível atravessando o mapa. Pausa no transporte deve conservar
posição e retomar no mesmo trecho. Interromper/desabilitar e repetir embarque;
proteção de outra fonte deve sobreviver e morte não deve reativar o jogador.
Disputa entre dois jogadores na estação continua decidida no servidor.

Não há comando de direção/rota do ocupante no controller nativo atual. Entrada,
saída e destino aleatório ficam autoritativos conforme o plano. Não marcar a
antiga pendência histórica de "boarding previsto" como requisito do C04.
Os testes automatizados e capturas locais são registrados no checkpoint; este
roteiro não representa aceite manual humano já executado.


### Evidência automatizada C04 — protocolo60,04/11/2026

Full7:1010/1010 Unity,0skips; 15novoscasosC04 desde995. Casos incluem root de
owner/observer na curva, pause/hidden/cut, fila de bounce+stop, disposals nativos
emEnter/Rail, proteção anterior/reuso, branch inativa, pausa longa e FPS10 sem
pular transição. Development8 compilou helper final da fixture.

Logs/PredictionCart60Peers7-cart32/cart9/cart12/result.json aprovados.2clientes
reais renderizados,110s porcenário,0erros/avisos. Battle9 e12: ambosowners a pé/Pink,
entrada/trilho/saída, sink/rise12 e caminhada após client soltar apresentação;
Stage3-2: shuttleRoom1 comwrap/jump e retorno. ImagensURP de embarque/trilho/jump/
rise inspecionadas. Não há medição humana de latência nem comparação manual
completa com offline; demais trajetos dasalas2/3 e aceites perceptuais seguem
no roteiro. Código de rota/ocupação/colisão/dano continua nativo/servidor.

### C05 — continuação de ciclo de vida dos portais

Full4:1013/1013, sem skips. Teste nativo dePlayMode desativa/reutiliza portal
hidden/sink e verifica locks/proteções anteriores, morte e cancelamento de bomba.
CatálogoBigWorm e limpeza precoce deStage34Sandstorm recebem regressões.
Smokes com2clientes reais e câmeraURP exigem entrada confirmada/origem aprovada,
destino apósLateUpdate e posterior movimento livre para owner e observer.
Evento de entrada permite janela oculta que começa entre frames; jamais substitui
a evidência da chegada renderizada. Resultados finais no checklist de progresso.
Comparação humana, bomba carregada e saída ocupada continuam neste roteiro.

C05 final: FinalFocused2 3/3; Peers8 Battle7 eStage3-4 com2clientes renderizados
passaram sem erros/avisos do servidor. Publicação completa do commitba9be59ee
confirmada no Actions42010967595 (Linux/Windows/updater). O QA humano acima
continua separado desse aceite automatizado.

### C08 — ímãs e redirecionamento

Full2:1028/1028,0skips, incluindo15 casos novos. Cobertura: Magnet/Search
lineares ordenados, canto deKick, stop normal, cancelamento que descarta rota,
pausa, revisão antiga/duplicada e espera quando aprovação futura não chegou.

Fixture de integração Development emStage_2-1 usa movimentos nativos no
corredor isolado(30,30), obstáculo real Stage no fim e2clientesMirror headless.
Cada captura exige origem/canto/destino deMagnet/Search/Kick e mantém a
regressão de remoção do presenter. Peers1 encontrou extrapolação autônoma
além do canto; falha preservada e causa corrigida sem aumentar tolerâncias.
Resultado final é registrado no checklist.

QA humano: Battle6(setas), Battle14 eStage2-1(ímãs), com mudança de direção
por explosão, bomba de outro jogador, bloqueio móvel, pausa e owner saindo.
Comparar sensação/frames comoffline; o smoke headless não mede renderGPU
nem latência dehardware. Não prever alvo/campo/colisão/dano.

C08 final: Peers2 aprovado em117,875s. Ambos os clientes apresentaram os3
trajetos e9amostras após transferência, sem erros/avisos do servidor.
Full2final1028/1028; C#0erros/0avisos; Python86/86.

C08 Peers3 final aprovado em118,812s com gate de serial: três trajetos e oito
amostras de transferência porcliente;0erros/avisos. Final18 18/18.

C08 publicado noActions42110831096, commit3ca94c369/protocolo63,
cliente+servidor+updater confirmados. QA humano acima continua separado.

## R01 — player remoto (observador): cobertura e medição

Medido por Tools/Diagnostics/analyze_remote_player.py (limites provisórios; sem baseline):
atraso do root contra o atraso de interpolação esperado (timeline-ptime ou bufferMs), deriva entre
janelas, travamentos, descontinuidades, erro RMS, sequência de poses, geração/tipo de montaria
(rev=, build nova) e offset do corpo. Resultados possíveis: dentro dos limites, falha de medição ou
inconclusivo (movimento insuficiente); inconclusivo nunca é aprovação.

| Transição | Fixture | Medida no observador | Lacuna |
| --- | --- | --- | --- |
| Caminhada / root | `--remote-walk` (novo; compilação pendente) | atraso, deriva, travamento, RMS | sem comparação offline |
| Montar (ovo) / troca de geração | `--mount-visuals`, `--mount-input --mount-cycles` | poses, offset, rev= | mount-cycles anda pouco |
| Morte (Blue/Pink) | `--mount-visuals` | estados de renderer | morte montado em outro cenário não coberta |
| Pink jump, emote, AFK | `--mount-visuals` | estados de renderer | aceite perceptual |
| Pausa | `--mechanisms sudden` (pausa) | indireta | pausa durante pose/montaria não medida |
| Mudança de rodada | `--mechanisms sudden` (recarga) | indireta | cleanup de pose/montaria não medido |

Arcos de montar e de salto Pink ficam fora das estatísticas de root (outros presenters).
Não medido: aceite perceptual/GPU, latência humana, custo CPU/bytes, comparação offline, tick por
observador (o campo só existe para o dono). Pistas de capturas antigas (build anterior ao reparo 58):
observador sem MountAscendLeft/Right; reconferir com a build atual.

### R01 — atualização da execução assistida (06/11 15:00)
| Item | Estado | Evidência |
| --- | --- | --- |
| Locomotion headless (2 observadores) | dentro dos limites provisórios | `analysis-locomotion.json`: lag 60 ms constante, 0 stalls/descontinuidades |
| Locomotion renderizado | dentro dos limites provisórios | `analysis-locomotion-rendered.json`: lag 40 ms; frames em `smoke-locomotion-rendered/client-N-frames` |
| Troca/offset de montaria e arcos | dentro dos limites provisórios | `analysis-mount-visuals.json`, `analysis-mount-cycles.json`; observador loga MountAscend/Descend |
| Raiz em mount-cycles / regression-sudden | não mensurável (players parados) | não conta como evidência de locomotion |
| Julgamento perceptual / GPU real | PENDENTE (QA manual) | imagens em `evidence/` |
| Baseline e limites definitivos | PENDENTE | limites provisórios; 1 execução por smoke |
R01 permanece parcial.


## R01 - atualizacao da investigacao (06/11, 15:30) - parcial

| Item | Estado | Evidencia |
|---|---|---|
| Falha de audio atribuida ao R01? | Nao: baseline sem R01 passa; asset do repo falha igual | Logs/R01-baseline-* |
| Analise por entidade/geracao/ptime | Implementada; inconclusivos contados | analyze_remote_player.py, 24 testes |
| locomotion rendered | Dentro dos limites provisorios | analysis-locomotion-rendered.json |
| locomotion headless | FALHA de medicao (p95 0,52; deriva em janela com travadas do servidor) | analysis-locomotion.json |
| mount-visuals / mount-cycles / regression-sudden | Inconclusivos (raiz parada), 0 divergencias estritas | analysis-*.json |
| Silhuetas pretas | TemporarySpeedBlink, esperado | evidence/black-silhouette-crops.png |
| Travadas do servidor | 190-4050 ms medidas por dt | server-motion.jsonl |
| Candidatos | recuo de ptime, estado de 1 quadro, degraus sem referencia | analysis-*.json candidates |
| QA manual | pendente (sequencia no checkpoint) | - |
| R01 | PARCIAL, nao publicado | - |

## Rodada 06/11/2026 noite (resumo)
- Locomotion headless/renderizado repetido (budget 600): 2 dentro dos limites provisorios, 2 inconclusivos; nenhuma janela descartada. R01 parcial.
- ptime: recuo por TimelineClamp, chega a posicao (0,3-0,4 u em movimento); perceptivo pendente.
- Quadro corpo inteiro Mole/montaria: reproduzido so no observador (nao e amostragem); sem correcao.
- Travadas do servidor: GC/cena/update, captura barata; GC nao provado causal.
- round-reload: falha de orquestracao (sem checkpoint [LoadTest] completed); mount-visuals_n3 inconclusivo; troca de rodada, pausa com montados: nao cobertos.
- Audio catalog: falha separada (ver progresso).

## Rodada 06/11/2026 noite (2) - resumo
- Flash de corpo na montagem: causa localizada (EnableExclusiveFromState apos o LateUpdate do NetworkPlayerAnimation; mount adiantado em 1 frame sobre o corpo bufferizado). Corrigido na apresentacao do observador (reaplicacao no fim do frame + gate do mount); residual 1 frame (~16 ms) no dono em MoleStartup. Dados: tudo desligado 8 -> 0-1.
- Passos para tras: suavizador de posicao so no observador (relogio do Mirror intacto, cortes legitimos preservados). CPU nova quase nao reproduz o recuo; beneficio indireto. Percepcao pendente (usuario).
- round-reload: falha era do harness (cliente 180 s < servidor 200 s); corrigido, k1 exit 0.
- mount-cycles k1: 1 falha de validacao do dono, nao reproduzida em 4 repeticoes.
- Locomotion CPU nova: headless k1 dentro dos limites; renderizado inconclusivo. Resultados separados por hardware; sem comparacao direta de tempos.
- Cenarios parados validam pose, mas seguem inconclusivos para locomotion.


## Rodada 07/11/2026 (CPU nova) - resumo
| Item | Resultado | Evidência |
|---|---|---|
| EditMode | 1047/1047, 5 testes R01 novos | Logs/R01-assisted-20261107-002305/R01-tests.xml |
| Python | Ci 107/107, RoomDirectory 15/15 | - |
| Desmonte sob travada | predicted 4 / confirmed 4 (s1, r1) | smoke-mount-cycles_*/result.json |
| Corpo a pé + mount | 0 no dono e no observador (1 execução) | smoke-mount-visuals_r1, mountchange-*.png |
| Relógio de apresentação | retenções ocorrem; 0 passos durante retenção | analysis-locomotion_*.json |
| Passos para trás > 0,1 u (travada 180 ms) | 2ª métrica: ligado 17, desligado 29, renderizado 38 (1 execução cada) | analysis-*_m1/_m2.json |
| Métrica final (3ª) | compilada, NÃO executada (rodada 5 encerrada por memória baixa) | Logs/R01-round5-chain-output.txt |
| Catálogo de áudio | corrigido (2 entradas), teste passa | OnlineBattleAudio.asset |
| Runner | restaurado, sem conflitos | Logs/R01-runner-sync/ |
| QA manual perceptual | pendente | - |
| R01 | PARCIAL, não publicado | - |

## Rodada 07/11/2026 06:00-08:30 (CPU nova) - A/B final, relógio removido, R01 parcial
| Item | Resultado | Evidência |
|---|---|---|
| Captura do servidor | densa (todo frame), 0 lacunas de captura | `-online-dense-server-reference` |
| Passos reais (`presentation`), travada 180 ms | relógio ligado 0/2*/0, desligado 0/0/2 (*ambíguos) | Logs/R01-assisted-20261107-061225/ab-stall.json |
| Passos reais, calmo | ligado 0/0/0, desligado 0/1/0 | ab-calm.json |
| Build final sem relógio | travada 0, calmo 0; atraso p95 0,4 ms | Logs/R01-assisted-20261107-071951/ab-final.json |
| Retenções do relógio | 297-342 (travada), 43-55 (calmo); máx. 218 ms; sem benefício | ab-*.json |
| Atraso acumulado | mirror−apresentado p95 ≤ 0,5 ms, não cresce | ab-*.json |
| Raiz/pose/mount | 0 desalinhados, 0 pose≠mount | ab-*.json |
| Relógio de apresentação | **removido** | NetworkPlayerAnimation.cs |
| Fila de input | Select 4/4, 0 duplicados (3 execuções); teste de rajada | mount-cycles_c1-c3, EditMode |
| Pausa montado | 0 passos e 0 trocas durante e depois | mount-visuals_v2 |
| Morte montado | NRE do servidor corrigida; Tank→None→Death sem quadro inválido | mount-visuals_v1 (falha) / v2 |
| Sudden death | completo, 0 avisos | regression-sudden_s1 |
| Round-reload | `round-reload_r1` (calmo, cerca de 200 s, com troca de rodada): terminou (`[LoadTest] completed`), com 0 exceções e 0 avisos, mas teve **exit 2 por 1 falha de pose**: duas cabeças no servidor, ver "Achados separados". | round-reload_r1 |
| Testes | EditMode no runner, com o código final (relógio removido e correção da NRE): **1045/1045** aprovados, 0 falhas, 0 ignorados e 0 avisos `warning CS` no log de compilação (`Logs/R01-assisted-20261107-071951/R01-tests.xml`). O total caiu de 1047 porque os 3 testes do relógio saíram e 1 teste de fila foi acrescentado.; Python Ci 113/113, RoomDirectory 15/15 | R01-tests.xml |
| Locomotion (verificador de amostragem) | inconclusivo "borderline" em excursões rápidas (limite não mudou) | analysis-locomotion_*.json |
| QA humano perceptual | pendente, separado | - |
| Duas cabeças no servidor (Mole + override de inatividade) | aberto, pré-existente, fora da apresentação do observador | round-reload_r1, frames 7167-7227 |
| R01 | PARCIAL: critérios de apresentação atendidos; 1 achado de pose do servidor aberto; QA humano pendente; não publicado | - |

## Rodada 07/11/2026 08:30-09:20 - duas cabeças no Mole corrigidas
| Item | Resultado | Evidência |
|---|---|---|
| Causa | stun só com tremor (Mole) ligava a cabeça para baixo sem desligar a atual | StunReceiver.ShowMountedPlayerDownForMountedStun, Mole.prefab `useAnimatedStunRenderer: 0` |
| Correção | StunReceiver esconde/restaura as poses sentadas (MovementController.CollectMountedBodyRenderers) | StunReceiver.cs, MovementController.cs |
| Regressão | EditMode R01MountedShakeOnlyStun... passa; fixture de stun no mount-visuals | Logs/R01-assisted-20261107-084653/R01-tests.xml |
| EditMode | 1046/1046, 0 avisos | idem |
| mount-visuals_t1 | uma cabeça durante o stun (Mole e Tank) em servidor, dono e observador | smoke-mount-visuals_t1 |
| round-reload_t1 | 0 falhas (antes exit 2), 0 poses duplas, 0 `presentation` | analysis-round-reload_t1.json, ab-final.json |
| sudden / mount-cycles / locomotion (travada) | 0 falhas; Select 4/4 sem duplicar; 0 `presentation` | ab-final.json |
| Falhas técnicas abertas | nenhuma reproduzida | - |
| Inconclusivos | borderline de amostragem em locomotion; cenários parados sem atraso | analysis-*.json |
| QA humano perceptual | pendente, separado | - |
| R01 | técnico sem falhas abertas; aguarda QA humano; não publicado | - |

## R01 - status em 07/11/2026 09:30
| Item | Status |
|---|---|
| Implementação | concluída |
| Validação automatizada (EditMode 1046/1046, smokes no código final) | concluída, sem falhas técnicas abertas |
| Medições inconclusivas (borderline de amostragem em locomotion, cenários parados, morte inferida, poucas repetições, só CPU nova) | registradas como limitações |
| Teardown antigo (`NetworkBombFx::ServerEmit` após `[LoadTest] completed`, smoke-locomotion_a1) | não reproduzido; não corrigido; limitação |
| Aceite manual (QA perceptual) | **pendente**; pacote em `C:\Users\mauri\SB6-QA-R01` |
| Publicação / R02 | não realizada / não iniciado |

## R01 - status em 07/11/2026 (aceite manual aprovado; R01 parcial)
| Item | Status |
|---|---|
| Implementação | concluída |
| Validação automatizada (EditMode 1046/1046; smokes no código final) | concluída, sem falhas técnicas abertas |
| Aceite manual (QA perceptual, 2 clientes) | **aprovado pelo usuário nos cenários testados** |
| Medições inconclusivas (borderline de amostragem em locomotion, cenários parados, morte inferida, poucas repetições, só CPU nova, stun casual no round-reload) | preservadas como limitações |
| Teardown antigo (`NetworkBombFx::ServerEmit` após `[LoadTest] completed`, smoke-locomotion_a1) | não reproduzido; não corrigido; limitação |
| Custo CPU/bytes do player remoto (R01) | **MEDIDO**. Com captura (`Logs/R01-measure-20261107c`): observador +6,4% de ciclos e servidor +8,6% de alocação, vindos dos diagnósticos. Sem captura (`Logs/R01-measure-20261107d/R01-custo.json`; 5+5+5 válidas; captura confirmada desligada nos 3 processos): nenhuma diferença resolvida em ciclos, tempo de frame (Δ ≤ 0,05 ms, < 0,35% de 16,7 ms), alocações, GC, fps ou bytes. Otimização não acionada. Limitações: só builds Development; fila de input e stun sem chave |
| Comparação online × offline (`analyze_prediction.py`, Logs/R01-comparacao.json) | **DEMONSTRADA** na sessão 2 (`Logs/R01-measure-20261107b`; 3+3, A–E aprovados, 0 excluídas). A sessão 1 falhou pelas regras originais (instrumento de jogadores e `Afk2` aleatório); está registrada |
| R01 | **CONCLUÍDO** nos critérios obrigatórios (07/11): QA manual aprovado; comparação demonstrada; custo medido com e sem captura; gate Development-only compilado e testado (EditMode 1046/1046). **Publicado em homologação**: `b01817c73`/65, Actions 42403939455; servidor, cliente e atualizador conferidos. R02 não iniciado |
| Publicação / R02 | pendente de autorização / não iniciado |

## R02 — mundo co-op: cobertura e medição (07/11/2026)
| Item | Resultado | Evidência |
|---|---|---|
| Forma, famílias de gameplay (Junction, Speeder, BubbleChip, Sandpile) | legacy p95 0,3–0,8 px / máx. até 4,64 px → r02 p95 0,1 px / máx. ≤ 0,43 px | Logs/R02-families-20261107/families.json, Logs/R02-families2-20261107/families.json |
| Início após repouso (retenção) | legacy 0,64–1,28 px → r02 0 | idem |
| Fome (pré-registrada) | Junction 0,22–0,33 → 0,08–0,18; Speeder 0,22–0,34 → 0,09–0,22; Sandpile e famílias estáticas não resolvidas | idem |
| Paradas no cliente contra o nativo (offline) | BubbleChip 12–19% → 0–0,6% (nativo 0,6–1,7%) | idem |
| Pausa / recarga | pausedMoving 0; pausedHeld igual; 2 recargas por execução, 8 + 8 + 21 válidas | sessões |
| Custo atribuível (r02 − legacy) | servidor +0,13 ms/frame, +4,5% ciclos, +13 KiB/s; clientes sem diferença resolvida | Logs/R02-cost-20261107/cost-attributable.json |
| Custo total (r02 − baseline) | servidor +8,5 KiB/s (headless) / +13,8 (renderizado); alocações +46 KiB/s, das quais +34 vêm de `playbackTime` | cost.json, cost-rendered.json |
| Sobreposições de tela | perda de forma anterior ao R02 mantida; fora do stream de posição | checkpoint R02 |
| QA perceptual | pendente | C:\Users\mauri\SB6-QA-R02\ROTEIRO.md |
| R02 | técnico validado; aceite perceptual pendente; não publicado | - |

## R02 parte 2 — wrap, reconexão, atraso p95, frequência e fome real (08/11/2026; protocolo 67; R02 parcial)
| Item | Resultado | Evidência |
|---|---|---|
| Atraso p95 (frames em movimento, agregado por cliente) | frame 70–120 ms contra legacy 90–210 ms; sobreposição, então não piora. Média 60–72 ms | Logs/R02b-modes-20261108/families.json |
| Pico de 1,2–1,6 s (parte 1) e picos de até 5,9 s | lacunas de entrega do host: RTT de localhost 0,7–1,5 s nos 2 clientes; retenção aprovada + buffer dinâmico do Mirror; iguais no legacy | Logs/R02b-modes-20261108/spikes.json |
| Reconexão real (saída pela LeaveRoomToBrowser, sala nova) | 3/3 aprovadas; `prior*` = 0 em todos os inícios de stream; fixture completa na sala B; 0 erros | Logs/R02b-reconnect-20261108 |
| Wrap das sobreposições | máximo 200–255 → 9–20 px; frames > 2 px 28–31 mil → 0,5–3,9 mil; reposicionamentos = 0,75–0,78% dos bytes da família. Critério de forma **não atendido**: resíduo em 96–99% nas janelas após travamentos do servidor | families.json (modes) |
| Frequência | `frame` mantido (teto de 60 Hz = taxa de envio do Mirror). Half perde a forma; segments piora paradas e fome real | families.json (modes) |
| Fome residual | separada: retenção aprovada (pose repetida) × fome real (pose mudou). BubbleChip: real 0–1%; Junction/Speeder: real 7–15%, média < 1 frame | families.json (modes) |
| Cobertura | Stage_1-1 e Stage_3-6: frame máximo ≤ 0,64 px; legacy perde a forma em 7 famílias (até 8,2 px). Atraso inconclusivo (host degradado nas 2 configurações) | Logs/R02b-coverage-20261108 |
| Alocações (campo de cliente removido) | clientes −26 KiB/s (medido); servidor −28 KiB/s não resolvido (sobreposição de 4 KiB/s). Renderizado contra a baseline: servidor +13,6 KiB/s (parte 1: +47) | Logs/R02b-costparts-20261108, Logs/R02b-cost-20261108 |
| Validação | dotnet 0/0; EditMode 1053/1053; Python 136/136 + 15/15 | Logs/R02-tests-20261107-232936 |
| R02 | parcial; aceite perceptual pendente (roteiro atualizado); não publicado | C:\Users\mauri\SB6-QA-R02\ROTEIRO.md |

## R02 parte 3 — resíduo das sobreposições, alocação por caminho e origem dos picos (08/11/2026; protocolo 67; R02 parcial)
| Item | Resultado | Evidência |
|---|---|---|
| Causa do resíduo após frames do servidor > 40 ms | captura do servidor no LateUpdate do manager (−150), antes da areia e da câmera (0): pose do frame anterior com o carimbo atual; o wrap também era detectado um frame atrasado. Câmera parada no Stage_3-4 (0 px) | coop_phase_probe em Logs/R02c-repro-20261108 |
| Correção | `CoopLateCaptureDriver` (ordem 32000) captura depois de todos os LateUpdate; teste EditMode de ordem | OnlineCoopWorld.cs; EditMode |
| Reprodução controlada (travadas de 120 ms × 12) | fase antiga: máximo de 10,52 px (2/2); r02c: 1,13 px (2/2) + renderizada 1,13 px; sem hitch: 1,06–1,16 px (3/3). p95 de 0,8 px. **Critério de forma atendido; limite de 2 px mantido** | Logs/R02c-repro-20261108/repro.json, families.json |
| Regressão das famílias de gameplay | máximo ≤ 0,43 px; p95 do atraso de 80–100 ms (r02b 70–120); nenhuma piora medida em forma ou fome | families.json |
| Alocação por caminho (mensagem de 48 frames) | campo removido (168 → 160 B por frame), mas o Boehm aloca 8192 B nos dois casos; servidor ≈ 0 KiB/s esperado. **Redução não demonstrada** | EditMode CoopSpriteMessageAllocationsArePerPathMeasured (Logs/R02-tests-20261108-032440) |
| Custo total no código final | r02c − r02b: nada resolvido (sem regressão). r02c − baseline: servidor +25,2, clientes +23 KiB/s, saída +22,3 KiB/s (medidos); trabalho não resolvido | Logs/R02c-cost-20261108/cost.json |
| Picos de localhost | 41 travadas do servidor, 5 de cliente, 2 com origem não isolada (atraso observado pelo Mirror); corrige a atribuição à rede feita na parte 2 | Logs/R02c-spike-origin-20261108/spike-origin.json |
| Validação | dotnet 0/0; EditMode 1055/1055; Python 137/137 + 15/15 | Logs/R02-tests-20261108-032440 |
| R02 | parcial: aceite perceptual humano pendente; publicado em homologação em `b2d90fb7b`/protocolo 67 (Actions 42473009178; servidor, cliente e atualizador conferidos) | C:\Users\mauri\SB6-QA-R02\ROTEIRO.md; Logs/R02-publication.json |

## Aceite R02 e continuidade R03 — 09/11/2026

| Item | Estado atual |
|---|---|
| R02 | CONCLUÍDO: usuário aprovou os cenários testados. Publicação b2d90fb7b/protocolo 67 preservada. Custos e limitações históricos permanecem válidos. |
| R03 | Continuidade autorizada para Claude Code; auditoria de famílias iniciada documentalmente. Implementação e validação pendentes; não publicado. |


## R03 — entrega técnica final em 10/11/2026

Candidato final **viewcache2**, protocolo **68**, assembly `2c3bb067ec602195d5dc069a05ecb4166b9cbdc3d6b1e49f55ee979732dd34f7`. Implementação e validação técnica concluídas; **aceite perceptual humano pendente**, sem publicação/commit/push, sem avançar o plano. QA preservado em `C:/Users/mauri/SB6-QA-R03` (build, identidade, quatro atalhos visíveis e saves isolados). Nenhuma cena/prefab foi alterada ou depende de novo wiring.

Correções: frames manuais obedecem à autoridade; arestas de pose/visibilidade e vértices de repouso são publicados sem antecipar a locomoção; água preserva parâmetros do shader; corpo/sombra seguem o mesmo histórico aprovado; Frog/Hop limpam e reiniciam estado nas interrupções nativas. IA, RNG, colisão e dano continuam no servidor. Cache por vista evita limpar MaterialPropertyBlock/reatribuir material a cada frame; transições água/normal/blackout e remoção invalidam o cache.

Validação final: core EditMode online/COM **1068/1068**, build Development, C# **0 erros/0 avisos**, Tools/Ci **161** e RoomDirectory **16** testes aprovados. Não é a suíte inteira do projeto: a falha de chat de teclado encontrada na baseline continua registrada fora deste core. Três smokes gráficos com servidor e dois clientes Mirror reais, três referências offline nativas e três smokes de interrupções concluídos. Sem descarte por limite de captura. Em ambos clientes e nas quatro famílias: zero divergências verificáveis de sprite, movimento em pausa ou água; zero amostras verificáveis acima de2px. Frog máximo **0,420px**, água ativa **224/231** amostras; demais máximos <0,000031px (erro numérico do renderer, não garantia de precisão física). Lacunas, cortes e referência ausente continuam explicitamente separados.

Interrupções: morte nativa de Frog/Hop/SnowMan/Korobokkuru visível nos dois clientes; esmagamento por barrel aplicado nos dois Hops; desativação/reativação e recarga verificadas. Sombra Frog teve cauda de **7,1/20,6ms** após a última amostra visível da autoridade, compatível com cadências diferentes; não declarar remoção instantânea. Snow/Koro mantêm sombra durante parte da morte nativa e isso foi preservado.

Custo final: **três pares** baseline-guard/viewcache2, mesma fixture, captura desligada, últimos30s antes de pause1 (janela definida antes da análise). Baseline assembly `a709a71ae0fcc806740f1a4c7f441fb6b3b496e3aa423729c3e6f2115c5de95a`. Clientes: delta ciclos **+2,97%/+0,59%**, work/frame **−0,0177/+0,0039ms**, todos com ranges sobrepostos; não demonstram custo zero. O incremento separado de +0,238/+0,344ms da candidata shadercache foi resolvido nesta medição. Servidor: alocação **+71,97KiB/s (+4,10%)**, saída total **+42,83KiB/s (+6,55%)**, ranges separados. Saída agrega o mundo inteiro/2clientes (~21,4KiB/s por cliente), não é custo isolado de uma família. Não escondê-la como otimização de tráfego.

Evidências: `Logs/R03-entrega-viewcache2-20261110/{identity,custo,familias}.json`, `Stage_3-X-comparacao.json` e `Stage_3-X-interrupcoes.json`; capturas originais em `Logs/R03-peers-viewcache2`, `Logs/R03-offline-viewcache2`, `Logs/R03-interruptions-viewcache2`, custos em `Logs/R03-cost-viewcache2`. Os somatórios instrumentados por família excluem envelopes, âncoras e fanout, têm durações/RNG nativos diferentes e não substituem a medição total sem captura. A comparação offline valida curva/fase nativa; execuções independentes não repetem exatamente o RNG ou colisões.

Inspeção Codex: imagens finais Snow/Koro e janelas reais de ambos observadores do Hop/blackout/olhos conferidas sem corrupção evidente. Firewall resolvido manualmente pelo usuário; nenhum controle de segurança automatizado. Não equivale a aceite humano ou estudo perceptual contínuo. Limites: uma máquina Windows, Development, poucas repetições, sem GPU/WAN/jitter/produção Linux medidos, intervalos sem referência inconclusivos e transições excluídas da janela de custo de regime. Pacote e roteiro prontos para aceite humano.


### R03 — encerramento da validação técnica e restauração (10/11/2026)

Janelas reais sem batchmode: smokes adicionais Stage_3-4 e Stage_3-5 concluíram com dois clientes, zero erros/avisos do servidor, mesma DLL final. Inspeção Codex do Hop nos dois observadores, blackout/olhos e Snow/Koro em poses diferentes sem corrupção evidente; PNG final Frog/água/sombra também conferido. Não houve aceite humano nem teste manual de todos os inputs, e não foram inferidos a partir das imagens. O aviso de Firewall foi resolvido manualmente pelo usuário.

Pacote final em C:/Users/mauri/SB6-QA-R03: cópia da DLL 2c3bb067ec602195d5dc069a05ecb4166b9cbdc3d6b1e49f55ee979732dd34f7 verificada, identity.json, RESULTADO.md, LEIA-ME.md, quatro atalhos com clientes visíveis e launcher com sintaxe verificada. Antes da restauração, todos scripts de runtime no runner conferiram com identity.json e com as fontes do checkout (normalizando somente CRLF/LF).

Runner restaurado pelo sync reversível com exit0: dez arquivos originais restaurados e dois arquivos novos removidos apenas do runner. Conferência independente dos 12 hashes/ausências originais passou; estado arquivado em Logs/R03-runner-sync/backup-20261109-194652/state-restored.json. Library/build/cache não foram revertidos. Fontes implementadas permanecem no checkout principal. Sem reset/clean, Packages ou fontes/URP/settings preexistentes alterados.

Conferência final: git diff --check sem erro; UTF-8 estrito nos 11 C# alterados/novos sem BOM corrompido/duplicado ou replacement character; dotnet0avisos/0erros; Tools/Ci161/161 repetidos após empacotamento. R03 permanece com checkbox aberto exclusivamente pelo aceite perceptual humano; implementação, evidência automatizada, custo e pacote estão entregues. Nenhuma publicação/commit/push nem avanço ao próximo ID.


## R03 aprovado pelo usuário — 10/11/2026

Após receber o roteiro de QA visual do pacote C:/Users/mauri/SB6-QA-R03, o usuário confirmou: "está aprovado". Aceite perceptual registrado nos cenários testados; R03 passa a CONCLUÍDO no checklist. Implementação, core Unity1068/1068, comparação online/offline, interrupções e três pares de custo permanecem como descritos na entrega técnica viewcache2 (protocolo68, assembly2c3bb067…). Todas as limitações de amostragem, ambiente e custo permanecem; o aceite não equivale a medição WAN/GPU/Linux. Seções anteriores com aceite pendente são histórico, superado por esta seção.

Publicação do R03 continua pendente. Este aceite não executou commit/push/deploy nem iniciou outro ID.

Checklist por ID após este aceite: 28/32 itens marcados. A01–A13, C01–C10, R01–R04 e R07 marcados; A14, R05, R06 e R08 continuam parciais. A contagem descreve os checkboxes existentes, não aprovação de todas as pendências de QA global anotadas nos itens marcados. Próximo R pendente: R05; A14 também permanece aberto.


## A14 — implementação e validação técnica finais (10/11/2026)

Status: **implementação/validação técnica concluídas; aceite visual humano pendente**. R03 continua aprovado. Protocolo68 local, sem publicação/commit/push, sem avanço ao R05. Candidato final abilities6, assembly `30bbbefd4bd8ef8457cf0ed4a67636f9203ac8bd899b006d361fad820709236b`; fonte/build/testes em `Logs/A14-abilities6`, integrações em `Logs/A14-peers-abilities6`. Pacote preservado em `C:/Users/mauri/SB6-QA-A14`, com identidade e DLL conferidas. Nenhuma cena/prefab exigiu alteração ou novo wiring.

### Correções comprovadas nesta rodada

- Comandos agrupados: um GetDown por frame conserva o primeiro tick de cada botão, inclusive entre vários FixedUpdates, e não é relabelado pelo último toque do lote. Replay continua apenas de movimento.
- A da Power Glove: silêncio de rede não equivale a soltar o botão. Só o A já recebido é retido até um comando real; caminhada e auto-C continuam expirando. Pause preserva esse hold lógico sem executar ações; reset/remoção de ator mantêm a limpeza existente. Sem fabricar GetDown.
- Lock da luva: finalizar coroutine na borda da pausa não torna InputLocked permanente; restaura baseline e preserva locks de intro/ending ou anteriores. Reproduzido em abilities1, corrigido e retomada validada.
- Predição recém-criada: não herda deltaTime do frame anterior ao input. Um hitch1,2s pré-input não pode apagar o estado em4ms antes do RPC. A expiração não confirmada passa pelo rollback explícito; primeiro passo permanece limitado.
- Carry→Throw: stop do Carry/Lift não confirma um Throw pendente nem reinicia sua fase, mesmo com tick agrupado mais novo. Confirmação correspondente preserva fase; rejeição continua restaurando collider e lock. Reproduzido em abilities5 e corrigido em6.
- Autoridade: StartPunch/Throw/Rubber agora tem a mesma guarda de pure client dos outros entrypoints nativos. Cliente continua apresentando receitas, sem iniciar gameplay.

### Matriz A14 e evidência

| Critério | Evidência final | Estado |
|---|---|---|
| Tick do primeiro comando, inclusive lote/overflow carrier/frame seguinte/rollover | Quatro testes A14BatchedPress… | aprovado |
| Gap não solta A nem duplica press; movimento/auto-C expiram | A14InputSilence… + luva real em dois owners | aprovado |
| Baseline de lock na pausa | Três A14GloveFinishingDuringPause… + pausa/retomada nas integrações | aprovado |
| Kick/Punch/Glove/Blue/Yellow/Rubber por owner/observador | 12 casos nativos por execução, begin+confirm correspondente por dono e apply+samples no outro cliente; Glove exige Lift **e** Throw | aprovado, normal e stall120ms/3s |
| Bounce do jogador | Recovery nativo contra blocker Development, pausa, recuperação de input; receipt/apply nos dois papéis; curva/fila/stop nos testes existentes | aprovado |
| Rubber determinístico/curvas, rejeição, obstáculos, replay e cleanup | Core atual inclui BombAbilityPresentation…, InitialKickPrediction…, RubberBounceSequence…, InactiveOwner… e WorldBombMotion… | aprovado |
| Search/Magnet/Kick com curva/corner/stop | Cenário C08 field-transfer; ambos clientes passaram kind=Magnet/Search/Kick | aprovado |
| Remoção do presenter no arco vivo | Mesmo cenário, ambos clientes têm8 amostras móveis após transferência | aprovado |
| Trailer Carry stop versus Throw e rejeição | Dois A14CarryStop… + confirmação Throw real nos dois owners | aprovado |
| Visual/sensação comparada ao offline | Pacote guiado e jogo livre para Search/Magnet, com roteiro | **pendente humano** |

Core Unity online+COM **1078/1078**, não a suíte inteira do projeto. Tools/Ci166 e RoomDirectory16 aprovados. Compilação dotnet final:0erros; seis avisos de dependências Mirror/Core/Editor/Examples/Edgegap, nenhum nos arquivos alterados; Packages não foram editados para suprimi-los. O primeiro --no-restore não encontrou project.assets.json após Unity limpar Temp; build normal restaurou os artefatos NuGet e passou. UTF-8 estrito verificado nos20 C# alterados/novos; diffcheck sem erros. Avisos de shutdown de ComputeBuffer já existentes não foram declarados corrigidos.

`normal-analysis.json` e `hitches-analysis.json`: PASSED, problems vazio, capturas servidor/clientes válidas. Ambos donos têm predição+confirmação de Kick/Punch/Lift/Throw/Blue/Yellow/Rubber; observadores não preveem comandos alheios e apresentam segmentos. Zero rejeições inesperadas nesses casos válidos. Não confundir checkpoints do servidor com essa prova: o analisador inicialmente recusou abilities5 por Carry confirmar Throw. Todos resultados superados/falhos foram preservados. Cinco testes do analisador impedem aprovar apenas ação nativa/observador, Lift sem Throw, outro bombId, rejeição ou captura vazia/truncada/dropped.

Inspeção Codex dos PNGs finais de throw e Yellow sem corrupção evidente. A silhueta branca do outro player ocioso corresponde a Afk2 no trace, com dead=False, não a uma morte/remoção indevida. Isso não equivale a aceite humano. QA guiado sem PNG evita custo de codificação durante a avaliação; os smokes automatizados mantêm PNGs e logs. Jogo livre separado usa menu nativo, sem inputs sintéticos. Saves isolados; não usa servidor publicado.

Limites explícitos: Windows Development, localhost, dois peers e stalls programados; não mede GPU/hardware de input/WAN/perda real, todos controles físicos ou sessão gráfica host. Offline: geometria/fórmulas e caminho nativo são cobertos pelos testes/referência autoritativa; não foi produzida uma captura offline temporal independente de A14 nesta rodada. Comparação perceptual com offline permanece no aceite humano. Não foram feitas novas medições isoladas de CPU/bytes A14; não transferir custos medidos de R03 para esta DLL nem afirmar custo zero.


### A14 — pacote executado e encerramento técnico (10/11/2026)

O próprio pacote C:/Users/mauri/SB6-QA-A14/run-qa.py normal foi executado: servidor+2clientes visíveis, sem PNG (captureFrames=false),170s, ambos owners completaram7famílias,0erros/avisos de rede do servidor. Análise adicional qa-no-png-analysis.json PASSED, sem problemas. Não é aceite humano; demonstra que o launcher de entrega realmente roda, além dos smokes gráficos com PNG e do teste C08.

Runner restaurado com exit0, conferidos independentemente os22 hashes/ausências originais:18 arquivos restaurados,4 novos removidos somente do runner. Estado em Logs/A14-runner-sync/backup-20261110-124655/state-restored.json. Não houve reset/clean nem alteração de Packages; Library/build/cache não foram revertidos. Implementação permanece no checkout principal. QA preserva a DLL30bbbefd4bd8ef8457cf0ed4a67636f9203ac8bd899b006d361fad820709236b conferida. UTF8 final20 C# válido. Comparação de runtime source antes da restauração passou (normalizando só CRLF/LF).

A14 fica aberto só pelo aceite visual/comparação perceptual offline: 1-normal.cmd (guiado, observar sem enviar comandos), 2-hitches.cmd (stalls deliberados), e jogo livre3/4/5 para Search/Magnet pelo menu nativo. Roteiro em LEIA-ME.md. Nenhuma publicação, commit/push ou avanço a outro ID.


## A14 aprovado pelo usuário — 10/11/2026

Usuário confirmou explicitamente: "Está aprovado A14." Após o roteiro fornecido e a entrega do pacote SB6-QA-A14, aceite humano registrado nos cenários testados. A14 passa a CONCLUÍDO no checklist; os registros anteriores de aceite pendente são históricos, superados por este aceite. Permanecem as evidências e limitações da entrega abilities6/protocolo68 (assembly30bbbefd…), sem ampliar o aceite para cenários não testados ou afirmar medição independente offline/GPU/WAN inexistente.

Estado dos checkboxes:29/32 marcados (90,625%). A01–A14 e C01–C10 marcados; R01–R04 e R07 marcados; R05, R06 e R08 parciais. Algumas linhas marcadas mantêm ressalvas de QA global, que não foram apagadas. Próximo item aberto na sequência: R05. Publicação de R03/A14 permanece pendente; este registro não fez commit, push, deploy nem iniciou outro ID.


## R05 — validação local em andamento (10/11/2026)

Candidate14, protocolo68, assembly `b7c70e056d6440b104901e5a24ab944853d983b3567faea568fa9f3a3cbc59fb`. Implementação local, sem commit/push/publicação. Status atual no checkpoint original.

| Critério | Estado/evidência |
| --- | --- |
| Core Unity online/COM |1088/1088; Logs/R05-candidate14/tests.xml |
| C# |0 erros,0 avisos de código do jogo;6avisos em dependências Mirror não alteradas |
| Integração2clientes renderizados |9/9 aprovados; Logs/R05-peers-candidate14 |
| Trajeto/frame/visibilidade/pausa | aprovados nas amostras verificáveis; gaps/efeitos curtos/transições separados |
| Referências offline por caso |9executadas,96labels correspondentes;1caso Tank sem voo mensurável, órbita/alvos/colisões qualificados em offline-review.json |
| Custo A/B sem captura | em execução; foguetes3pares dentro do orçamento; gelo/Tank/Hall em validação |
| Controle Hall renderizado sem PNG | pendente; isolar retenção correlacionada a captura |
| Revisão de imagens |9cenas inspecionadas; somente estado estático, não aceite perceptual |
| Aceite visual humano | pendente; não inferido dos logs |

Não mede WAN, GPU, input físico ou custo isolado de cada família. A comparação offline não repete exatamente RNG/alvos/colisões. As limitações dos IDs anteriores não foram apagadas nem aceitas por esta rodada.


## R05 — entrega técnica concluída em11/11/2026; aceite humano pendente

Supera o estado em andamento da seção anterior. Candidate14/protocolo68, mesma DLL `b7c70e056d6440b104901e5a24ab944853d983b3567faea568fa9f3a3cbc59fb`; sem publicação/commit/push, sem avanço ao R06.

| Critério | Resultado final |
| --- | --- |
| Core Unity online/COM e C# |1088/1088;0erros/avisos de código do jogo;6avisos de dependências Mirror |
|17famílias /9cenários /2clientes |9/9aprovados; relatórios projectiles-analysis-final.json em Logs/R05-peers-candidate14 |
| Trajeto/frame/visibilidade/pausa | P95 máximo1,862px;0divergências verificáveis de sprite/visibilidade; drift pausado0u onde medido |
| Offline |9referências/96labels;1Tank sem voo suficiente, órbita/AI/contatos qualificados em offline-review.json |
| Custo sem captura |24execuções/3pares por4cenários, dentro do orçamento; maior incremento mediano wire5,525KiB/s; maior aumento central alocação4,073% |
| Controle Hall sem PNG | aprovado;0retenções em movimento>100ms,0drift pausado e0divergências verificáveis nos dois clientes |
| Captura com PNG | preservada1retenção259ms associada a frame187ms; não confundir com o controle sem PNG |
| Pacote QA | C:/Users/mauri/SB6-QA-R05, DLL conferida e fluxo servidor+2clientes testado |
| Runner | arquivos sincronizados restaurados e hashes/ausências conferidos; cache não revertido |
| Aceite perceptual humano | **PENDENTE**, não inferido dos logs/imagens |

Evidência consolidada em Logs/R05-candidate14/{identity,delivery,cost-report,R05-comparacao,offline-review,extra-validation,runner-restored,visual-review}.json.21testes do analisador aprovados; relatório final de CI e RoomDirectory junto do checkpoint. As duas bordas de métricas (hold com gap e hold que inclui pausa) têm testes e contadores separados; não alteram os limites de trajetória.

Limitações preservadas: Windows Development/localhost, custo de CPU headless e controle visual separado; sem medição WAN/GPU/input físico, sem replay integral de RNG, sem extrapolar3pares para todas máquinas. Efeitos<80ms, gaps>150ms e transições sem referência suficiente ficam explícitos. Imagens estáticas não provam suavidade percebida. Checklist permanece29/32até aceite R05; próximos itens de implementação após aceite:R06 e R08.

Conferência final: Python CI187/187, analisador21/21, RoomDirectory16/16;25C# UTF8/BOM e diff sem erros;46entradas restauradas no runner e nenhum processo R05/Unity/SB6 ativo. Identidade do pacote/analisador conferida. Resultado em Logs/R05-candidate14/final-verification.json. Aguarda apenas aceite visual do usuário.
