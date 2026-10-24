# Predição de movimento e reconciliação

O online usa servidor autoritativo com predição do movimento do jogador local.
O cliente envia controles, nunca posição, velocidade, dano ou um delta de tempo.
A mesma lógica de curvas, alinhamento e passos de pixels do `MovementController`
é executada em ticks de física no cliente e no servidor.

## Fluxo

1. O cliente amostra teclado, controle ou toque pelo PlayerInputManager local P1,
   independentemente do slot P1–P6 atribuído na sala. Bordas das ações ficam
   acumuladas até o próximo tick; direções usam o estado mantido mais recente.
2. Cada tick recebe uma sequência crescente e entra no histórico local. Após o
   primeiro snapshot, andar e mudar de direção respondem sem esperar a rede.
3. O servidor aceita comandos somente do dono do objeto Mirror, normaliza as
   máscaras e consome no máximo um comando por tick de física. Não aceita passos
   extras de movimento pelo simples envio de mais comandos. COM e host local
   mantêm sua simulação anterior.
4. Depois da física, o servidor envia ao dono a posição, o último input processado,
   a velocidade autorizada e o estado das curvas/acumuladores/alinhamento.
5. O cliente restaura esse estado, remove inputs confirmados e reexecuta somente
   os restantes. Replay não simula a cena física inteira, não aplica dano e não
   executa chutes ou outras habilidades que alterem objetos do mundo.
6. O NetworkTransform continua ServerToClient para observadores. No dono, seu
   Update fica desativado para não sobrescrever a posição reconciliada. A animação
   de caminhada é local; observadores continuam usando os frames do servidor.

O histórico contém até 128 ticks. A fila do servidor contém até oito: após uma
interrupção longa, descarta os comandos mais antigos para não acumular segundos
extras de atraso. Inputs descartados não são simulados; a confirmação do próximo
comando traz o resultado real. Ações nesses comandos também podem ser descartadas.
Se faltar uma parte do histórico, o cliente suspende a predição até conseguir
reconciliar um histórico completo. Transporte e comandos continuam confiáveis,
portanto perdas podem atrasar sua entrega. Não há garantia de latência zero.
Snapshots são enviados a até 30 Hz e também durante a pausa (tempo não escalado).
Cada snapshot confirma o resultado após a física, incluindo múltiplos ticks que
possam ter ocorrido no mesmo frame. Pausa/início/resultado bloqueiam a predição;
comandos pendentes no servidor são descartados durante esses bloqueios.

## Escopo e limites

Esta implementação faz rollback **do movimento do dono**, não do mundo inteiro.
Bombas, explosões, itens, dano, RNG, inimigos, vitória e alterações de tiles
continuam autoritativos. Colocar/chutar/arremessar bombas ainda espera a rede.
A reconciliação consulta o mundo atualmente conhecido pelo cliente; não guarda
histórico de tiles, bombas ou outros jogadores. Divergências podem gerar correções
visíveis de posição, especialmente perto de obstáculos dinâmicos.

Montarias, saltos de montar, lançadores, movimentos externos, stun e poses especiais
suspendem a predição e usam posição/animação autoritativas. Não reproduzir saltos
ou poses de luva paralelamente evita efeitos duplicados durante o replay.

Para predição de todas as ações, o próximo trabalho é separar dados de simulação
por tick dos MonoBehaviours e corrotinas: registrar bombas/fusíveis/ocupação de
células, IDs de ação, RNG e eventos de spawn/despawn; prever as ações, confirmar ou
rejeitar cada ID e reproduzir efeitos audiovisuais uma única vez. Só então seria
correto anunciar rollback completo. Reexecutar Physics2D global e corrotinas sobre
a cena atual não fornece esse contrato.

## Compatibilidade e validação

O protocolo passa de 17 para 18. Cliente e servidor precisam ser recompilados
juntos. A implementação usa os componentes existentes do Player.prefab; nenhuma
nova referência de cena ou prefab é necessária.

Testes EditMode em OnlineRoomEditModeTests cobrem sequência/rollover, histórico
limitado e restauração/replay do motor com curvas e frações de pixel, além do gate
Offline/Host/Client existente. Esses testes não substituem uma sessão com dois
peers. Validar com os mesmos builds: Offline P1/P2, host, dono e observador;
movimento/curvas, obstáculos, soltar direções, bombas e fuga, montaria/desmontaria,
luva, stun, morte, pausa, resultado e duas rodadas. Repetir com RTT de 50/100/200 ms,
jitter e perda de pacotes, conferindo que ações não se duplicam e correções não
atravessam paredes no estado autoritativo.

### Verificação desta implementação

C# runtime/Editor compilados pelo dotnet; scripts e Weaver do Mirror compilados
no Unity 6000.6.0f1. As 24 verificações selecionadas passaram por invocação direta
dos métodos NUnit no Editor, incluindo replay com curva, subpixel e bloqueio por
parede. Registro local: `Logs/OnlinePredictionDirectChecks.txt`. Essa execução não
incluiu setup/teardown do Test Runner. O Test Runner foi cancelado ao solicitar
salvamento da TitleScreen, que já estava modificada; a cena aberta foi preservada.
Não foram executadas partidas entre dois peers, testes com latência/perda, nem
validação visual das montarias/arenas. Essas validações continuam pendentes.
