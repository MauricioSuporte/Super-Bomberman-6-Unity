# Prova de conexão por Edgegap Relay

Esta é a primeira etapa de catálogo + relay: duas máquinas em redes diferentes,
sem encaminhar portas no roteador. A publicação automática no catálogo ainda
não usa relay. A prova é opt-in, por argumento, e não altera o fluxo LAN normal.

## Conta e custo

1. Cadastre-se em https://app.edgegap.com/auth/register.
2. No painel, abra Relay e crie um Relay Profile para o jogo.
3. Gere o token específico desse perfil. Um token de hosting comum não serve.
4. Confira o plano e os limites em https://edgegap.com/resources/pricing.
   Use o teste gratuito disponível na conta antes de contratar infraestrutura.
5. Configure o token apenas no ambiente local da ferramenta administrativa:
   `EDGEGAP_RELAY_API_TOKEN`. Não coloque a chave em Assets, no Git ou nos builds.

Documentação do provedor:
https://docs.edgegap.com/learn/distributed-relay/relay-edgegap-api

## Criar a sessão de teste

Use o IP público de cada participante (duas redes diferentes), não o endereço
192.168.x.x do computador. A prova autoriza antecipadamente esses dois IPs.
Mudanças de IP exigem uma nova sessão. Nenhuma chamada real é feita nos testes.

Obtenha o IPv4 observado por UDP em cada computador, na rede que usará para jogar:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/RoomDirectory/Get-UdpPublicIp.ps1
```

O script usa STUN, não precisa de Python nem do token Edgegap. No notebook,
copie somente esse script e execute com o caminho correspondente. Consultas
de IP no navegador podem mostrar um endereço diferente do tráfego UDP.
Na investigação de 2026-10-12, a sessão autorizava 193.186.4.202 para o host,
mas duas consultas UDP independentes observaram 179.252.130.209. A sessão
precisa ser recriada com os endereços UDP dos dois participantes. A origem
da diferença entre os endereços não foi determinada.

Na raiz do repositório, com a variável de ambiente configurada:

```powershell
python Tools/RoomDirectory/relay_probe.py create --host-ip IP_PUBLICO_HOST --client-ip IP_PUBLICO_CONVIDADO --output output/RelayProbe/teste1
```

A ferramenta cria `host.json`, `client.json` e `session.json` em uma pasta
ignorada pelo Git. Os dois primeiros contêm credenciais temporárias distintas.
Envie somente `client.json` ao convidado por um canal privado; mantenha
`host.json`, `session.json` e a chave administrativa com você. No Windows,
guarde os arquivos em uma pasta acessível somente à sua conta.

O `sessionId` numérico do transporte Mirror recebe `authorization_token` da
sessão. O `session_id` textual da API é usado somente para consultar/remover
a sessão. Não são o mesmo identificador.

## Executar os jogadores

Gere dois clientes atuais pelo menu Build / Online Client. Execute cada cliente
com o caminho absoluto do seu arquivo privado:

```powershell
& 'CAMINHO/DO/JOGO.exe' -relay-config 'CAMINHO/ABSOLUTO/host.json'
& 'CAMINHO/DO/JOGO.exe' -relay-config 'CAMINHO/ABSOLUTO/client.json'
```

Cada comando deve rodar na máquina correspondente. Entre em Battle Mode /
Online no título. O host inicia a sala e o convidado conecta automaticamente.
Nenhum IP é digitado no lobby e a descoberta/listagem pública fica desativada
nesse modo de teste, evitando anunciar um endereço direto inacessível.

O servidor verifica igualdade de `Application.version` e `ProtocolVersion`
antes de admitir cada conexão; há timeout de dez segundos para autenticação.
Atualize a versão do produto a cada release e incremente `ProtocolVersion`
quando mensagens, prefabs de rede ou conteúdo de gameplay ficarem incompatíveis.
Não há ainda fingerprint automático do conteúdo ou garantia de cliente legítimo.
Clientes antigos, sem handshake, também não são admitidos: reconstrua ambos.

Confira slots, personagens diferentes, configuração, dois rounds, pause,
retorno ao Stage Select e saída do host. Depois teste uma versão diferente.
O host continua executando a partida; sair encerra a sessão de jogo.

## Remover a sessão

Fechar o jogo nesta prova NÃO remove a alocação externa automaticamente.
Ao terminar, execute na máquina administrativa:

```powershell
python Tools/RoomDirectory/relay_probe.py delete --session-file output/RelayProbe/teste1/session.json
```

Apague as credenciais locais e a cópia do convidado depois. Se a ferramenta
falhar, remova a sessão pelo painel Edgegap para não manter recursos alocados.

## Próxima etapa: catálogo + relay automático

Após confirmar a conexão real, ampliar o backend existente para criar sessões,
autorizar convidados, emitir credenciais individuais, renovar a sessão durante
a partida inteira e remover alocações com tentativas de recuperação. A remoção
de um anúncio ao iniciar a partida não pode destruir o relay da partida.

Hospedar esse backend com HTTPS; trocar o anúncio `kcp://IP:porta` por uma
referência de sala; obter as credenciais via backend ao criar/entrar; filtrar
compatibilidade no catálogo; aplicar limites de criação e entrada antes das
chamadas pagas. Manter as chaves exclusivamente no backend. O teste entre
redes e a hospedagem pública dependem da conta e infraestrutura do usuário.

## Validação local desta etapa

Em 2026-10-12, oito testes Python passaram (quatro do catálogo e quatro da
ferramenta de relay, com API simulada). A compilação C# runtime/Editor passou
com os dois scripts novos incluídos por um suplemento temporário de MSBuild;
os seis avisos emitidos são de arquivos existentes do pacote Mirror.
Os cinco novos casos Edit Mode foram compilados, mas não executados.
Unity MCP não tinha sessão conectada e a tentativa batch de compilação
encerrou antes de abrir o projeto, já aberto em outro Editor. A compilação
Unity/Weaver, o teste entre dois jogadores e a conexão real Edgegap ainda
precisam ser validados. Não foi contratado nem ativado serviço externo.

## Correção do handshake do transporte

O Mirror 96.6.4 incluído no projeto tinha `EdgegapKcpClient.TickOutgoing`
enviando os pings de autenticação apenas quando o KCP já estava conectado.
O projeto agora envia esses pings assim que existe um socket, incluindo a
fase de conexão. Preserve essa correção ao atualizar o pacote Mirror.
O protocolo do provedor exige pings desde a abertura da conexão:
https://github.com/edgegap/distributed-relay-examples#game-client-communication-with-relay
Isso corrige uma dependência circular identificada no código; o teste real
entre host e convidado ainda precisa confirmar o resultado. O estado numérico
6 observado nos logs não está definido no enum do pacote e não foi interpretado.
