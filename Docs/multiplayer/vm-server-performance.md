# Servidor Linux: publicação e capacidade

Ensaios de 17–18/10/2026 na VM `sp-dx-sb`: Debian 12, KVM, duas vCPUs Xeon Gold
6138 a 2 GHz, 3.928 MiB de RAM e sem swap. Endereço público
`192.48.205.46`, API TCP 8080 e partidas UDP 7777/7778. O serviço de usuário
`sb6-rooms.service` mantém limite de duas salas, `MemoryMax=3G` e linger ativo.

## Builds e publicação

O build normal é Linux Dedicated Server, Mono, Unity 6000.6.0f1, protocolo 6.
O manifesto `~/sb6/server/deployment.json` identifica commit de origem, estado
local modificado, data UTC e SHA-256 da assembly/executável. A revisão inclui
alterações locais ainda não commitadas; o hash da assembly identifica o artefato
publicado com mais precisão que apenas o commit de origem.

O build Development fica separado dos servidores públicos. O release não contém
`LocalServerLoadTest`, `TickLocalLoadTest` nem a alteração artificial de vida.
`deploy_staged.py` verifica hash e ausência do harness, valida boot/KCP numa
porta interna, exige API sem salas ativas e conserva o diretório anterior antes
da troca. Se a API não reiniciar, restaura a versão anterior. Os logs existentes
ficam preservados fora do diretório substituído.

## Método e limites

- Um processo Unity por sala, mais um gerador KCP compartilhado dentro da VM.
  Ele autentica o protocolo, entra nas salas, recebe/ACK os pacotes, envia
  movimento a 30 Hz e tenta colocar bombas a cada 1,2 segundo.
- O gerador não renderiza nem instancia os mundos dos clientes. Seu consumo
  está separado do RSS/CPU dos servidores; a CPU total do host o inclui.
- Nos testes Development, personagens/inimigos recebem vida 200 periodicamente
  para manter o combate. Boss Rush usa o primeiro chefe, `Stage_1-6`; o cenário
  Normal mais animado usa `Stage_2-5`; Battle usa arena 15 e COM Hard.
- Testes de release usam regras normais. O gerador envia a mesma solicitação
  de configurar/iniciar que o dono humano enviaria, sem comandos de debug no
  servidor. Mortes/progressão podem encurtar as janelas ativas desse ensaio.
- Cada caso espera todos os peers nascerem, aquece 15 segundos e descarta a
  próxima janela parcial. Métricas Unity: dez segundos; host `/proc`: dois.
  Percentis têm resolução de 0,25 ms e a tabela conserva o pior p99 por janela,
  não um percentil global reconstruído.
- Orçamento provisório: mínimo de 58 FPS, p99 até 33,5 ms, menos de 1% de frames
  acima de 33,333 ms e pelo menos cinco janelas completas por sala. Erros
  funcionais são contabilizados separadamente da aprovação do tempo de frame.
- Cada caso roda em unidade systemd própria: limite de 2.500 MiB, 512 tarefas e
  duração máxima de 20 minutos; encerramento elimina apenas seus processos.
  O runner interrompe sob pressão persistente de CPU total acima de 85% ou
  memória disponível abaixo da reserva. Na VM sem swap, o piso efetivo de
  memória durante medição é 1 GiB, além da reserva configurada de 768 MiB.
- O preflight de entrada reserva 300 MiB para o cliente e 32 MiB adicionais por
  sala no Linux headless, após observar o crescimento real. A estimativa inicial
  de 96 MiB por sala veio do player Windows e bloqueou uma tentativa de oito
  salas antes de jogar; essa tentativa não conta como capacidade validada.
- RSS somado pode contar páginas compartilhadas mais de uma vez. No adaptador
  Linux, o campo histórico `privateMiB` representa **VmData**, memória virtual
  de dados, e não commit privado Windows. Para decisões de RAM, use RSS,
  MemAvailable, memória do cgroup e pressão de memória.
- Há registros de CPU steal, pressão de CPU/memória e bytes/drops das interfaces.
  O tráfego loopback inclui os dois lados, ACKs e cabeçalhos; os contadores
  Mirror medem mensagens e não incluem todos os cabeçalhos de transporte.

Os ensaios são curtos e sintéticos. Não demonstram estabilidade por dias, teto
absoluto da VM, todas as fases/chefes, experiência gráfica ou comportamento sob
latência/perda WAN. A conectividade pública é validada separadamente do ensaio
loopback de capacidade. Não trate o resultado do primeiro chefe como limite de
qualquer combinação de modos.

## Correções encontradas pela instrumentação

1. **Tráfego zerado no Linux:** o Mirror limpava seus eventos de diagnóstico em
   AfterSceneLoad, depois de nossa configuração em Awake. A inscrição agora
   acontece no primeiro frame ativo. Zeros das primeiras execuções significam
   contador indisponível, não ausência de tráfego.
