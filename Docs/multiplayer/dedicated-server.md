# Salas em servidor dedicado

O fluxo é `TitleScreen → Game Modes → Online → OnlineLobby`. Criar sala chama
o supervisor HTTP da VM, que inicia um processo Unity headless com uma porta
KCP exclusiva. O cliente aguarda o processo ficar pronto e conecta como
qualquer outro participante. O mesmo supervisor pode ser iniciado localmente pelo menu Tools descrito abaixo.

O primeiro participante é o dono. Com pelo menos um humano conectado durante o período de release, ele pode abrir
a configuração nativa de Battle Mode; os demais acompanham os painéis e
escolhem seus personagens na própria vez. Se o dono desconectar, o processo
encerra a sala e desconecta todos. A saída de qualquer participante também encerra a sala para todos e retorna
os clientes à tela de título. Não há transferência de dono.

Movimento, física, bombas, dano, COM e decisões de partida executam no servidor.
Clientes enviam controles a 30 Hz e recebem transformações, estado e animação.
Não há predição local nesse fluxo; a latência até a VM influencia a resposta.
Offline continua usando a simulação local. O protocolo 7 rejeita clientes antigos.

## Rede e implantação

`Assets/Resources/OnlineRoomDirectory.json` contém a URL do supervisor.
O endereço configurado é `http://192.48.205.46:8080`; a API foi consultada pelo
IP público e pelo Tailscale `100.64.0.46`. A VM possui esse IP público em eth0.
O cliente permite HTTP somente para esses dois endereços conhecidos ou loopback.
A API contém o catálogo público de salas, sem credenciais SSH ou chaves privadas.
Para usar um domínio HTTPS, altere a URL; mantenha `--host` como o endereço KCP
alcançável pelos clientes. Disponibilize TCP 8080 (ou HTTPS no proxy) e as portas
UDP das salas. Disponibilizar apenas SSH ou HTTP não valida o tráfego de jogo.

1. Instale Linux Mono e Linux Dedicated Server Build Support da versão do Editor.
2. Gere `Build → Linux Dedicated Server` e `Build → Online Client (Windows)`.
3. Copie todo `Build/LinuxServer/` para `/home/mauricio/sb6/server/`.
4. Copie `Tools/RoomDirectory/supervisor.py` para `/home/mauricio/sb6/` e torne
   `server/SB6.x86_64` executável.
5. Use a unidade de usuário `sb6-rooms.service` em `~/.config/systemd/user/`:
   `systemctl --user daemon-reload` e `systemctl --user enable --now sb6-rooms`.
   `loginctl show-user mauricio -p Linger` deve indicar `yes` para sobreviver ao SSH.

O supervisor tem limite de três salas simultâneas, portas UDP 7777 a
7779. Esse limite é uma proteção de recursos, não uma capacidade já medida.
Cada processo mantém estado separado, expira sem ocupantes após 60 segundos
e é removido se encerrar ou deixar de atualizar o status por 30 segundos.
O lobby encerra a sala após cinco minutos sem iniciar, com contador visível.
Após iniciar a configuração, cinco minutos sem inputs humanos encerram a sala;
o aviso aparece apenas no último minuto. Inputs mantidos contam como atividade,
mas pacotes de rede sem ações não renovam o prazo. O relógio usa tempo real,
inclusive durante a pausa. O deploy migra o limite do serviço existente para três
salas e restaura a configuração anterior se a ativação falhar.

Não é necessário Docker. O serviço usa a conta da aplicação, sem sudo.

## Logs e custo das partidas

`/home/mauricio/sb6/logs/supervisor.log` registra criação/saída de processos e
falhas de alocação; possui rotação de 5 MB com três cópias.
Cada sala tem uma pasta identificada pelo ID com `unity.log`, `events.jsonl` e
`metrics.jsonl`, além de `launcher.log` para falhas nativas anteriores ao
carregamento do Unity. Os arquivos JSON separam eventos/erros das amostras de consumo. Ali ficam
exceções com stack trace, conexões, saída do dono, trocas de cena e métricas
`[ServerMetrics]` a cada dez segundos, inclusive quando o jogo está pausado.
O supervisor limita cada arquivo ativo a aproximadamente 10 MB com três cópias,
usando copy/truncate; uma linha pode se perder na fronteira da rotação.
Logs de salas encerradas são preservados dentro de um orçamento total de
512 MiB para as pastas de salas; a cada minuto, os históricos mais antigos
são removidos se o orçamento for ultrapassado. Logs de salas ativas não são
removidos por essa limpeza.

