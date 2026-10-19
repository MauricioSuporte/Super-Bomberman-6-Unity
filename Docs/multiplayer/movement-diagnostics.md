# Comparar movimentação e Bomb Kick offline/online

A captura é opcional e usa os mesmos eventos no offline, cliente, host e servidor
dedicado. Não muda física, predição, frequência de envio ou protocolo e não envia
pacotes extras. Teclado, controle e touch passam pelo PlayerInputManager existente.

## Ativar e coletar

- No Editor: `Tools > Super Bomberman 6 > Diagnostics > Movement and Kick > Start
  Capture (This Device)`. Pode ativar antes ou durante Play. Use `Stop Capture
  (This Device)` para terminar e descarregar o buffer em disco.
- Em executáveis Windows/Linux, acrescente `-gameplay-latency-logs` na linha de
  comando. O antigo `-online-motion-logs` também ativa a captura, mantendo os
  logs antigos de animação no Player.log.
- Para APK e builds de teste sem linha de comando, marque `Capture in Builds`
  nesse menu antes de gerar o build. Esse marcador é o arquivo
  `Assets/Resources/GameplayLatencyDiagnostics.json`. Desmarque ao finalizar os
  testes; ele está desligado por padrão. Gere o servidor com a captura habilitada
  ou passe o argumento nele para obter também suas decisões de chute.

Os arquivos ficam em `Application.persistentDataPath/Diagnostics/Movement/`,
com nomes `motion-<UTC>-<identificador>.jsonl`. O Console/Player.log informa o
caminho ao iniciar cada arquivo. No Editor, `Open Log Folder` abre a pasta.
Cada execução possui seu identificador; mudanças de modo/cena aparecem em cada
linha. O limite é aproximadamente 8 MiB ou 10 minutos por captura. Os arquivos
anteriores são preservados. Ative novamente para começar outro arquivo após o limite.

No Android, o diretório normalmente fica em
`/sdcard/Android/data/com.mauriciosuporte.superbomberman6/files/Diagnostics/Movement/`.
Com depuração USB autorizada, use `adb pull` nesse diretório. No servidor, colete
os arquivos do persistentDataPath da conta que executa o processo Unity.

## Roteiro de comparação

1. Use o mesmo estágio, velocidade, quantidade/tipo de bombas e Bomb Kick nos
   dois modos; compare o mesmo personagem sem montaria e sem skull inicialmente.
2. Offline: parado, avance, solte, inverta a direção, faça curvas e chute uma
   bomba parada. Repita dez vezes, incluindo um chute contra parede e o ActionR
   para interromper a bomba.
3. Online: repita como dono do personagem e depois com outro participante
   observando, mantendo captura no cliente e no servidor. Repita em LAN e na VM
   para separar custos do jogo e da conexão.
4. Pare a captura e guarde os arquivos identificando modo, aparelho, conexão e
   sequência executada. Não é necessário compartilhar saves ou credenciais.

## O que cada evento mede

| Evento | Interpretação |
| --- | --- |
| `input_edge` | Mudança do input aceito pelo PlayerInputManager local; não é timestamp de hardware. |
| `input_send` / `input_receive` | Comando enviado/recebido; mudanças, ações pressionadas e amostra de manutenção a cada segundo. |
| `input_apply` | Mudança aplicada ao input sintético; `durationMs` é a idade local desde o último recebimento no servidor. |
| `simulation_tick` | Amostra da execução física no offline/servidor; inclui bloqueios e idade do último comando recebido. |
| `input_to_motion` | Tempo local entre mudança de direção e primeiro deslocamento observado nessa direção. No cliente inclui espera de rede e apresentação/interpolação. |
| `input_no_motion_500ms` | Ainda não houve esse deslocamento após 500 ms; parede, stun, intro ou input bloqueado podem ser legítimos. |
| `motion_sample` | Posição, delta do frame, buffers, estado e contadores de comandos, a 10 Hz; `durationMs` é a idade do último recebimento, se houver. |
| `bomb_near_input` | Input dirigido a uma bomba a menos de 1,6 unidade; é aproximação, não detecção autoritativa de chute. |
| `bomb_first_observed_motion` | Primeiro deslocamento da bomba após essa aproximação; pode ser chute, soco ou outro efeito. Correlacione com a decisão do servidor. |
| `bomb_sample` | Posição e delta de bombas próximas em movimento/contato, a 10 Hz. Flags de chute têm autoridade apenas no offline/servidor. |
| `kick_detail` | Contexto e restrições do BombKickAbility: distância, alinhamento, solidificação e liberação do chute antecipado. |
| `kick_result` | Resultado da tentativa do jogador, incluindo o id do jogador que chutou e da bomba. |
| `kick_accepted` / `kick_rejected` | Decisão da física da bomba: indisponível, tile bloqueado, personagem na saída ou sobreposição de destino. |
| `kick_first_physics_motion` | Tempo do StartKick aceito até o primeiro deslocamento físico observado na corrotina. |
| `kick_stopped` / `kick_cancelled` | Fim do trajeto, parada com snap ou cancelamento externo. |

Campos comuns incluem `role`, `scene`, `player`, `netId`, `bombNetId`, ids locais
`entity`/`bombEntity`, frame, UTC, tempo local, timeline Mirror, RTT, desvio de RTT,
buffer de interpolação, intervalo configurado de envio, tempo do frame e passo
fixo. O cabeçalho registra versão, identidade de build, protocolo e plataforma.
Bits de `held`: cima=1, baixo=2, esquerda=4, direita=8, A=16, B=32, C=64,
Select=128 e R=256. `-1` indica que o probe ainda não observou input desse ator.

Compare `input_to_motion` local em condições equivalentes e observe picos de
`frameMs`, RTT, variação de RTT, `bufferMs` e buffers vazios. Cruze cliente e
servidor pelo `bombNetId`, jogador, cena e sequência das ações. No offline use
`bombEntity` para distinguir bombas. Não subtraia UTC de máquinas diferentes nem
trate `timeline` como relógio comum exato: a timeline do cliente é interpolada.
Os contadores de comandos não são contadores de perda de pacotes.

Para evitar que o diagnóstico vire o gargalo, detalhes de chute são limitados a
quatro por segundo por jogador/bomba, há limite global de 180 eventos/segundo e
as escritas são bufferizadas, com flush periódico. `dropped` informa linhas
descartadas pelo orçamento. Não há log por frame no Console. Ainda há custo de
captura, serialização e disco; compare com captura ativa nos dois modos e faça
uma rodada com ela desativada para verificar sua influência.