2. **Shader em fade simples:** capturar `Image.material` solicitava o shader
   padrão removido no build headless. Fades simples agora transmitem apenas
   cor/visibilidade; a consulta de material fica restrita ao spotlight existente.
3. **Transição durante shutdown:** ao encerrar o processo, callbacks de vários
   peers tentavam voltar ao lobby. O manager marca saída antes de chamar o
   shutdown do Mirror, evitando iniciar cargas de cena durante encerramento.
4. **Gerador e lobby:** o cliente de carga aguardava um objeto de gameplay antes
   de solicitar o início da partida de release. Como o lobby mantém slots lógicos,
   essa dependência impedia o início. A condição agora usa autenticação e estado
   da sala; o ensaio interrompido foi preservado como falha do harness, sem
   atribuí-lo à capacidade do servidor.

As execuções anteriores às correções permanecem no conjunto bruto, incluindo
os erros de shutdown. Não foram apagadas nem contadas como execuções sem erros.

## Revisão de replicação e headless (protocolo 6)

- Louie mantém seus sprites e o tempo de vida das imagens residuais no servidor,
  necessários às capturas cooperativas. No build dedicado, a resolução do material
  retorna o material padrão sem consultar shaders removidos pelo Unity. O caminho
  gráfico de cliente/offline continua tratando o material de submersão.
- LineBomb passa a ter uma única NetworkIdentity na raiz do item. A lista de
  spawnables do OnlineLobby também referencia essa raiz, em vez de DestroyAnimation.
- Mole/Tank compartilham prefabs entre o mundo e a montaria anexada ao jogador.
  `NetworkWorldMount` replica o pickup por um proxy separado, com pose, frames,
  cores e visibilidade autoritativos. Os clones de cliente têm física e gameplay
  desativados. Consumo/destruição remove o proxy; a montaria equipada continua
  usando NetworkPlayerState/NetworkMountAnimation. Os prefabs compartilhados
  permanecem sem identidades aninhadas.
- A desmontagem também precisa registrar o pickup, sem passar pela revelação de
  item do BombController. MountWorldPickup faz esse registro no Start, de forma
  idempotente. O proxy usa o catálogo existente do PlayerMountCompanion para todas
  as nove montarias, incluindo Louies. Select segue pelo comando normal de input;
  a desmontagem é executada somente pelo servidor (offline mantém a simulação
  local). O catálogo pode ser consultado em um prefab sem executar seu Awake.
- O protocolo muda para 6 porque clientes antigos não conhecem o novo prefab.
  O deploy verifica e troca também o supervisor da API, preservando sua cópia
  anterior para rollback. Servidor, API e cliente devem ser atualizados juntos.
- O gerador de carga deixa de enviar controles depois de NotReady. A contagem
  final de personagens distingue o lobby (slots conectados, sem personagens)
  de partidas artificiais duráveis. Resultados antigos não foram reclassificados
  como testes de capacidade aprovados.
- A primeira repetição encontrou outra origem de aviso de shader: as telas de
  empate/placar/vitória chamavam Canvas.ForceUpdateCanvases no dedicado. Essas
  chamadas agora ficam restritas aos builds com gráficos, preservando as rotinas
  e os tempos de apresentação. Os clientes continuam atualizando seus canvases.
- Clientes normais não enviam comandos de gameplay enquanto não estão Ready ou
  enquanto pausa/apresentação bloqueia os controles. O gerador artificial também
  interrompe os controles ao receber RoundOver, antes da mudança de cena.

As métricas de versão 2 identificam `includesTransition` quando há apresentação
de fim de rodada, carga de cena ou troca do identificador da cena (inclusive
recarregar a mesma arena). Essas janelas continuam no arquivo bruto, mas ficam
fora do orçamento de **combate estável**; o resumo registra quantas foram excluídas.
Nenhum erro/aviso é excluído por essa marcação. A memória/CPU do host continua
medida durante toda a execução. Os resultados anteriores não tinham essa marca
e incluíam os stalls das transições; a comparação deve considerar essa diferença.

Marcadores e GC indisponíveis em release aparecem como `null`/célula vazia nos
resumos, nunca como zero nem como uma soma de valores negativos. Os arquivos
brutos preservam o indicador original `-1`.

## Referência anterior às correções finais

