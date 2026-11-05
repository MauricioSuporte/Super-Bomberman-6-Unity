# Chat online

O chat tem dois canais separados:

- **Geral:** participantes na lista de salas, antes de criar ou entrar em uma.
  Usa o mesmo diretório HTTP configurado em `OnlineRoomDirectory`.
- **Sala:** somente as conexões autenticadas daquele servidor Mirror. Continua
  durante a configuração, Normal Game, Boss Rush, Battle Mode e trocas de cena.

Controles de teclado:

- **T:** abre a digitação.
- **Enter:** envia e fecha. Com texto vazio, apenas fecha.
- **Esc:** cancela a digitação.
- **Segurar Z:** mostra o histórico enquanto a tecla estiver pressionada.
- **Page Up / Page Down:** percorre o histórico enquanto digita ou segura Z.

Mensagens novas aparecem por cinco segundos, seguidos por um fade de 0,4 s.
O histórico local guarda até 64 mensagens por canal. O chat da sala é apagado
ao desconectar; entrar em outra sala não mostra mensagens da anterior.
As mensagens gerais recebidas ao iniciar a sessão entram como histórico.
O painel usa o safe frame do `UICameraViewportFitter`/`PixelPerfectViewport`,
no canto inferior esquerdo, com escala inteira para a referência 256×224.

Enquanto digita, o chat captura os controles locais de menus e gameplay,
incluindo Enter/Esc no frame de fechamento. A partida continua para os outros
jogadores. Z fica reservada ao histórico no online; offline mantém seus controles.
Os formulários de nome/senha/criação mantêm a própria entrada de teclado.
Esses atalhos requerem teclado; esta alteração não adiciona teclado virtual ou
atalhos de chat ao controle e aos botões de toque.

## Transporte e limites

`OnlineChatSession` estende o `BombermanNetworkManager`. O cliente envia somente
o texto; o servidor obtém o nome pelo autenticador, valida o slot e distribui
a mensagem a todas as conexões autenticadas, independentemente de `isReady`.
Nenhum prefab de jogador precisa conter o componente do chat.

`OnlineChatOverlay` é adicionado pelo menu ao NetworkManager persistente e cria
a interface em runtime. Não há novo singleton persistente nem alteração de cena.
O histórico pertence à sessão, não ao save. Texto é limitado a 128 unidades
UTF-16, com controles invisíveis removidos e rich text desativado no TMP.
O servidor limita os envios a uma mensagem a cada 0,75 s por conexão.

O supervisor oferece `POST /chat/session`, `GET /chat?after=<id>` e `POST /chat`.
A sessão vincula um token aleatório ao IP e ao nome informado na entrada.
O nome geral é um apelido, sem autenticação de conta. Tokens expiram após
90 segundos sem consultas; o cliente renova a sessão automaticamente.
Há até 64 mensagens em memória, com retenção de cinco minutos, limite de oito
sessões por IP e 4096 no total. Criações e mensagens têm intervalo mínimo de
0,75 s por IP, compartilhado entre sessões. O polling ocorre a cada segundo.
Mensagens e tokens não são gravados em logs nem em disco.

## Publicação e validação

O protocolo passa a **61**. Recompile cliente e servidor dedicado e publique o
supervisor atualizado junto deles. O servidor público anterior não oferece
esses endpoints/handlers. Proxies que filtram rotas precisam permitir `/chat`
e `/chat/session`, além de encaminhar `X-Chat-Token`.

Testes do backend:

```powershell
python -m unittest discover -s Tools/RoomDirectory -p "test_*.py" -v
```

No Test Runner do Unity, execute `OnlineChatEditModeTests`. Além da normalização
e da liberação do bloqueio de input, a suíte exercita transporte Mirror local,
nome autenticado, limite de frequência, teclado, cancelamento sem pausa e
histórico após troca de cena. Em execução interativa com o Game View renderizando,
solicita uma captura em `build/Validation/online-chat-lobby.png`.

Validação em 05/11/2026: os sete testes Unity passaram, incluindo o transporte
Mirror em host local, os atalhos, os inputs sintéticos do host e os limites do
painel dentro do safe frame. Os 13 testes Python do diretório passaram. A execução
batch não produziu a captura; a revisão visual e a sessão com dois executáveis
recompilados ainda precisam de validação.

Para QA com dois executáveis recompilados, verifique conversa geral, entrada
na mesma sala, isolamento de uma segunda sala e mensagens nos três modos.
Teste texto longo e japonês, pausa, morte, carregamento e desconexão com o
chat aberto. Inspecione o safe frame em 4:3, 16:9 e janela menor.

Referência de interação: [chat do SB5 Recomp](https://github.com/MauricioSuporte/SuperBomberman5SNESRecomp/blob/main/docs/NETPLAY.md).

## Publicação para teste em 05/11/2026

Servidor Linux e supervisor publicados com protocolo 61, a partir do checkout
`75abd74e1f6e836260fd66b2b4467aaf56463d74` com as alterações locais desta
implementação. O build dedicado passou sem erros; boot/KCP, identidade da API
pública, criação de sessão do chat, polling e rejeição de mensagem vazia foram
verificados. A verificação não enviou mensagens ao chat público.

A publicação preservou rollback e atualizou somente o servidor. Teste pelo
Unity Editor deste checkout, entrando em Play Mode a partir de TitleScreen.
O executável Windows e o canal do atualizador não foram publicados nesta etapa.
Os relatórios locais estão em `build/Validation/ChatDeploy/`.
