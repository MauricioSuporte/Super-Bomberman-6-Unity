# Desempenho do servidor local

Ensaio em 17/10/2026. Os números deste documento se referem ao Windows local,
não à VM Linux. Resultados e logs brutos ficam em `Build/Validation/ServerBench`
(não versionado); `results.json` e `results.csv` são gerados pelo agregador.
Uma cópia compacta dos resultados está em
[`local-server-performance-results.csv`](local-server-performance-results.csv).

## Ambiente e método

- Unity 6000.6.0f1, Mono, player Windows Development com profiling, executado
  com `-batchmode -nographics`. Não é o subtarget Dedicated Server do Linux.
- Windows informou aproximadamente 16 GiB de RAM e 16 processadores lógicos;
  identificação da CPU: AMD Ryzen 5 1600 Six-Core Processor. Outros aplicativos
  permaneceram abertos. O Editor foi fechado para as medições.
- Um processo Unity por sala; um processo adicional mantém vários clientes KCP
  leves. Há autenticação, Ready/AddPlayer, observadores reais, recepção e ACK dos
  pacotes, comandos de movimento a 30 Hz e tentativa de bomba a cada 1,2 s.
- O gerador não instancia nem renderiza o mundo no cliente. Este ensaio mede
  simulação/serialização/transporte do servidor, não desempenho gráfico, rede
  externa, perda de pacotes ou experiência completa dos clientes.
- Personagens e inimigos recebem vida 200 a cada segundo, apenas no harness de
  desenvolvimento. Isso mantém o combate em execução sem esvaziar a fase após
  mortes. Não representa as regras normais nem cobre todas as fases de um chefe.
- Boss Rush: primeiro encontro, Stage_1-6. Normal: Stage_1-1. Battle: arena 15,
  seis humanos sintéticos. Casos adicionais usam Stage_2-5 e Battle com dois
  humanos sintéticos + quatro COM Hard por sala.
- Cada execução só começa a medir depois de todos os peers nascerem. Descarta
  15 segundos de aquecimento e a janela parcial seguinte do logger. As métricas
  Unity são registradas a cada dez segundos; o host é amostrado a cada dois.
- Percentis usam histograma com resolução de 0,25 ms. A tabela mostra o maior
  p99 entre as janelas/salas, não um percentil global recalculado de todo o teste.
- Critério provisório: janela mínima de 58 FPS, p99 até 33,5 ms, menos de 1% de
  frames acima de 33,333 ms, cinco janelas completas por sala e nenhum erro.
- Reserva durante os ensaios: 512 MiB de RAM disponível e 1 GiB de commit
  disponível. Há interrupção sob pressão persistente de memória ou CPU total
  acima de 85%. Apenas os processos filhos do runner são encerrados.

RSS e memória privada são lidos por GetProcessMemoryInfo. CPU usa
GetProcessTimes, onde 100% significa um processador lógico inteiro. A soma de
RSS pode contar páginas compartilhadas mais de uma vez; também são mantidos
commit privado e memória livre do host. O Mono Windows não disponibilizou RSS
no logger interno (`-1`), por isso a leitura externa é necessária.

## Capacidade observada antes das otimizações

| Cenário | Salas | Humanos sintéticos | RSS dos servidores | FPS mínimo | Maior p99 |
|---|---:|---:|---:|---:|---:|
| Boss Rush | 1 | 4 | 304 MiB | 60,00 | 16,75 ms |
| Normal Game | 1 | 4 | 322 MiB | 59,98 | 16,75 ms |
| Battle Mode | 1 | 6 | 372 MiB | 59,84 | 18,50 ms |
| Boss Rush | 2 | 8 | 609 MiB | 59,46 | 19,25 ms |
| Boss Rush | 4 | 16 | 1.216 MiB | 60,00 | 16,75 ms |
| Boss Rush | 6 | 24 | 1.828 MiB | 59,94 | 17,00 ms |

As execuções completas acima mediram 60 segundos, com cinco janelas de dez
segundos por sala após o descarte. O teste com oito salas foi interrompido ao
entrar os jogadores, com aproximadamente 401 MiB de RAM livre. Não existe
resultado estável de oito salas nessa execução. A pressão de memória, e não
saturação de CPU demonstrada, encerrou essa tentativa.

## Otimizações implementadas

- Reutilização de listas, conjuntos e dois dicionários alternados por tilemap,
  mantendo os snapshots anterior/atual sem reconstruir suas capacidades.
- Construção de cada lote de deltas uma vez, compartilhada pelos observadores
  durante a serialização síncrona, em vez de copiar listas/arrays por cliente.
