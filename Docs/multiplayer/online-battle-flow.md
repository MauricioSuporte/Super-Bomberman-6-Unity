# Battle Mode: entrada online pelo título

O título oferece `Battle Mode → Offline / Online`. Offline continua abrindo
o `BattleModeMenu` local. Online abre a cena compartilhada `OnlineLobby`.
Os builds `Build/Online Client (Windows/Linux)` agora começam no título.
O build de servidor dedicado continua usando seu bootstrap headless.

## Sala e configuração

- Cada conexão autenticada recebe o primeiro slot livre entre P1 e P6.
  A atribuição pertence ao servidor. Uma sala cheia rejeita novas conexões.
- Os slots não são renumerados quando um participante sai. Só é permitido
  entrar enquanto a sala está no lobby.
- Com pelo menos dois participantes, o host escolhe `Configurar partida`.
  O Mirror leva todos à cena `BattleModeMenu`.
- No Player Select, conexões ocupam slots Man fixos. O host pode alternar
  os slots vazios entre COM e Off.
- Character Select mantém a seleção sequencial existente: cada participante
  usa os controles de P1 de sua própria máquina quando chega a sua vez.
  O host escolhe também os COM. O servidor valida as seleções com o catálogo
  existente; os demais clientes veem os cursores e confirmações replicados.
- O host controla os demais passos. Os clientes acompanham os mesmos painéis, opções e cursores do host,
  incluindo as confirmações de restaurar padrões. Os controles do lobby
  desaparecem ao sair da cena. Apenas o participante da vez controla
  a seleção de personagem; as demais configurações pertencem ao host.
- Ao iniciar, o servidor envia o snapshot final e usa `ServerChangeScene`.
  COM são instanciados e simulados pelo servidor; jogadores humanos conservam
  o modelo de movimento do Mirror já usado no projeto.

## Persistência e saída

`SaveSystem` mantém uma cópia temporária das configurações da batalha durante
a sessão. Getters e setters existentes usam essa cópia no online. O snapshot
transmite regras, times, loadouts, personagens e skins; não transmite controles,
idioma, desbloqueios ou slots da campanha. As configurações offline não são
sobrescritas. A fonte do estado da sala é o NetworkManager; propriedades
estáticas de leitura são caches para os menus.

O fim de round reproduz a comemoração e os sons antes do placar. Ao atingir
a meta de vitórias, todos assistem ao Win Match e retornam diretamente ao
Stage Select, mantendo as configurações da sala. A troca de cena é decidida
pelo host após a apresentação dos clientes (com limite de espera de 5 segundos). Uma desconexão durante a configuração ou
a partida retorna os participantes restantes ao lobby para refazer a configuração.
Se o host encerra a sala, os clientes voltam ao título. Na arena, apenas o host
pode pausar e navegar pelo menu. O pause, cursor e confirmação são replicados
a todos; continuar, retornar à configuração e sair seguem a decisão do host.
Personagens e skins são aplicados por slot antes do spawn e replicados no
estado do jogador, sem depender dos desbloqueios locais de quem os observa.

`OnlineRoomMode` identifica Battle Mode, Normal Game e Boss Rush, mas apenas
Battle Mode está disponível neste fluxo. Descoberta pública de salas, relay,
reconexão e migração de host continuam fora desta entrega.

## Teste manual

1. Abra `Assets/Scenes/TitleScreen.unity`.
2. Gere um cliente em `Build → Online Client (Windows)` e execute-o.
3. No executável: Battle Mode → Online → Criar sala.
4. No Editor: Play → Battle Mode → Online → IP `127.0.0.1` → Entrar.
5. Confira P1/P2 e aguarde o host escolher Configurar partida.
6. Confira os slots Man fixos; configure um slot vazio como COM.
7. Escolha personagens diferentes usando P1 em cada janela, na vez de cada slot.
8. Configure arena, regras, itens e handicap no host; confira os mesmos painéis e cursores no cliente; tente alterar as regras pelo cliente.
9. Inicie a partida: compare som, cursor confirmado, texto piscando e fade em ambas as janelas.
10. Confira personagens, loadouts, COM, comemoração de vitória, placar e próximos rounds. Configure uma vitória para testar Win Match completo e retorno ao Stage Select com as mesmas regras, skins e arena.
11. Pause no host: confira congelamento e cursor em ambos, confirmação/cancelamento e retomada. Tente pausar pelo cliente (deve ser ignorado).
12. Teste retorno à configuração pelo pause, desconexão de cliente e encerramento pelo host.
13. Jogue pelo menos dois rounds seguidos e elimine um jogador logo no início do segundo: a vitória deve ser avaliada após a proteção de spawn, sem precisar de outra morte.
14. Saia da sala e confira Battle Mode → Offline e suas configurações locais.

Os testes `OnlineRoomEditModeTests` cobrem limite/reutilização de slots,
isolamento das configurações offline, conteúdo do snapshot e entrada/saída
do host no lobby, aplicação de skins por slot e reprodução dos painéis e uma morte durante a proteção de spawn em dois rounds consecutivos. Eles não substituem o teste entre duas máquinas.

Validação em 2026-10-12, no repositório original após o merge em `v0.5.0`: Unity compilou e os nove `OnlineRoomEditModeTests` passaram, sem falhas ou testes ignorados. Isto não substitui a validação manual entre host e cliente em todas as arenas.
