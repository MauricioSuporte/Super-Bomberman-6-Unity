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

O protocolo passa a 7 porque as mensagens de configuração e de estado mudaram.
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

1. Executa a suíte online Unity e os testes Python do supervisor.
2. Gera Linux Dedicated Server release, cliente Windows e servidor Windows de teste.
3. Publica artefatos Windows no GitHub Actions, identificados pelo SHA do commit.
4. Envia o release Linux, hashes e o supervisor correspondente para staging na VM.
5. Aguarda até 30 minutos pelas salas vazias. Se não esvaziarem, falha sem
   desconectar partidas nem substituir o release atual.
6. Testa boot/KCP interno, verifica manifesto e ausência dos helpers Development,
   publica e confere health/protocolo. Conserva os três últimos rollbacks criados
   pelo CI; backups manuais com outros nomes ficam preservados.

Não há dois deploys desse workflow em paralelo. Falha em testes/build impede
publicação; falha na ativação restaura binário e supervisor anteriores. Os logs
das salas ficam fora da pasta substituída. Um commit posterior não altera os
artefatos de um commit anterior.

## Artefatos para a equipe

No GitHub, abra Actions → Online release v0.5.0 → execução → Artifacts:

- `SB6-Windows-Client-<SHA>`: extrair tudo e abrir SB6.exe para jogar no público.
- `SB6-Windows-Server-<SHA>`: inclui supervisor, README e Start-Local-Server.cmd.
  Requer Python 3. Para testar no mesmo PC, iniciar o script e abrir o cliente
  com `SB6.exe -online-directory http://127.0.0.1:18081`.
- `SB6-Validation-<SHA>`: relatórios de testes, logs de compilação e publicação,
  manifesto e pacote Linux. Retenção configurada em 14 dias.

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
