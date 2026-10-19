# Online com save do dono e publicação automática

O dono é o primeiro participante da sala. Start Game envia uma cópia do seu
save para a sessão do servidor: slots, progresso, desbloqueios, configurações
de Battle Mode e recordes. Controles, idioma, vídeo e layout de toque não são
transmitidos nem substituídos. O servidor continua responsável pela simulação.

## Fluxos

- Normal Game: lobby → SkinSelect → SaveFileMenu → WorldMap → fase escolhida.
  O dono pode criar um jogo ou continuar um dos seus três slots, com a dificuldade
  e as fases disponíveis nesse save. A quantidade de humanos vem da sala.
- Boss Rush: lobby → SkinSelect → BossRush → chefe. A dificuldade e o loadout
  vêm do menu nativo, respeitando desbloqueios e recordes do dono.
- Battle Mode: lobby → BattleModeMenu → arena. A configuração inicial vem do
  dono; jogadores conectados ocupam slots Man e os COM salvos são preservados.

Somente o dono controla os menus de configuração, save, mapa e dificuldade.
Cada participante controla a própria seleção de personagem, na sua vez, usando
os comandos locais de P1. Na partida, cada máquina continua usando seus próprios
bindings, associados ao seu slot da sala. Não se importam controles do dono.

O progresso recebido do servidor é salvo no computador do dono. Os convidados
recebem uma cópia temporária para apresentar as mesmas telas e regras; ao sair,
seus saves originais são restaurados. A VM não grava o save do dono no disco.
Sair da sala, ou perder a conexão de um participante registrado, encerra a sala
para todos e retorna os clientes à TitleScreen. Não há transferência de dono.

O protocolo atual é 10 e inclui a identidade do build na autenticação.
Servidor, API e clientes devem ser recompilados juntos, mesmo mantendo 0.5.0.

## Runner Windows

Runner registrado: `SB6-Mauricio-Windows`, label `sb6-unity`. Está instalado em
`%USERPROFILE%\.sb6-actions-runner`, fora do checkout de desenvolvimento. A tarefa
do Windows `SB6 GitHub Actions Runner` inicia o listener ao entrar na conta.
O PC precisa estar ligado, com a conta conectada, Unity licenciado e Tailscale
conectado para acessar `mauricio@100.64.0.46`. O runner usa o SSH já configurado
nessa conta; nenhuma chave privada é colocada no repositório ou nos artefatos.

Verificação e controle pelo PowerShell:

```powershell
Get-ScheduledTask -TaskName 'SB6 GitHub Actions Runner'
Start-ScheduledTask -TaskName 'SB6 GitHub Actions Runner'
Stop-ScheduledTask -TaskName 'SB6 GitHub Actions Runner'
```

O workflow `.github/workflows/online-release.yml` é executado em pushes feitos
por `MauricioSuporte` na branch real `v0.5.0`, ou por disparo manual nessa branch (quando o workflow também estiver na branch
padrão, `main`, como exigido pelo GitHub).
Não é executado em pull requests. Ele usa um checkout próprio dentro do runner,
sem abrir, fechar ou modificar o Editor/checkout usados para desenvolvimento.
Compila o SHA que disparou a execução; alterações locais não commitadas não entram.

1. Executa os testes Python; no perfil full também executa a suíte online Unity.
2. Gera Linux Dedicated Server release e cliente Windows.
3. Publica artefatos Windows no GitHub Actions, identificados pelo SHA do commit.
4. Envia o release Linux, hashes e o supervisor correspondente para staging na VM.
5. Ativa a versão validada sem aguardar salas vazias; as partidas atuais são desconectadas.
6. Testa boot/KCP interno, verifica manifesto e ausência dos helpers Development,
   publica e confere health/protocolo. Conserva os três últimos rollbacks criados
   pelo CI; backups manuais com outros nomes ficam preservados.