| Cenário sintético | Duração medida | CPU total da VM | RSS dos servidores | FPS mínimo / p99 | Diagnóstico |
| --- | ---: | ---: | ---: | --- | --- |
| 8 salas Boss Rush, 32 humanos artificiais duráveis | 90 s | 59,8% | 2.183 MiB | 59,30 / 22 ms | Sem erros/avisos; somente primeiro chefe |
| 2 salas Normal, Stage_2-5, 8 humanos duráveis | 90 s | 19,3% | 554 MiB | 59,98 / 16,75 ms | Sem erros/avisos; tráfego de saída total ~900 KiB/s |
| 2 salas Battle, 4 humanos + 8 COM Hard | 180 s | 38,0% | 579 MiB | 58,38 / 27,75 ms | 9 erros de spawn e 16 avisos de shader |
| 4 salas Battle, 8 humanos + 16 COM Hard | 90 s | 71,8% | 1.126 MiB | 56,72 / 44 ms | 10 erros de spawn e 966 avisos de shader; fora do orçamento de frames |

Os avisos e seus stack traces fazem parte do custo medido. Não se pode atribuir
toda a diferença entre cenários exclusivamente à IA, nem extrapolar oito salas
do primeiro chefe para oito salas em qualquer modo. COM.Update e COM.Think são
marcadores aninhados: seus tempos não devem ser somados.

## Logs e reprodução

Na VM, os resultados brutos ficam em `~/sb6/benchmarks/`; cada sala tem
`metrics.jsonl`, `events.jsonl` e `unity.log`, e cada caso tem `host.jsonl`,
`config.json` com hash do build e `summary.json` ou `failure.json`.
`results.csv`/`results.json` são gerados por `summarize_benchmarks.py`.
Os logs normais do serviço continuam em `~/sb6/logs/` com a retenção já existente.

```sh
python3 ~/sb6/bench-tools/benchmark_vm_job.py novo-boss-2 -- \
  --binary ~/sb6/benchmark-server-replication-v6-final/SB6.x86_64 \
  --mode BossRush --rooms 2 --players 4 --seconds 180
XDG_RUNTIME_DIR=/run/user/1000 systemctl --user status sb6-bench-novo-boss-2
python3 ~/sb6/bench-tools/summarize_benchmarks.py ~/sb6/benchmarks
XDG_RUNTIME_DIR=/run/user/1000 journalctl --user -u sb6-rooms -f
tail -F ~/sb6/logs/supervisor.log
```

Use nomes de caso novos. Não execute casos simultâneos para comparar capacidade.
Os testes internos usam UDP 18777 em diante e não aumentam o limite da API pública.

O teste Windows `smoke_full_clients.py` usa dois executáveis de cliente normais,
não os peers artificiais do benchmark. Ele revela/remove os três prefabs afetados
em um servidor Development, exige os checkpoints e verifica os logs dos três
processos antes de encerrar apenas os processos que criou. É um teste sem render:
valida a execução da replicação, não o aspecto visual ou os controles humanos.
Com `--mount-input`, usa clientes Development completos e injeta Select no
PlayerInputManager. O servidor equipa Mole/Tank, confirma a desmontagem por
comando normal e verifica o registro dos pickups devolvidos ao chão. A entrada
de teste e a preparação artificial existem somente no build Development.

```powershell
python Tools/RoomDirectory/smoke_full_clients.py `
  --server Build/ServerBenchmark/Windows/SB6.exe `
  --client Build/OnlineClient/Windows/SB6.exe `
  --output Build/Validation/VmDeployment/FullClientSpawnV6

python Tools/RoomDirectory/smoke_full_clients.py `
  --server Build/ServerBenchmark/Windows/SB6.exe `
  --client Build/ServerBenchmark/Windows/SB6.exe --mount-input `
  --output Build/Validation/VmDeployment/FullClientInputV6
```

O comando `Tools > Super Bomberman 6 > Online > Use Public Server` apenas remove
a preferência local de diretório em PlayerPrefs. Não modifica código/builds e não
interfere nos ensaios que usam `-online-join` ou o gerador KCP com destino explícito.


## Revalidação final das correções (18/10)

Todos os quatro casos abaixo usam o mesmo build Development Linux final,
protocolo 6, métricas versão 2. CPU é a média total da VM, incluindo o gerador;
RSS é o pico somado dos processos de sala. Nenhum caso registrou erros ou avisos.

| Caso | Salas | Duração medida | FPS mínimo | Pior p99 | RSS | CPU VM | Orçamento |
|---|---:|---:|---:|---:|---:|---:|---|
| Battle, 6 humanos/sala | 2 | 90 s | 59,94 | 17,00 ms | 555 MiB | 15,9% | passou |
| Battle, 2 humanos + 4 COM Hard/sala | 2 | 180 s | 58,23 | 29,50 ms | 585 MiB | 37,2% | passou |
| Battle, 2 humanos + 4 COM Hard/sala | 4 | 90 s | 54,08 | 48,75 ms | 1.076 MiB | 75,6% | excedeu |
| Normal, Stage_2-5, 4 humanos/sala | 2 | 90 s | 59,98 | 16,75 ms | 548 MiB | 19,1% | passou |

