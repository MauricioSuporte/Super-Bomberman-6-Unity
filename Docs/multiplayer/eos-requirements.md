# EOS (Epic Online Services) — requisitos e viabilidade

> **Status: possibilidade FUTURA, não uma decisão.** O multiplayer online do Battle
> Mode hoje usa **KCP** com modelo de **servidor dedicado** (validado pela internet —
> ver [`plano-multiplayer.md`](plano-multiplayer.md), F5d). Este documento registra o
> que seria necessário **se** um dia quisermos oferecer o modelo "qualquer jogador
> hospeda de casa" via relay gratuito, sem Steam. Nada aqui está integrado ao projeto.

## 1. O que o EOS entrega (e a pegadinha arquitetural)

EOS é gratuito, sem royalties, agnóstico de engine/loja. O que interessa pra nós é o
**P2P NAT Relay**: resolve a conexão entre jogadores atrás de NAT sem port-forward,
roteando por um proxy da Epic quando não há conexão direta.

⚠️ **Ponto crítico:** o relay do EOS foi desenhado pro modelo **P2P / host-é-jogador**
(um player hospeda de casa, os outros entram via relay). **Escolhemos servidor
dedicado com IP público**, que **não precisa de relay** (o IP já é acessível). Logo,
o EOS só faz sentido se **mudarmos** para o modelo "qualquer um hospeda de casa". No
modelo dedicado atual ele adiciona login + SDK sem ganho real.

## 2. Requisitos de conta / portal

- **Conta Epic Games** + acesso ao **Developer Portal** (dev.epicgames.com).
- Uma **Organization** sob a conta de dev.
- Um **Product** criado no portal.
- **Verificação de domínio** é opcional — mas, sem domínio verificado, o login por
  conta Epic mostra um **aviso** ao jogador (contornável, ver §4).

## 3. Credenciais geradas pelo portal (entram no jogo)

- **Product ID**, **Sandbox ID**, **Deployment ID**
- **Client ID** + **Client Secret**
- Uma **Client Policy** do tipo **Peer2Peer** amarrada a esse client

## 4. SDK + transport para o Mirror

- **EOS C# SDK** (baixado do portal). O transport de Mirror usa o **SDK C#** direto
  (NÃO o "EOS Plugin for Unity").
- **Transport:** o mais conhecido é o **`FakeByte/EpicOnlineTransport`** (Mirror ↔ EOS:
  Auth/Connect para login, Lobby para matchmaking, P2P para dados). Há forks
  (`WeLoveJesusChrist/EOSTransport`, `CodedImmersions/EOSTransport`).
  - ⚠️ **Risco de compatibilidade** (mesma família do LRM): não fixam versão de
    Mirror/SDK; podem exigir ajuste contra o nosso Mirror embutido (96.x). A favor: o
    **EOS SDK é mantido ativamente pela Epic** (diferente do LRM, arquivado).
- **Autenticação é obrigatória** — todo jogador loga pelo **Connect interface** antes
  de jogar. Opções:
  - **Device ID (anônimo)** ⭐ — sem conta Epic, sem UI de login, sem o aviso de
    domínio. **Ideal para fan game** (fricção zero pro jogador).
  - Conta Epic / Steam / outros identity providers (mais fricção).
- Para testar localmente no modo "Developer", precisa de **múltiplas contas Epic** na
  organização.

## 5. O relay grátis — a real

- **Grátis de fato**, sem taxa de hosting/royalty, **sem mínimo de MAU**, sem tier pago.
- **Não há teto de banda publicado.** A Epic aplica **limites anti-abuso/fair-use**
  (anti-DDoS, sobrecarga) descritos por serviço na doc técnica + na Acceptable Use
  Policy. Na prática, tranquilo para escala de hobby/amigos.

## 6. Legal / ToS (o ângulo que motivou tirar a Steam)

- **Fan game não é proibido explicitamente** no acordo de dev do EOS, nem há termo
  especial para não-comercial. Regras padrão valem igual.
- Mantemos a **propriedade do jogo**; **não podemos usar marcas da Epic** sem permissão.
- **Sem gate de AppID** como a Steam — cadastrar no EOS é bem mais simples (não exige
  listagem de loja).
- Obrigações a observar: **indenização ampla**, **desativação por inatividade** (>1 ano
  sem uso, aviso de 30 dias), **atualizar o SDK em até ~3 anos**.
- 🎯 **Sobre o IP (Bomberman/Konami):** o EOS **não policia o IP do seu jogo** como
  temíamos na Steam, mas **distribuir publicamente** um jogo sobre IP de terceiros é
  risco em **qualquer** plataforma — é problema de *distribuição*, não específico do
  EOS. Para uso **privado/entre amigos**, o risco é baixo.

## 7. Comparação com o que já temos

| | Servidor dedicado (atual) | EOS relay |
|---|---|---|
| Precisa de máquina pública | ✅ sim (já temos) | ❌ não |
| Qualquer um hospeda de casa (sem port-forward) | ❌ não | ✅ sim |
| Login obrigatório do jogador | ❌ não | ✅ sim (Device ID = anônimo, sem UI) |
| Dependência externa | nenhuma | conta/portal/SDK Epic + transport (compat a validar) |
| Esforço de integração | ✅ feito | médio-alto |
| Custo | a VPS | grátis (fair-use) |

## 8. Conclusão / quando reconsiderar

- **Ficando no modelo dedicado** (validado): **não é preciso EOS** — ele resolve NAT
  punchthrough, que o IP público já resolve.
- **Se o objetivo virar "qualquer amigo hospeda do PC de casa"**: o **EOS é a melhor
  opção gratuita sem Steam**, e o **Device ID anônimo** encaixa bem num fan game. O
  trabalho seria: portar/validar o `EpicOnlineTransport` contra o nosso Mirror +
  configurar o Product no portal + trocar o transport ativo (o core não muda — só a
  camada de transporte).

## Fontes

- EOS Overview — https://dev.epicgames.com/docs/epic-online-services/eos-overview
- EOS (site oficial) — https://onlineservices.epicgames.com/
- Developer Agreements — https://onlineservices.epicgames.com/en-US/services/terms/agreements
- Acceptable Use Policy — https://onlineservices.epicgames.com/services/terms/aup
- Get Started / SDK — https://dev.epicgames.com/docs/en-US/epic-online-services/eos-get-started/get-started-guide/set-up-account-and-download-eos-sdk
- FakeByte/EpicOnlineTransport — https://github.com/FakeByte/EpicOnlineTransport
- CodedImmersions/EOSTransport — https://github.com/CodedImmersions/EOSTransport
- Free Relay (EOS Help) — https://eoshelp.epicgames.com/s/topic/0TO2L000000kC1NWAU/free-relay
- Edgegap: EOS relay p/ dedicated? — https://edgegap.com/blog/can-epic-online-services-eos-relays-allow-for-dedicated-server-or-authoritative-server