As métricas incluem CPU do processo (100% = um núcleo, 200% = dois), RAM
residente, memória gerenciada e alocada pelo Unity, coletas GC, FPS, maior
intervalo de frame, quantidade de objetos de rede e KiB/s de mensagens Mirror.
O tráfego medido não inclui cabeçalhos/retransmissões do transporte.

`Build → Linux Dedicated Server (Profiling)` gera o Development Build separado
em `Build/LinuxServerBenchmark/`, preservando o release em `Build/LinuxServer/`,
para capturar os marcadores existentes de movimento, física do jogador,
bombas, COM, habilidades, arena e input. As médias são por frame e podem se
sobrepor por conter escopos aninhados; não devem ser somadas. `-1` significa
marcador indisponível, não custo zero. Esse build tem overhead de diagnóstico;
compare a capacidade final com o build normal.

Comandos úteis na VM:

```sh
journalctl --user -u sb6-rooms -f
tail -F ~/sb6/logs/supervisor.log
grep '\[ServerMetrics\]' ~/sb6/logs/*/unity.log | tail -20
ps -C SB6.x86_64 -o pid,%cpu,rss,etime,args
```

Meça primeiro uma sala com dois humanos, depois seis slots com humanos/COM,
bombas, montarias, Sudden Death e rounds consecutivos. Repita com duas salas
simultâneas. Compare médias e picos antes de aumentar `--capacity`; não estime
quantidade de partidas apenas pela RAM livre. Valide saída do dono, entrada
durante configuração, sala cheia, versão antiga e retorno ao offline.

Para ordenar os custos por sala, cena e quantidade de jogadores:

```sh
python3 ~/sb6/report_metrics.py ~/sb6/logs
```


## Validação na VM em 17/10/2026

O build Linux dedicado com profiling compilou com zero erros e foi instalado
em `~/sb6/server`. O serviço de usuário `sb6-rooms` está ativo e a API pública
responde em `http://192.48.205.46:8080`. Duas criações reais alocaram portas
7777 e 7778; uma terceira recebeu HTTP 429 por capacidade. Uma sala sem
jogadores encerrou por inatividade com código 0 e foi removida pelo supervisor.

No primeiro lobby vazio, cinco amostras registraram CPU média de 1,94% de um
núcleo e pico de RAM residente de 314,44 MiB. O maior intervalo de frame
incluiu uma pausa de 6,8 segundos durante inicialização; esses números não
representam desempenho de partidas nem permitem estimar sua capacidade.

Os 23 testes Edit Mode de online e quatro testes Python do supervisor
passaram. O teste de rede anterior encontrou a API acessível, porém os pacotes
UDP enviados pelo Windows às portas 7777/7778 não chegaram à VM, tanto pelo
IP público quanto pelo Tailscale. O administrador foi acionado. A validação
com dois clientes, configuração, partida, desconexões e carga simultânea
permanece pendente de conectividade e deve preceder qualquer aumento do limite.

O cliente Windows também compilou com zero erros, em
`Build/OnlineClient/Windows/SB6.exe`. No Play Mode, o cliente acessou a API,
criou uma sala e iniciou a conexão KCP pública; não houve conexão estabelecida
na tentativa enquanto a liberação UDP estava pendente.


## Servidor de teste local no Windows

No Unity, use `Tools > Super Bomberman 6 > Online > Build and Start Local Server`.
Isso compila o cliente Windows (também usado com `-server -batchmode -nographics`)
e inicia o supervisor Python em loopback
(API 18081, UDP 17777–17780). Python 3 deve estar no PATH; alternativamente,
defina `SB6_PYTHON` com o caminho do executável antes de abrir o Editor.
`Start Local Server (Existing Build)` reutiliza o último build, que precisa ser
recompilado após alterações de gameplay. `Stop Local Server` encerra também
as salas. Os logs ficam em `Build/LocalServerLogs`.

`Use Local Server` seleciona o diretório local no Editor e no cliente Windows
com o mesmo perfil de usuário. Saia e entre novamente no lobby após trocar.
`Use Public Server` restaura o endereço do JSON. Também é possível passar
`-online-directory http://127.0.0.1:18081` ao executável do cliente.

Protocolo 7: POST /rooms recebe `mode` (`BattleMode`, `NormalGame`, `BossRush`).
O modo é fixado no processo com `-room-mode` e anunciado junto com `maxPlayers`.
Battle Mode permite seis slots; Normal Game e Boss Rush permitem quatro.
Não existe mensagem nem endpoint para alterar o modo de uma sala criada.
O dono inicia Normal Game ou Boss Rush com um a quatro participantes durante o release. Normal
Game passa por SkinSelect, SaveFileMenu e WorldMap usando o save do dono;
Boss Rush passa por SkinSelect e pela escolha nativa de dificuldade. Battle Mode
carrega a configuração salva do dono. Veja [save do dono e CI](online-owner-save-and-ci.md).
A campanha cooperativa usa uma cópia completa do save em memória. Escritas e
reloads de disco ficam bloqueados durante a sessão; ao sair, a referência ao
save offline é restaurada.