Não há dois deploys desse workflow em paralelo. Falha em testes/build impede
publicação; falha na ativação restaura binário e supervisor anteriores. Os logs
das salas ficam fora da pasta substituída. Um commit posterior não altera os
artefatos de um commit anterior.

## Artefatos para a equipe

No GitHub, abra Actions → Online release v0.5.0 → execução → Artifacts:

- `SB6-Windows-Client-<data-hora>`: extrair tudo, abrir a pasta Super Bomberman 6 e executar Super Bomberman 6.exe.
- `SB6-Validation-<data-hora>`: relatórios de testes, logs de compilação e publicação,
  manifesto. O pacote Linux é enviado somente à VM. Retenção configurada em 14 dias.

O servidor Windows é um player release iniciado em headless para testes locais;
a produção usa o subtarget Linux Dedicated Server. A publicação automática não
cria tags ou GitHub Releases: "release" aqui é o build sem Development/debug.

Documentação oficial usada na configuração:
[runners próprios](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners)
e [download de artefatos](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts).


## Validação desta integração

- 54 testes Unity passaram, inclusive na execução com save vazio e isolado.
- Cinco testes do supervisor e quatro testes de ativação CI passaram.
- Dois clientes Development completos, sem render, passaram pelos fluxos Normal
  e Boss Rush. Normal continuou o segundo slot Hard do dono através do mapa;
  Boss Rush escolheu HARD no menu nativo e iniciou Stage_1-6 com dois jogadores.
- Nos dois casos, o save foi gravado no arquivo isolado do dono, o convidado não
  recebeu essa progressão no seu arquivo, e a saída do convidado levou ambos
  à TitleScreen desconectados. Nenhum erro ou aviso de servidor foi registrado.
- Artefatos locais: `Build/Validation/OwnerFlowNormalR2`, `OwnerFlowBossR1` e
  `OwnerSaveFlowSandboxTests.xml`. Testes batch usam `SB6_SAVE_DIRECTORY` por
  processo, evitando os arquivos pessoais. A configuração de dificuldade Boss
  Rush também não grava PlayerPrefs quando está nesse sandbox.

Para repetir, gerar Tools → Super Bomberman 6 → Online → Build Performance Test
Player e usar nomes de saída novos:

```powershell
python Tools/RoomDirectory/smoke_owner_flow.py --binary Build/ServerBenchmark/Windows/SB6.exe --mode NormalGame --output Build/Validation/OwnerNormalNovo
python Tools/RoomDirectory/smoke_owner_flow.py --binary Build/ServerBenchmark/Windows/SB6.exe --mode BossRush --output Build/Validation/OwnerBossNovo
```

Para jogar no público, distribua artefatos de uma execução cujo deploy terminou
com sucesso. Artefatos de uma compilação cujo deploy falhou podem ser usados nos
servidores locais correspondentes, mas não demonstram compatibilidade com o
release que permaneceu na VM. A inspeção gráfica, controles humanos e campanha
completa até o final continuam sendo validações manuais.


## Preparação de checkout limpo no Windows

O workflow habilita `git config --global core.longpaths true` na conta do runner
antes do checkout, necessário pelos caminhos longos do pacote Mirror embutido.
A primeira execução revelou também dois metadados inválidos já existentes:
`BirdRobotBlue 1.prefab.meta` continha somente bytes NUL e não tinha GUID
recuperável no AssetDatabase; recebeu metadados válidos e GUID próprio.
`SnowBall.mp3.meta` terminava sem a linha final esperada pelo parser Unity;
o terminador foi corrigido preservando seu GUID e os parâmetros de áudio.
Os arquivos de prefab/áudio não foram removidos ou substituídos.


O runner conserva somente o cache de importação `Library/` entre execuções.
Antes do checkout, remove arquivos não versionados e saídas antigas da sua cópia
isolada; o checkout força os arquivos versionados para o commit solicitado.
Isso evita reimportar todos os assets em cada correção e não conserva código
extra em Assets. O caminho é verificado contra o workspace do runner antes da
limpeza. Os painéis nativos mantêm os rebuilds de layout no servidor e deixam
as atualizações gráficas de Canvas para os clientes, evitando pedir shaders
removidos do Linux Dedicated Server.