- Renderers de pools continuam descobertos, inclusive inativos, para preservar
  ativações curtas a 20 Hz. Objetos adormecidos não repetem leitura completa de
  transform/material/sprite; a transição visível -> oculto ainda é transmitida.
- Snapshots completos de configuração continuam em entrada/transição e no
  configurador Battle. O tick periódico de lobby/partida não reenvia o JSON.
- A atualização periódica de estado Boss Rush roda apenas nesse modo; Normal
  ainda recebe o estado inicial necessário ao começar sua sessão.
- A IA reutiliza as consultas de itens, montarias e explosões dentro de uma
  única decisão síncrona. Descarta o snapshot ao terminar, inclusive em exceções;
  a decisão seguinte e ações fora dela consultam a cena novamente. Regras e
  frequência das decisões permanecem iguais.

## Resultados após as otimizações

| Cenário | Salas | Participantes por sala | RSS dos servidores | FPS mínimo | Maior p99 |
|---|---:|---|---:|---:|---:|
| Boss Rush | 1 | 4 humanos sintéticos | 304 MiB | 59,96 | 17,00 ms |
| Normal Game | 1 | 4 humanos sintéticos | 317 MiB | 59,99 | 16,75 ms |
| Battle Mode | 1 | 6 humanos sintéticos | 335 MiB | 59,98 | 17,00 ms |
| Normal Game, Stage_2-5 | 4 | 4 humanos sintéticos | 1.298 MiB | 59,92 | 17,00 ms |
| Battle Mode com COM | 2 | 2 humanos sintéticos + 4 COM Hard | 623 MiB | 59,12 | 17,00 ms |
| Battle Mode com COM | 4 | 2 humanos sintéticos + 4 COM Hard | 1.390 MiB | 58,88 | 29,75 ms |
| Boss Rush, 180 segundos | 6 | 4 humanos sintéticos | 1.841 MiB | 58,31 | 18,25 ms |
| Boss Rush | 8 | 4 humanos sintéticos | 2.427 MiB | 59,17 | 17,00 ms |

Os casos Battle têm erros funcionais descritos abaixo. Passar no orçamento de
frames não os torna funcionalmente aprovados. As demais execuções completas
não registraram erros nos logs coletados.

No comparativo de uma sala Battle, o envio caiu de 285,54 para 16,16 KiB/s
(94,3%), principalmente por não repetir o JSON completo de configuração durante
a partida. No cooperativo, alocações por segundo caíram de 979,19 para
474,33 KiB/s no primeiro chefe (51,6%) e de 949,09 para 371,63 KiB/s na primeira
fase Normal (60,8%). São alocações, não redução equivalente da RAM residente.
O tempo do marcador de apresentação cooperativa no primeiro chefe caiu de
0,155 para 0,098 ms por frame (37%). O tráfego cooperativo varia com a cena e RNG;
não houve redução demonstrada nessa comparação.

Antes do cache de consultas da IA, quatro salas com quatro COM Hard cada
registraram janela de 54,05 FPS e p99 de 56 ms. Depois: 58,88 FPS e 29,75 ms.
Isso removeu a violação do orçamento provisório nesse cenário, mas não prova
um limite de capacidade para todas as arenas, itens e situações de COM.

Stage_2-5 enviou aproximadamente 1.777 KiB/s somando quatro salas (cerca de
444 KiB/s por sala), muito mais que a primeira fase. Dimensionar a rede apenas
pela fase inicial subestima a carga de fases mais animadas.

O ensaio de seis salas durou três minutos, com 17 janelas completas por sala.
Apesar do p99 baixo, houve um frame isolado de 292 ms; no ensaio de oito salas,
o maior foi 181 ms. A causa desses picos não foi isolada. Percentis não devem
ocultar pausas raras; um teste prolongado continua necessário.

## Capacidade e pendências

O máximo **validado neste ensaio** foi oito salas Boss Rush / 32 jogadores
sintéticos. Não é o teto absoluto da máquina: restaram apenas 800 MiB de RAM
disponível no pior momento, e os servidores somaram 3.501 MiB de memória privada.
A tentativa anterior de oito salas falhou por memória com outros aplicativos
consumindo mais RAM. A diferença não pode ser atribuída apenas às otimizações.
Oito salas também não foram validadas com COM ou todas as fases.

Mantenha inicialmente o limite operacional local de **quatro salas**, que já
está configurado no menu Tools, para deixar margem ao Editor, ao cliente gráfico
e a picos. A maior restrição observada para ampliar o teste foi memória livre;
não foi demonstrada saturação geral de CPU. Um processo Unity por sala ainda
custa aproximadamente 300–370 MiB de RSS nos cenários medidos.