## Replicação cooperativa das fases

Os jogadores continuam usando os componentes Mirror existentes. Os scripts de
inimigos, chefes, projéteis e mecanismos da fase executam no servidor. No cliente,
a simulação desses objetos de cena é desativada antes de Start; câmera, UI e HUD
continuam ativos. A apresentação recebe deltas de sprites a 20 Hz e de tilemaps
a 4 Hz, com lotes limitados e uma sincronização completa para cada cliente pronto.
Sprites gerados em runtime e pequenas texturas geradas por efeitos são enviados
sob demanda, com limite de 65.536 pixels por textura. Objetos já replicados pelo
Mirror mantêm seu próprio caminho e ficam fora desse fluxo de sprites.

`CoopSceneCatalogBuilder` gera catálogos por fase, com referências ordenadas por
GUID/local id. Eles são atualizados antes dos builds e devem acompanhar ambos os
executáveis. O hash é comparado ao iniciar a apresentação de cada fase; clientes
com catálogo incompatível desconectam com diagnóstico. Sons carregados de Resources
usam caminhos, sem carregar todos os áudios de todos os mundos ao entrar na fase.

Transições são decididas pelo servidor. O estado do Boss Rush (encontro, tempo e
eliminações) é replicado; conclusão da sequência retorna ao lobby. Morte de toda a
equipe reinicia a fase Normal ou encerra a tentativa de Boss Rush. Saída de um
participante retorna ao lobby; saída do dono encerra o processo. O marcador
`SB6.Coop.Presentation` permite medir o custo adicional da replicação no build
com profiling. Não aumente a capacidade de produção sem medir fases e chefes
reais com quatro jogadores.


## Verificação cooperativa local

A suíte `OnlineRoomEditModeTests` passou com 30 casos, sem falhas ou skips;
os cinco testes Python do supervisor também passaram. Um servidor Windows
headless e dois clientes reais iniciaram Normal Game em Stage_1-1: o cliente
recebeu dois jogadores e os sprites da fase, com os três controladores locais
de inimigos desativados. Movimento e bomba enviados pelo cliente foram
processados no servidor, incluindo vida zero após a explosão. Desconectar o
participante retornou o dono ao lobby, e a saída do dono encerrou o processo
com código 0. Capturas e XML ficam em Build/Validation (não versionado).

Boss Rush foi repetido após a correção da inicialização: Stage_1-6 recebeu dois
jogadores, loadout NORMAL, cronômetro, Mecha ativo com movimento e bombas
replicados. A pausa enviada pelo dono foi aplicada no cliente (timeScale 0).
Suspender o cliente do dono provocou timeout real de 10 segundos no KCP com o
segundo participante ainda conectado; o processo encerrou com código 0, sem
exceção de alteração da coleção de conexões.

O build Windows passou com zero erros. Nesta execução local, o lobby estável
consumiu cerca de 1,5 KiB/s por cliente; o primeiro encontro do Boss Rush registrou
aproximadamente 21–23 KiB/s de saída total e 60 FPS após o carregamento. Esses
valores são amostras de dois jogadores parados em um desktop, não um benchmark
de capacidade. A campanha inteira, todos os chefes e quatro clientes simultâneos
ainda precisam de uma sessão de QA completa.

RSS com valor -1 indica que o runtime não disponibilizou a medida (Mono no
Windows pode retornar zero); a memória alocada pelo Unity continua disponível.
As medições locais não representam a capacidade nem a latência da VM.

## Histórico: atualização da VM para protocolo 5

O build Linux dedicado com profiling passou com zero erros (321 MB) e foi
instalado junto com o supervisor atualizado. A API pública confirmou protocolo 5;
uma sala Boss Rush publicou disponibilidade/capacidade 4 e encerrou por ociosidade
com código 0. O executável anterior permanece em server-before-coop-v5 na VM.

UDP 7777/7778 continuou sem resposta do Windows tanto pelo IP público quanto
pelo Tailscale. Uma tentativa de túnel temporário para QA foi rejeitada pela
revisão automática da ferramenta; nenhuma partida Linux com clientes foi validada.
O lobby vazio registrou RSS 312–314 MiB, mas também janelas de 36–47 FPS e frames
máximos de 2–3 segundos. Essas pausas precisam ser investigadas em uma medição
controlada antes de afirmar capacidade de partidas. Não foi atribuída uma causa.