## Iteração rápida em v0.5.0

O push usa o perfil quick: compila servidor Linux e cliente Windows, executa
os testes Python e preserva as verificações de integridade e o preflight Linux.
O servidor Windows continua disponível pelo menu do Editor para testes locais,
mas não faz parte da publicação. workflow_dispatch oferece full para também
executar OnlineRoomEditModeTests no Unity. O perfil rápido não executa esses
testes; uma compilação bem-sucedida não comprova comportamento em jogo.
Este workflow continua limitado a v0.5.0; não publica automaticamente main.

Library, Build/LinuxServer e Build/OnlineClient/Windows ficam no checkout
isolado do runner para reaproveitamento incremental. O empacotamento Linux usa
gzip nível 1 e o artefato Windows usa compressão nível 1. Os comandos
registram duração, e o log Unity separa os tempos do servidor e do cliente.

A ativação não espera salas vazias. Depois de validar a nova versão, para o
serviço com KillMode=control-group, desconecta as salas e inicia a nova versão.
Falha anterior à ativação preserva a versão em execução; falha após a troca
aciona a restauração prevista no script.

O CI grava o SHA do commit em Assets/Resources/OnlineBuildIdentity.txt
(ignorado pelo Git) antes de importar o projeto no Unity. Cliente e servidor
recebem o mesmo identificador. A alocação de salas exige protocolo e build;
a autenticação Mirror exige também a versão do jogo. Dois builds 0.5.0 de
commits diferentes não podem jogar juntos. Builds manuais sem esse arquivo
usam local-development e não se conectam a uma release carimbada pelo CI.

IPs, portas e caminhos de instalação estão presentes no repositório; a chave
SSH usada para publicar permanece na conta Windows do runner. SB6_DEPLOY_SSH
permite configurar o destino por ambiente, mantendo o destino existente como
padrão. Isso não remove informações já existentes no histórico do Git.


## Lobby, distribuição e apresentação online

Os nomes dos artefatos usam yyyy-MM-dd_HH-mm-ss no fuso America/Cuiaba.
O SHA continua no build-info.json e na autenticação, para garantir compatibilidade.
O ZIP Windows contém uma pasta Super Bomberman 6 com os arquivos do jogo,
incluindo Super Bomberman 6.exe. Executáveis antigos do cache são excluídos da cópia.

No lobby, cima/baixo selecionam uma linha, esquerda/direita trocam o modo ou a
visibilidade. Salas privadas usam seis dígitos: esquerda/direita selecionam o
dígito, cima/baixo alteram o valor, A confirma e B cancela. O servidor valida a
senha mesmo em conexões diretas; a listagem expõe somente requiresPassword,
sem senha ou hash. O processo recebe um verificador com salt via ambiente.
Tentativas incorretas são limitadas por endereço. Sair da sala volta à listagem;
voltar da listagem abre TitleScreen.

A música intro/loop é temporizada no dispositivo de áudio do cliente. O servidor
headless não usa AudioSource.isPlaying para antecipar a troca do loop. Reenvios
do mesmo estado musical não reiniciam o áudio.

A animação do jogador replica offsets verticais e é apresentada na mesma linha
do tempo da posição interpolada. A proteção silenciosa de montar/desmontar é
replicada separadamente da proteção com blink. Esses ajustes ainda exigem
validação visual entre peers; compilação não comprova fluidez ou timing.

Para diagnosticar movimento, abrir Start-Online-Diagnostics.cmd na pasta do jogo.
Após jogar, enviar OnlineMotion.log, criado nessa mesma pasta. O parâmetro
-online-motion-logs registra RTT, buffers, posição e animação uma vez por segundo,
sem registrar senhas.