Os testes Battle revelaram erros `[NetSpawn]` em `LineBomb(Clone)`,
`Mole(Clone)` e `Tank(Clone)`: o objeto passado ao spawn não tem NetworkIdentity
na raiz. Em LineBomb há identidade num filho, o que não satisfaz a busca atual
de NetSpawn na raiz. Esses erros também apareceram nos ensaios anteriores à
otimização. É preciso corrigir a autoria/registro desses prefabs e validar
pickups/montarias em clientes completos antes de aprovar Battle para produção.
Os prefabs já tinham alterações locais e foram preservados neste trabalho.

## Reproduzir

No Editor, use `Tools > Super Bomberman 6 > Online > Build Performance Test Player`.
O harness só compila no Editor/debug e exige `Debug.isDebugBuild` e argumentos
locais explícitos. Nenhuma mensagem de rede ativa esse comportamento. O player
normal de release não contém o gerador nem o código que altera vida/início.

Feche o Editor para isolar a carga e execute, na raiz do projeto:

```powershell
python Tools/RoomDirectory/benchmark_local.py --output Build/Validation/ServerBench/new-boss-6 --mode BossRush --rooms 6 --players 4 --seconds 180 --reserve-mib 512
python Tools/RoomDirectory/benchmark_local.py --output Build/Validation/ServerBench/new-com-4 --mode BattleMode --rooms 4 --players 2 --com --seconds 60 --reserve-mib 512
python Tools/RoomDirectory/benchmark_local.py --output Build/Validation/ServerBench/new-heavy-4 --mode NormalGame --rooms 4 --players 4 --scene Stage_2-5 --seconds 60 --reserve-mib 512
python Tools/RoomDirectory/summarize_benchmarks.py Build/Validation/ServerBench
```

Cada pasta de saída deve ser nova. Use `--binary` para escolher outro build;
o baseline deste ensaio foi preservado em `Build/ServerBenchmark/BaselineWindows`.
Os casos iniciais desse baseline não têm os switches adicionais de COM/fase.
O build intermediário, antes do cache de consultas da IA, foi preservado em
`Build/ServerBenchmark/OptimizedWorldWindows`; o build de desempenho atual fica
em `Build/ServerBenchmark/Windows`.
O runner usa portas 18777 em diante, sem passar pela API 18081 nem pelo limite
de quatro salas configurado no menu de servidor local.

## Limites da conclusão

Estes são ensaios sintéticos curtos e uma faixa de capacidade validada nas
condições registradas. Não demonstram um teto absoluto, capacidade de todas as
arenas/chefes, nem estabilidade de horas. Mais memória livre em outro momento
pode mudar o ponto de interrupção; os aplicativos abertos também variaram.
O custo de um processo Unity por sala continua significativo. Reduzir garbage
e tráfego não elimina esse piso de memória.

Não transfira a quantidade de salas para a VM de 2 vCPUs/4 GiB. Ela precisa de
medição própria com partidas, incluindo as pausas previamente observadas nos
logs. Nenhum limite de produção foi aumentado por este ensaio.

## Validação do código

- C# (`dotnet build Assembly-CSharp-Editor.csproj -m:1 -v:q`): zero erros,
  seis avisos existentes em Mirror/vendor; nenhum aviso novo do jogo.
- API/supervisor: cinco testes Python passaram.
- Online: 31/32 passaram na execução conjunta; o caso de lobby que falhou com
  erro de recarga de domínio passou na repetição isolada. Os dois testes novos,
  de deltas de tiles e percentis/overflow, passaram na primeira execução.
- A suíte COM existente foi executada como descoberta pelo Unity (Play Mode):
  77/92 passaram e 15 falharam. Os logs mostram falhas de preparação, incluindo
  objetos sem um collider concreto exigido por seus componentes, seguidas de
  alvos nulos em reflection. Não é uma suíte aprovada. Uma tentativa em assembly
  Editor também não resolveu as fixtures; sua organização original foi mantida.
  Os resultados XML de ambas as tentativas foram preservados junto aos ensaios.
- Há erros de importação anteriores em `BirdRobotBlue 1.prefab.meta` e
  `SnowBall.mp3.meta`. Esses arquivos não foram alterados para mascarar logs.
- Os arquivos C# alterados passaram na verificação estrita de UTF-8/BOM.
- Build Windows normal: `Succeeded`, zero erros, 967 MiB, em
  `Build/OnlineClient/Windows/SB6.exe`. A DLL de release não contém
  `LocalServerLoadTest` nem `TickLocalLoadTest`; a DLL Development contém o
  harness, confirmando a exclusão dos controles de benchmark no player normal.

Não foi feita certificação gráfica com dois clientes completos, nem validação
WAN, controle/mobile ou todas as combinações de fases e montarias neste ensaio.
