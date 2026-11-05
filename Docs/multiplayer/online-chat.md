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
- O histórico aparece automaticamente ao abrir a digitação com **T**. Z mantém sua função de gameplay.
- **Page Up / Page Down:** percorre o histórico enquanto digita.

Mensagens novas aparecem por cinco segundos, seguidos por um fade de 0,4 s.
O histórico local guarda até 64 mensagens por canal. O chat da sala é apagado
ao desconectar; entrar em outra sala não mostra mensagens da anterior.
As mensagens gerais recebidas ao iniciar a sessão entram como histórico.
O painel usa o safe frame do `UICameraViewportFitter`/`PixelPerfectViewport`,
no canto inferior esquerdo, com escala inteira para a referência 256×224.

Enquanto digita, o chat captura os controles locais de menus e gameplay,
incluindo Enter/Esc até soltar as teclas após o fechamento. A partida continua para os outros
jogadores. Z não é reservada pelo chat.
Os formulários de nome/senha/criação mantêm a própria entrada de teclado.
Esses atalhos requerem teclado; esta alteração não adiciona teclado virtual ou
atalhos de chat ao controle e aos botões de toque.

## Transporte e limites

`OnlineChatSession` estende o `BombermanNetworkManager`. O cliente envia o texto e uma nonce de confirmação; o servidor obtém o nome pelo autenticador, valida o slot e distribui
a mensagem a todas as conexões autenticadas, independentemente de `isReady`.
Nenhum prefab de jogador precisa conter o componente do chat.

`OnlineChatOverlay` é adicionado pelo menu ao NetworkManager persistente e cria
a interface em runtime. Não há novo singleton persistente nem alteração de cena.
O histórico pertence à sessão, não ao save. Texto é limitado a 128 unidades
UTF-16, com controles invisíveis removidos. Marcadores enviados pelos usuários são escapados antes das cores controladas pelo apresentador TMP.
O servidor limita os envios a uma mensagem a cada 0,25 s por crédito, com tolerância a quatro mensagens em sequência.

O supervisor oferece `POST /chat/session`, `GET /chat?after=<id>` e `POST /chat`.
A sessão vincula um token aleatório ao IP e ao nome informado na entrada.
O nome geral é um apelido, sem autenticação de conta. Tokens expiram após
90 segundos sem consultas; o cliente renova a sessão automaticamente.
Há até 64 mensagens em memória, com retenção de cinco minutos, limite de oito
sessões por IP e 4096 no total. Criações de sessão têm intervalo mínimo de
0,75 s por IP. Mensagens compartilham um orçamento por IP de quatro créditos, recuperando um a cada 0,25 s. O polling ocorre a cada 0,2 s.
Mensagens e tokens não são gravados em logs nem em disco.

## Publicação e validação

O protocolo atual é **63**. Recompile cliente e servidor dedicado e publique o
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

## Revisão visual e de entrada

Texto em negrito com contorno escuro, nomes nas cores P1 branco, P2 preto, P3 vermelho, P4 azul, P5 verde e P6 amarelo, e mensagens em rosa
claro. Faixas pretas com transparência acompanham cada linha visível, inclusive
o campo de entrada verde claro. Não há dica de T na interface. Z não mostra
o histórico; o histórico aparece junto com o campo ao pressionar T.

Digitação é aplicada imediatamente ao TMP no evento de texto, inclusive várias
letras no frame de abertura. Inputs locais permanecem bloqueados até liberar as
teclas após Enter/Esc; COM e outros participantes continuam jogando. O HUD de
FPS e seus atalhos de captura também respeitam o bloqueio.

Enter mostra o envio imediatamente no histórico local e o coloca em uma fila
limitada. Uma nonce por mensagem permite confirmar o eco do servidor sem duplicar
o histórico. O texto novo não espera pela resposta HTTP, e a resposta de um envio
anterior não fecha um novo campo aberto. Falhas de conexão aparecem separadas do
texto digitado; limite temporário HTTP 429 é reprocessado sem o aviso de ocupado.
Mensagens de usuários são escapadas antes dos marcadores de cor do TMP.

A cor da sala é determinada pelo slot validado no servidor, não pelo cliente.
No chat geral, o diretório atribui um índice de cor único por sessão e o preserva
no histórico. Nomes pretos e azuis recebem um contorno claro adicional, sem criar
fundo no painel. A prévia Unity dos seis slots fica em `build/Validation/ChatPolish/preview.png`.

Validação da revisão: 8/8 testes Unity, 15/15 testes Python do diretório e 12/12
testes do pipeline de release aprovados. Compilação C# sem erros/avisos nesta
execução. A prévia renderizada confirmou as seis cores e o campo sem fundo.

## Presença e efeitos sonoros

O servidor anuncia entradas e saídas com o nome e a cor do slot. A própria
entrada e saída também aparecem, com texto localizado nos quatro idiomas.
Entradas usam verde e saídas laranja; trocar de cena na mesma sala não produz
novos avisos. O aviso da própria saída permanece brevemente durante a transição.

Os arquivos `Sounds/OnlineChat/player_join` e `player_leave` reproduzem os SFX
fornecidos pelo usuário. A reprodução é local em cada cliente, respeita o volume
de efeitos e funciona durante a pausa, sem retransmitir o áudio pela rede.

Validação de presença/SFX: 8/8 testes Unity, 15/15 testes do diretório e 12/12
testes do pipeline aprovados. A compilação C# final passou sem avisos nem erros.
A prévia renderizada está em `build/Validation/ChatPresence/preview.png`.
A suíte verificou avisos próprios, importação dos clips, faixas transparentes,
ausência da dica e preservação do aviso de saída. A audição dos SFX e a sessão
com dois executáveis remotos permanecem como validação manual.