O limite público continua em **duas salas**, para suportar a combinação com COM.
Quatro salas com COM não atendem ao orçamento. Isso não determina um teto absoluto
para todas as combinações; o teste antigo de oito salas no primeiro chefe não
justifica aumentar a capacidade pública geral.

As janelas versão 2 marcadas `includesTransition` continuam nos logs brutos, mas
não entram no orçamento de combate. Cinco janelas de transição foram excluídas
no teste final de duas salas com COM. Erros/avisos não são filtrados, e CPU/RSS do
host incluem toda a duração medida. Os valores antigos incluem transições e não
são uma comparação causal de desempenho; arenas/COM também têm aleatoriedade.

O teste final de duas salas com COM teve aproximadamente 9,82 MiB/s de alocação
GC somada, COM.Update 4,02 ms/frame/sala e COM.Think 2,07 ms dentro de Update.
O próximo perfil deve localizar alocações e consultas dentro do COM antes de
alterar frequência/qualidade da IA. Não se deve atribuir toda a alocação do
processo ao COM apenas por esses contadores, nem somar Think a Update.

As correções removem consultas de shader dos afterimages no headless e evitam
atualizações forçadas de Canvas nos overlays do servidor. LineBomb tem identidade
na raiz. Os nove tipos de montaria usam um proxy de apresentação de pickup, sem
introduzir identidades aninhadas nos jogadores. Select e desmontagem passam pela
autoridade do servidor. Nenhum log de erro foi simplesmente suprimido.

O teste `FullClientInputV6R3` executou dois clientes Mirror completos sem render,
por 210 segundos de servidor: revelou/removeu LineBomb/Mole/Tank, desmontou
Mole/Tank por Select, confirmou ambos os clientes e o registro dos pickups,
recarregou a arena e encerrou sem erros ou avisos. A repetição com dois clientes
Windows de release foi impedida pela reserva mínima de 3 GiB de RAM; não conta
como teste executado. A validação pública de release é registrada separadamente.

Validação de código: 45 testes Unity passaram (`OnlineHandoffTests.xml`), cinco
testes Python da API passaram e a compilação SDK terminou sem erros. Os seis
avisos SDK existentes pertencem ao pacote Mirror/Examples. A última compilação
Unity Development terminou com código 0. UTF-8 foi verificado nos 23 C# alterados.
Ainda falta inspeção gráfica, controles humanos/offline e uma partida WAN humana.

Os casos release com regras normais são ensaios funcionais de conexão/fluxo,
sem estimativa de capacidade quando não produzem cinco janelas estáveis por sala.
Falhas anteriores do avaliador foram preservadas: ausência de janelas estáveis
não significa erro de gameplay; `complete` foi comparado incorretamente a
`completed` na primeira execução do avaliador de fluxo e depois corrigido.


## Publicação final e conectividade

Publicado em 18/10/2026: assembly SHA-256
`4bd291768d0e837320a05d1bb7ca1e2ab429877ab25cd4c65ac48787f0799f7d`.
Rollback: `~/sb6/server-before-replication-v6-final`, acompanhado do supervisor
anterior. API health confirmou protocolo 6; protocolo 5 recebeu HTTP 409 e a
terceira sala recebeu HTTP 429 por capacidade. UDP público 7777/7778 respondeu
KCP com 33 bytes, RTT aproximado de 109/94 ms nesta amostra.

Boss Rush release manteve oito conexões em duas salas por 60 segundos medidos,
sem erros/avisos. Normal Game release manteve oito por 30 segundos medidos,
sem erros, com três avisos NotReady do gerador sintético durante transição.
São testes de fluxo, não limites de capacidade. Uma tentativa de Normal foi
interrompida ao detectar sobreposição com o fluxo Boss; não conta como medição.

Resultados brutos, inclusive tentativas com falha, foram preservados em
`Build/Validation/VmDeployment/benchmarks-replication-v6-final.tar.gz` e extraídos
em `RemoteResultsV6`. O CSV versionado contém tanto referências antigas quanto
casos finais; os nomes `v6-final`/`v6-r3` identificam as repetições aqui descritas.


O cliente Windows normal `Build/OnlineClient/Windows/SB6.exe` autenticou no lobby
público Normal Game, apareceu na API com um jogador e permaneceu conectado por
10 segundos sem erros de cliente (`PublicClientV6/result.json`). Não iniciou uma
partida. Ao encerrar o processo do cliente, o servidor registrou timeout KCP após
10 segundos e fechou a sala do dono, comportamento esperado para desconexão sem
aviso; esse aviso foi conservado. A sala Boss vazia expira pelo limite de 60 s.
O serviço estava ativo, sem reinícios, com 13 GiB livres no disco e rollback pronto.
