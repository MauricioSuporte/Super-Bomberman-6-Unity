# Passagem de trabalho para Claude — predição online do Super Bomberman 6

> Estado conferido em 06/11/2026. Este documento é um ponto de partida, não substitui o código, o `AGENTS.md` nem o checkpoint atualizado. Confira novamente branch, arquivos modificados, protocolo e publicação antes de trabalhar.

## 1. Pedido do usuário e resultado esperado

Continue a implementação do plano de predição online, começando pelo **R01** e seguindo as pendências reais do checklist. Trabalhe até concluir o lote autorizado, sem parar a cada pequena decisão de implementação. Preserve a experiência offline, a autoridade do servidor e as alterações locais do usuário.

Implemente, compile, teste, atualize o checkpoint e, quando a publicação estiver autorizada e acessível, acompanhe o pipeline até confirmar servidor, cliente e atualizador. Não encerre apenas propondo um plano quando já puder executar o trabalho.

Se faltar acesso ou informação indispensável, peça ao usuário especificamente o que está faltando, explique o que isso impede e continue as partes independentes. Não declare uma validação que não executou.

## 2. O que falta de verdade

**25 de 33 IDs estão implementados/testados; 8 estão parciais.** Não são apenas todos os itens de R01 em diante: **R04 e R07 já estão marcados**, enquanto **P0 e A14 também continuam pendentes**. A consolidação da documentação/skill tem uma caixa separada, fora dos 33 IDs. A validação perceptual/manual permanece separada inclusive nos IDs marcados.

| ID pendente | Estado registrado e próximo trabalho |
|---|---|
| P0 | Instrumentação/contexto existem; obter capturas online/offline comparáveis e medições por ação. |
| A14 | Manutenção/regressões das habilidades já previstas; checkpoint registra descoberta/fixtures COM e matriz incompleta. Confronte plano, histórico e código para delimitar o restante; não presuma que só falta COM. |
| **R01 — próximo** | Alinhamento de posição/pose e geração de montaria já parcialmente implementado. Faltam capturas entre peers, custo e validação das transições/papéis. Auditar antes de reescrever. |
| R02 | Apresentação co-op, geometria/clock e métricas por família existem. Completar análise de curvas e comparação de custo, CPU e rede. |
| R03 | Inimigos com cortes/pausa e gates parciais. Completar curvas e capturas por família, especialmente movimentos em tiles/saltos. |
| R05 | Projéteis: algumas famílias já têm gates/stream. Completar trajetórias, impactos, homing aprovado e demais famílias. |
| R06 | Bosses/Bombers: gates parciais. Completar telegraphs, intro, curvas, poses, áudio e famílias restantes. |
| R08 | Objetos/ambientação móveis: gates e stream parciais. Completar curvas, áudio, capturas e famílias restantes. |

R04 e R07 podem exigir regressões ou QA adicional, mas não devem ser automaticamente tratados como não implementados. Não some esforço por contagem de caixas: 25/33 não é uma estimativa percentual do trabalho restante.

**Atenção ao histórico:** há um parágrafo antigo perto da lista dizendo “23 de 33”. Ele ficou desatualizado. O resumo atual no início do arquivo, as caixas dos IDs e a confirmação final de C10 registram **25/33**. Reconcilie esse texto ao manter a documentação; não use um trecho histórico isolado para desfazer conclusões posteriores.

## 3. Baseline confirmado nesta passagem

- Repositório: https://github.com/MauricioSuporte/Super-Bomberman-6-Unity
- Checkout original: `C:/Users/mauri/source/repos/Super-Bomberman-6-Unity`.
- Branch de integração: `v0.5.0`. O fluxo atual trabalha nela; não crie worktree ou migre para outra branch sem combinar com o usuário.
- Unity instalado/usado: **6000.6.0f1**, conforme `ProjectSettings/ProjectVersion.txt`. Algumas instruções históricas citam outra versão; confira o arquivo atual.
- Renderização: URP 2D, PPU 16, referência 256×224. Preserve o fallback pixel-perfect e o safe-frame.
- Protocolo publicado: **65**.
- Commit de runtime publicado de C10: `b391172d18e0303ddbee30e93c1387f26202057b`.
- HEAD documental conferido: `b7c77eff3` — pode ter avançado após este documento.
- Actions C10 concluído com sucesso: https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/42204673208
- Servidor ativo, build-info Windows e ponteiro do atualizador foram conferidos no mesmo commit/protocolo. O binário publicado não contém o harness de Development.
- Última evidência: suíte Unity **1041/1041**, seguida de **4/4 testes finais**; os grupos se sobrepõem, portanto não some como 1045 testes distintos. Python: **77 CI + 15 RoomDirectory = 92**.
- Quatro cenários com dois clientes Mirror reais renderizados: Sudden Death, FallingBomb, pólvora e teto. Repetição final de Sudden Death no código final, sem erros/avisos do servidor. Cada cliente aplicou as mesmas 104 células de pouso do servidor; houve pausa/resume e recarga de rodada.
- Isso não equivale a aceite manual, teste com latência de internet ou comprovação de input lag zero.

No início desta passagem, havia duas alterações **do usuário**:

```text
Packages/manifest.json
Packages/packages-lock.json
```

Não reverta, sobrescreva ou inclua essas alterações no seu commit. Leia `git status`/diff de novo: o usuário pode ter alterado mais arquivos depois. Não apague `Assets/_Recovery/`, logs de testes ou arquivos não rastreados indiscriminadamente.

## 4. Arquivos para ler, nesta ordem

Todos os caminhos abaixo são relativos ao checkout original indicado acima.

| Arquivo | Finalidade |
|---|---|
| `AGENTS.md` | Regras obrigatórias do projeto, encoding, engine, arquitetura, assets e validação. |
| `Docs/multiplayer/progresso-predicao-online.md` | **Checkpoint principal**: checklist por ID, histórico, pendências, commits, testes e publicação. Leia o resumo atual e o final; o histórico tem tentativas antigas. |
| `Docs/multiplayer/plano-aplicacao-predicao-online.md` | Escopo por ID, abordagem permitida, exclusões e critérios de investigação. |
| `Docs/multiplayer/prediction-and-reconciliation.md` | Notas técnicas mantidas e contratos dos presenters/replay/autoridade. |
| `Docs/multiplayer/matriz-validacao-predicao-online.md` | Matriz de cenários e QA manual/perceptual. |
| `.codex/skills/super-bomberman-6-online-prediction/SKILL.md` | Playbook de predição, reconciliação, trajetórias, pausa, cleanup e falhas aprendidas. Pode ser lido como Markdown pelo Claude. |
| `.codex/skills/super-bomberman-6-online/SKILL.md` | Autoridade, fluxo multiplayer e referências de arquitetura/validação. |
| `.codex/skills/super-bomberman-6-unity/SKILL.md` | Convenções gerais e referências de gameplay/renderização/QA. |
| `Docs/multiplayer/online-battle-flow.md` | Fluxo online atual. Documentos antigos de arquitetura/POC podem conter afirmações superadas. |
| `Docs/multiplayer/dedicated-server.md` | Infraestrutura, build, implantação, conectividade e logs. |
| `ProjectSettings/EditorBuildSettings.asset` | Cenas habilitadas e ordem real. |
| `ProjectSettings/ProjectVersion.txt` e `Packages/manifest.json` | Engine/pacotes efetivamente usados. |

As referências internas das skills também são úteis, especialmente `references/architecture.md`, `references/validation.md`, `references/current-baseline.md` e `references/qa-checklist.md` nos respectivos diretórios.

## 5. Mapa de código, começando por R01

Pesquise os arquivos atuais com `rg --files`/`rg`; não confie em números de linha deste histórico.

- `Assets/Scripts/Netcode/NetworkPlayerSetup.cs`, `NetworkPlayerAnimation.cs`, `NetworkMountAnimation.cs`, `NetworkVisualPose.cs`, `NetworkPlayerState.cs` e `NetworkPlayerInput.cs`: autoridade, root, pose, montaria, geração e timeline. Confira a localização real dos três primeiros com `rg` caso mudem.
- `Assets/Scripts/Netcode/NetworkBombFx.cs` e partials `NetworkBombMotion*.cs`: previsão/confirmação e apresentação de bombas.
- `Assets/Scripts/Netcode/OnlineWorldBombMotion.cs`: movimento aprovado que sobrevive à remoção do ator/presenter original.
- `Assets/Scripts/Netcode/OnlineCoopWorld.cs`, `OnlineCoopSprites.cs`, `OnlineCoopCollision.cs`, `OnlineCoopStageStatus.cs`, `OnlineCoopCinematics.cs`: transporte e apresentação co-op, catálogo, geometria e câmeras.
- `Assets/Scripts/Netcode/NetworkWorldMount.cs`, `NetworkEggFollower.cs` e `Assets/Scripts/Mounts/`: montarias no mundo, ovos, assento/HeadOnly, montagem/desmontagem e poses.
- `Assets/Scripts/Enemies/`, `Assets/Scripts/Bosses/`, `Assets/Scripts/Bombers/`, `Assets/Scripts/StageAssets/`: famílias R03/R05/R06/R08 e seus fluxos nativos.
- `Assets/Scripts/SuddenDeath/BattleSuddenDeathController.cs` e `.Network.cs`: exemplo C10 de animação aprovada separada de commit de terreno.
- `Assets/Scripts/Netcode/OnlineVersionAuthenticator.cs` e `Tools/RoomDirectory/supervisor.py`: versões de protocolo que precisam permanecer alinhadas.
- `Assets/Scripts/Diagnostics/GameplayLatencyDiagnostics.cs`, `MotionDiagnosticProbe.cs` e `Tools/Diagnostics/analyze_prediction.py`: coleta e análise, sem confundir frame observado com latência física do controle.

Para R01, primeiro siga um player remoto completo: root interpolado → corpo/pose → montaria/cabeça → término da ação → retorno à locomoção. Compare owner, observador e autoridade; inclua troca de montaria/geração, morte, emote/AFK, pausa e mudança de rodada. Não simule os inputs do outro jogador para “corrigir” sua pose.

## 6. Contratos de implementação que precisam ser preservados

1. **Offline é a referência funcional.** Identifique gatilho, estados, curva, colliders, sprites, áudio, cancelamento e conclusão antes de mexer no online. Verifique offline/2P local quando o código é compartilhado.
2. **Predição local é apresentação reversível.** Inputs conhecidos do próprio player podem iniciar feedback imediatamente. O servidor confirma/rejeita; a reconciliação restaura estado e reaplica inputs sem executar novamente edges de habilidades, sons ou spawns.
3. **Outros jogadores e decisões desconhecidas usam apresentação aprovada.** Interpole root/pose na mesma timeline. Extrapolação só curta e segura, sem atravessar paredes, wraps, teleporte ou mudança de geração. Não sorteie alvo/ataque no cliente.
4. **Servidor mantém dano, morte, coleta/posse, RNG de gameplay, terreno, pontuação e fim de rodada.** Não reative física/scripts de cena em massa no cliente. Terminar uma animação não concede posse, proteção, controle ou commit de terreno.
5. **Uma autoridade de apresentação por transform/pose.** Não crie outro manager/presenter concorrendo com os existentes. NetworkTransform continua recebendo dados; seu root antigo não pode sobrescrever um arco especializado ativo.
6. **Tempo e identidade:** correlacione entidade, tick real da ação, revisão, cena, tipo/geração da montaria e início/fim. Root e pose devem usar timeline/offset compatíveis. A câmera e os bounds do cliente também podem estar desativados para gameplay.
7. **Continuidade:** respeite curva e pixel grid nativos. Preserve segmentos curtos em ordem quando necessário; não adicione lead novamente a cada tile. Um stop antigo não pode descartar uma aprovação mais nova em fila.
8. **Pausa e cleanup:** congelar o progresso apropriado, distinguir snapshot antigo de retomada, rejeitar mensagens velhas e limpar filas/visuais/reservas em disable, morte, scene change e disconnect. Coroutines Unity podem continuar quando apenas o componente é desativado; cancelar só o sprite não basta.
9. **Client visual não é gameplay nativo.** Prefira helpers visuais auditados; não inicie coroutines de explosão, landing ou limpeza para reproduzir uma animação. Não duplique efeitos no host.
10. **Cuidado com prefabs offline que têm NetworkBehaviours.** `!isServer` sozinho não significa client ativo. SyncVars não inicializadas não podem apagar skin, inventário ou montaria offline.
11. **Controle local usa slot de hardware 1**, independentemente do playerId P1–P6 atribuído pelo servidor. Não confunda conexão, playerId e controle local.
12. **Limites mensuráveis:** registre custo de bytes/CPU, filas e timeout. Não prometa zero latência física, nem trate teste headless como prova de pixels na GPU.

## 7. Fluxo de trabalho por lote

1. Confira checkout/branch/HEAD, `git status`, diff e processos Unity/Actions ativos. Registre os arquivos do usuário e os próprios.
2. Leia o escopo do ID e o código atual. Identifique o que já existe e uma reprodução concreta; não implemente tudo de novo com base no texto antigo.
3. Registre no checkpoint o problema, hipóteses, papel afetado, arquivos, mudanças previstas e testes necessários.
4. Faça um lote pequeno no domínio existente. Preserve GUIDs `.meta`, referências e defaults serializados. Se mudar contrato de rede, alinhe protocolo cliente/servidor/supervisor e publique ambos.
5. Compile C#, execute testes Unity relevantes e regressões adequadas. Corrija erros e avisos introduzidos; diferencie avisos legados comprovados.
6. Gere binários novos antes do smoke. Teste com servidor e dois clientes reais; para R01, confira **o observador**, não só o owner.
7. Compare evidências de cada cliente com a autoridade, inspecione imagens quando há renderização e registre papéis/cenários ainda não cobertos.
8. Atualize checklist, matriz e skill/notas quando houver aprendizado reutilizável. Só marque concluído com evidência identificada; QA manual pendente fica separado.
9. Revise diff/encoding, stage apenas arquivos próprios, commit e publicação autorizada. Acompanhe o Actions até o resultado final e verifique o canal público.
10. Salve um checkpoint concreto antes de encerrar ou se aproximar do limite de sessão. Não deixe apenas “continuar testes” sem comandos, logs e estado dos processos.

### Encoding e arquivos gerados

Leia UTF-8 explicitamente (`encoding="utf-8-sig"` no Python); escreva `encoding="utf-8"`, preservando o estilo de newline. Verifique os primeiros bytes dos `.cs` alterados: sem BOM corrompido literal, BOM duplicado ou `U+FFFD`. Não edite `.csproj`, `Library/`, `Temp/` ou outros resultados gerados como se fossem fonte.

## 8. Ambiente de validação usado nesta sessão

O runner isolado existente neste computador ficava em:

```text
C:/Users/mauri/.sb6-actions-runner/_work/Super-Bomberman-6-Unity/Super-Bomberman-6-Unity
```

Esse caminho **não é garantido no ambiente do Claude**. Confirme acesso e disponibilidade. Não abra outra Unity nesse projeto enquanto o Actions ou outro teste o estiver usando. Nunca feche o Editor do usuário nem peça para salvar/sobrescrever sua cena como condição para testar código em outro checkout.

No runner livre, foram copiados **somente os arquivos próprios** e seus `.meta`, usando um manifesto em `Logs/PredictionCxx-owned-paths.json`. Antes disso, foi salvo seu estado dirty. Após todos os processos saírem, foram restaurados apenas os tracked próprios e removidos apenas os novos arquivos próprios daquele checkout. Não copie `Library` do usuário, não faça limpeza global e não trate modificações geradas preexistentes como suas.

Os smokes usam um servidor e dois processos de cliente. O computador tinha 16 GiB; o script exige pelo menos 3 GiB livres para iniciar. Espere a Unity de build terminar antes de iniciar os três players. Não rode suítes/builds/smokes concorrentes no mesmo checkout. Dê progresso ao usuário durante operações longas.

## 9. Comandos reproduzíveis

Execute no checkout original. Adapte os caminhos apenas depois de conferir o ambiente. Os exemplos são receita; este documento não afirma que o Claude já os executou.

### Testes Python

```powershell
python -m unittest discover -s Tools/Ci -p "test_*.py" -v
python -m unittest discover -s Tools/RoomDirectory -p "test_*.py" -v
```

### Unity: suíte e build Development no runner livre

O helper `Tools/Ci/release.py` fornece `unity(...)` com processo oculto, timeout e heartbeat do log no Windows. Exemplo de script Python salvo em `Logs/R01-validate.py`:

```python
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

repo = Path("C:/Users/mauri/source/repos/Super-Bomberman-6-Unity")
runner = Path("C:/Users/mauri/.sb6-actions-runner/_work/Super-Bomberman-6-Unity/Super-Bomberman-6-Unity")
sys.path.insert(0, str(repo))
from Tools.Ci import release as ci

# PRECONDIÇÕES: runner livre, fontes próprias já sincronizadas, usuário preservado.
ci.ROOT = runner
ci.REPORTS = runner / "Build/CI"
report = repo / "Logs/R01-tests.xml"
ci.unity([
    "-runTests", "-testPlatform", "EditMode",
    "-testFilter", "OnlineRoomEditModeTests;BattleModeComEditorTests",
    "-testResults", report,
], repo / "Logs/R01-tests.log")
result = ET.parse(report).getroot()
assert result.get("result") == "Passed" and int(result.get("failed", "1")) == 0
ci.unity([
    "-quit", "-executeMethod", "OnlineClientBuild.BuildPerformancePlayer",
], repo / "Logs/R01-development.log")
```

Leia XML/log de verdade; não infira o resultado pelo número esperado. Novos testes podem exigir ajustar o filtro. A classe wrapper COM é `BattleModeComEditorTests`; o conjunto online usa partials de `OnlineRoomEditModeTests` em `Assets/Tests/EditMode/Editor/`.

A build Development gráfica sai em `Build/ServerBenchmark/Windows/SB6.exe` **dentro do runner**. O mesmo executável suporta `-server -nographics` e os clientes. A build Windows normal de publicação tem outro caminho/nome e não serve para os fixtures Development.

### Checagem C# adicional

Quando o `.csproj` gerado estiver disponível:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore -v:q -m:1 -nodeReuse:false -p:UseSharedCompilation=false
```

Se faltar `project.assets.json`, rode o restore/build adequado. Se um `.cs` novo ainda não entrou no projeto gerado, regenere corretamente ou use um `.targets` temporário em `Logs/` com `CustomBeforeMicrosoftCommonTargets`, condicionado **somente** a `Assembly-CSharp` e excluindo `@(Compile)` já incluído. Não corrija isso editando o `.csproj` à mão nem propague o Include para assemblies de pacotes. A compilação Unity continua necessária.

### Smoke gráfico já existente, útil como regressão de R01

```powershell
$runner = 'C:/Users/mauri/.sb6-actions-runner/_work/Super-Bomberman-6-Unity/Super-Bomberman-6-Unity'
$binary = Join-Path $runner 'Build/ServerBenchmark/Windows/SB6.exe'
python Tools/RoomDirectory/smoke_full_clients.py --server $binary --client $binary --output Logs/R01-mount-visuals-01 --port 29020 --seconds 90 --diagnostics --rendered --mount-visuals
```

O diretório de saída precisa ser **novo** e a porta UDP local precisa estar livre. Essa é uma regressão de montaria/emote/AFK/cabeça, não uma suíte completa de R01. Estenda o harness existente `Assets/Scripts/Netcode/LocalServerLoadTest.cs` para a reprodução específica de player remoto, em vez de afirmar cobertura que esse flag não oferece.

Confira `python Tools/RoomDirectory/smoke_full_clients.py --help` e as combinações permitidas no código. Exemplos adicionais:

```powershell
# C10: quedas, pausa e commits do Sudden Death em dois clientes gráficos.
python Tools/RoomDirectory/smoke_full_clients.py --server $binary --client $binary --output Logs/R01-regressao-sd-01 --port 29021 --seconds 60 --diagnostics --rendered --mechanisms sudden

# Co-op básico; sem flag de fixture gráfica, este caminho é headless.
python Tools/RoomDirectory/smoke_full_clients.py --server $binary --client $binary --output Logs/R02-coop-base-01 --port 29022 --seconds 90 --mode NormalGame --scene Stage_1-1 --diagnostics --require-unlocked
```

Não acrescente `--rendered` a qualquer combinação indiscriminadamente: o script exige uma fixture gráfica suportada. Não desative os validadores para obter “verde”. Guarde tentativas falhas, corrija a causa e repita com outro diretório.

### Evidência que deve ser inspecionada

- `result.json`/`failure.json`, `config.json`, hashes das assemblies e logs do servidor e dos dois clientes.
- `server-motion.jsonl`, `client-1-motion.jsonl`, `client-2-motion.jsonl`, `metrics.jsonl` e PNGs quando gráficos.
- Mensagens/eventos por entidade, tick, geração e revisão; confirmação, timeout, pausas, correções e término da ação.
- Posições e poses reais do observador; câmera/visibilidade, head/assento, sorting e retorno ao movimento.
- Finalize os writers e aguarde saída/flush dos processos antes de copiar/analisar JSONL. Não esconda linha truncada de uma captura ainda ativa.

```powershell
python Tools/Diagnostics/analyze_prediction.py --online <online.jsonl> --offline <offline.jsonl> --output Logs/R01-comparacao.json
```

Compare mesma fase, loadout, ação, papel e condições. Registre FPS, RTT/jitter/perda quando disponíveis. Uma primeira medição de custo não prova melhora sem baseline comparável. Benchmark sintético não substitui dois clientes reais nem inspeção gráfica.

## 10. Onde procurar exemplos e evidências anteriores

| Caminho | Uso |
|---|---|
| `Assets/Tests/EditMode/Editor/OnlineRoomEditModeTests.cs` e demais `OnlineRoom*Tests.cs` | Regressões e fixtures existentes; pesquisar pose, mount, timeline, generation, pause e cleanup. |
| `Assets/Tests/EditMode/Editor/OnlineRoomVisualRepairTests.cs` | Casos visuais de montaria/poses; auditar os testes que de fato interessam a R01. |
| `Assets/Tests/EditMode/Editor/OnlineRoomSuddenDeathTimelineTests.cs` | C10: commit explícito, late snapshot, pausa, curva, offsets e cancelamento/reuso offline. |
| `Tools/Ci/test_*smoke.py` | Contratos dos verificadores de integração; não confundir com execução dos players. |
| `Tools/RoomDirectory/validate_*smoke.py` | Verificadores dos eventos apresentados em cada cliente. |
| `Tools/RoomDirectory/benchmark_local.py`, `summarize_benchmarks.py` | Custo/capacidade; ler limitações de cada modo antes de usar resultados em R02. |
| `Logs/PredictionC10Full.xml/.log`, `PredictionC10Final.xml/.log` | Evidência local das suítes de C10. |
| `Logs/PredictionC10Peers-*` e `Logs/PredictionC10Peers2-sudden` | Capturas dos quatro cenários e repetição final. |
| `Logs/PredictionC10-owned-paths.json`, `PredictionC10-user-packages.json` | Exemplo de manifesto próprio e hashes dos arquivos preservados. |
| `Logs/PredictionC10-live-server.json`, `PredictionC10-public-updater.json` | Confirmações da publicação 65. |
| `Build/CI/` e `Logs/CI/` no runner | Relatórios/manifestos da execução atual de Actions; podem ser substituídos no próximo job. |

**`Logs/`, builds e caches normalmente não estão no Git.** Ter acesso ao GitHub não significa ter essas evidências. Se não existirem no ambiente do Claude, peça ao usuário o pacote relevante ou execute novamente com permissão e infraestrutura disponíveis. Não fabrique os relatórios nem trate caminhos deste documento como arquivos anexados.

## 11. Publicação: seguir o fluxo atual

- Workflow: `.github/workflows/online-release.yml`; implementação: `Tools/Ci/release.py`, `Tools/Ci/activate_release.py` e `Tools/RoomDirectory/deploy_staged.py`.
- Push de runtime para `v0.5.0` normalmente inicia homologação **both/quick**. O workflow também permite despacho manual; confira a configuração atual.
- `quick` pula a suíte Unity **dentro do pipeline** para acelerar iteração, mas compila os players e roda os testes Python. Por isso os testes Unity locais do lote precisam estar documentados.
- O job tem restrições de repositório/branch/ator. Se ficar skipped, confira essas condições; não contorne controles de acesso.
- Mudou mensagem/protocolo? Alinhe `OnlineVersionAuthenticator.ProtocolVersion` e `supervisor.PROTOCOL`; gere **cliente e servidor compatíveis**. Não use `[server-only]` nesse caso.
- Em C10 o usuário já tinha autorizado o fluxo de commit/push/publicação. Se essa autorização não acompanhar a nova sessão, esclareça uma vez o escopo de publicação antes de executar o deploy. Depois, não solicite novamente a mesma autorização a cada etapa.
- Deploy reinicia o serviço e desconecta salas. Não repita ativação às cegas se SSH interromper: confira estado/log e rollback da transação.
- Acompanhe até o término. Build verde não significa servidor publicado; servidor publicado não significa canal do cliente atualizado.
- Confirme: sucesso do Actions, `deployment.json` ativo com commit/protocolo esperados e `benchmarkHarness=false`, `build-info.json` do cliente, `deploy-status.json` e ponteiro da Release `online-v0.5.0` no mesmo commit. Verifique asset/hash quando acessível.
- Não publique tokens. O pipeline usa autenticação local/GitHub Actions/SSH configurados; isso não é transferido por este Markdown.
- Checkpoints exclusivamente documentais foram commitados com `[skip ci]`; a build de runtime continua identificada pelo commit que gerou seus artefatos. Não confunda HEAD documental posterior com a identidade do binário.

## 12. O que solicitar ao usuário se não tiver acesso

Peça apenas o necessário, agrupando os bloqueios reais. Não peça novamente dados que puder obter nos arquivos ou conexões disponíveis.

| Falta no ambiente | Pergunta ou material necessário |
|---|---|
| Checkout/arquivos | Caminho do clone acessível e branch; ou disponibilização do repositório e dos documentos acima. |
| Estado atual | `git status`, HEAD/branch, alterações não salvas e se há Editor/Actions usando o projeto. |
| Unity/compilação | Caminho do executável, versão, licença operante e módulos Windows/Linux Dedicated Server. Se sandbox não permite Unity, pedir execução dos comandos pelo usuário/runner e XML/log completos. |
| Runner isolado | Caminho autorizado, como iniciar jobs/comandos e disponibilidade. Não suponha que o caminho Windows desta passagem exista em outro computador. |
| Smoke Windows/GPU | Máquina Windows acessível, permissão para três processos ocultos, GPU/URP funcional, binários Development correspondentes e porta local livre. Sem isso, peça execução assistida e o diretório completo de resultados. |
| MCP | Endereço/configuração da integração disponível e qual projeto/Editor ela controla. MCP é opcional; não é requisito se CLI resolve. Ferramentas internas do Codex podem não existir no Claude. |
| Logs do problema | JSONL do cliente e servidor da mesma partida, commit/protocolo, fase, habilidade, owner/observador, sequência de inputs e horário aproximado. Para R01, indicar em qual tela o problema é visível. |
| Comparação offline | Captura equivalente e passos de reprodução com personagem/montaria/loadout, FPS e condições de rede registrados. Vídeo ajuda no aceite perceptual. |
| GitHub/Actions | Conexão/autenticação local autorizada e capacidade de ver jobs/artefatos e fazer push/despacho, caso publicar esteja no escopo. |
| Servidor | Host/alias confirmado, acesso VPN/tailnet se necessário, usuário SSH/configuração já instalada e permissão de leitura/deploy/reinício conforme o escopo. O default no script não prova conectividade nem autorização. |
| QA humano | Duas instâncias/jogadores disponíveis para a sequência específica ainda não validada automaticamente. Não pedir “teste tudo”; entregar uma lista curta de ações e o resultado esperado. |

**Não solicite que o usuário cole tokens, senha, chave SSH privada ou o conteúdo inteiro de saves no chat.** Prefira conexões/autenticação locais e saídas redigidas. Se só for possível pedir execução manual, entregue comandos concretos, diretório de saída e quais arquivos retornar; não conclua smoke/publicação sem os resultados.

## 13. Checkpoint obrigatório ao interromper

Atualize `Docs/multiplayer/progresso-predicao-online.md` com:

- ID, objetivo e o que está implementado versus hipótese/pendência.
- HEAD/branch/protocolo e alterações do usuário que precisam ser preservadas.
- Lista/manifesto dos arquivos próprios, incluindo novos `.meta`.
- Último teste realmente executado, resultado e caminhos XML/log/JSONL/PNG.
- Processos/jobs ainda ativos, qual checkout ocupam e o que aguardar; sem identificadores de ferramentas exclusivas do Codex como única forma de retomada.
- Comando ou script reproduzível para continuar, pré-condições e eventual diferença entre fontes atuais e binário testado.
- Estado exato da publicação: não iniciada, build, deploy, canal do cliente ou sucesso confirmado.
- Próxima ação concreta e bloqueios que dependem do usuário.

Não marque um ID concluído porque acabou o crédito ou porque compilou. Não descarte tentativas falhas nem atenue critérios apenas para fazê-las passar.

## 14. Primeira ação recomendada para o Claude

1. Ler os arquivos da seção 4 e conferir o estado real do repositório.
2. Confirmar quais acessos da seção 12 já estão disponíveis; solicitar somente os indispensáveis ausentes.
3. Auditar **R01** contra as implementações atuais de root/pose/montaria/geração. Aproveitar A11, C02–C05 e R07 já implementados.
4. Escolher uma reprodução de owner/observador com captura mensurável, completar o lote, validar e atualizar o checkpoint.
5. Prosseguir pelas pendências reais mantendo a mesma disciplina de autoridade, evidências e publicação.

## 15. Complemento: Claude em VM Linux sem execução Windows

### O que a limitação significa

Uma montagem do repositório Windows em uma VM Linux permite ler/editar arquivos, mas não necessariamente iniciar processos no Windows. Informar o caminho do runner não cria essa capacidade. Nesta sessão do Codex, o shell era PowerShell executado diretamente no Windows; isso não é uma ferramenta automaticamente compartilhada com Claude.

Confirme primeiro quais ferramentas a SUA sessão oferece: terminal local Windows, conector de execução, SSH/WinRM já configurado ou MCP que realmente exponha os comandos necessários. Não invente nomes de ferramentas e não suponha que MCP de Unity forneça shell, build e smoke. Se houver um canal autorizado, faça uma leitura simples de versão/caminhos para confirmar onde executa antes de modificar o ambiente. Não instale serviço de acesso remoto nem abra portas sem autorização.

No Windows original, o runner existe no caminho da seção 8 e ProjectVersion.txt indica 6000.6.0f1. Isso não comprova que esteja livre nem que Claude consiga executá-lo. Use o checkout atual como fonte: nesta conferência as duas alterações de Packages continuavam presentes, além do checkpoint modificado. Uma visão diferente na VM exige comparar checkout, branch, HEAD, caminho montado e diff; não significa que seja seguro substituir os arquivos.

### Como continuar sem bloquear todo o desenvolvimento

1. Continue a auditoria R01 nos arquivos originais. Entregue uma matriz concreta de cenários owner/observador, transições, métricas esperadas e lacunas de cobertura. Não altere runtime apenas para corrigir uma hipótese não reproduzida.
2. Prepare uma reprodução pequena reutilizando o harness e os testes existentes. Pode preparar código/testes para revisão e execução assistida, registrando claramente que ainda não compilaram; não finalize nem publique C# sem passar pelas validações exigidas. Não é preciso limitar todo o trabalho a documentação por falta de Unity local.
3. Rode os testes Python pertinentes que o ambiente suportar e registre seus resultados. Eles não validam compilação C# ou a apresentação entre peers.
4. Prepare um script reproduzível para Windows com preflight, checkout/commit esperados, caminhos de XML/log, build Development e smoke específico. Use as receitas da seção 9; sincronização das fontes e preservação do runner são pré-condições. O script não deve fechar o Editor, limpar globalmente, publicar ou modificar Packages.
5. Se não houver execução conectada, peça ao usuário uma única execução assistida desse script e a pasta completa de resultados. Entregue o arquivo e o comando exato, em vez de pedir genericamente “uma máquina com Unity”. Após receber resultados, diagnostique e continue.
6. Continue o trabalho independente enquanto aguarda. Mantenha R01 parcial até ter evidência suficiente; não marque concluído apenas pela auditoria.

Pergunta objetiva ao usuário, somente se os conectores disponíveis não resolverem:

> Meu terminal executa Linux e só alcança os arquivos. Há um conector autorizado para executar PowerShell no Windows? Se não houver, vou preparar o script de validação para você executar no Windows e me devolver a pasta de resultados. O caminho do runner sozinho não me permite iniciar Unity nessa máquina.

### Actions: acesso remoto não exige necessariamente shell Windows

É possível consultar status, logs e artefatos do Actions a partir de Linux usando uma integração GitHub ou CLI autenticada com os escopos necessários. Portanto, não afirme que acompanhar Actions exige Windows. Descubra primeiro quais operações o conector realmente oferece. A ausência de autenticação/permissão é um bloqueio distinto da ausência de Unity local.

O workflow atual `.github/workflows/online-release.yml` usa runner self-hosted Windows com labels `self-hosted, Windows, X64, sb6-unity`. O despacho admite `target=both|server` e `profile=quick|full`, mas **não existe uma opção validation-only nesse workflow**: ele executa deploy e pode reiniciar salas e atualizar o canal do cliente. O perfil full inclui testes Unity, mas não deve ser apresentado como substituto automático dos smokes gráficos específicos do R01.

Não dispare esse workflow apenas para conseguir compilar um rascunho. Para testar sem publicar, use execução Windows assistida ou proponha uma alteração separada e revisável de CI que forneça um job de validação sem deploy. Não implemente infraestrutura adicional como se já tivesse sido solicitada. Consulte condições de ator/branch e disponibilidade do runner antes de qualquer despacho autorizado; não contorne as restrições.

### Como interpretar a hipótese OnVisualTime

A ausência de uma guarda idêntica à de NetworkMountAnimation não comprova bug em NetworkPlayerAnimation. Trace registro/desregistro do callback, condições de spawn/owner/modo e os prefabs atingidos. Reproduza o callback offline e online e verifique se ele realmente altera pose/estado indevidamente. Só então proponha a correção mínima e sua regressão. Evite uma guarda por simetria que possa desativar animações válidas.

### Entrega esperada antes de solicitar a execução assistida

- Arquivos reais consultados e cenário R01 escolhido, com comportamento esperado e como medir.
- Patch/harness necessário, se houver, acompanhado de revisão estática e status explícito de compilação pendente.
- Script Windows e comando exato, sem publicação embutida; fontes e binário identificados.
- Lista curta dos artefatos que o usuário deve devolver e próxima ação após recebê-los.
- Checkpoint atualizado nos mesmos documentos originais, sem substituir o plano por um resumo paralelo.

## 16. Revisão do script assistido apresentado pelo Claude

O arquivo `Logs/R01-validate-assisted.py` foi lido diretamente no Windows. Não execute `--step all` como evidência conclusiva de R01: os casos mount-visuals e sudden são regressões existentes, não a captura específica de root/pose de player remoto. Primeiro prepare a reprodução R01 no harness existente e sua validação. Não precisa pedir nova preferência ao usuário para preparar esse trabalho já solicitado; registre que compilação e execução estão pendentes.

Antes de entregar o script para execução, corrija estes pontos encontrados:

- O preflight apenas registra os HEADs; não os compara. Mesmo HEAD não prova fontes iguais quando há arquivos modificados ou não rastreados. Exija identidade das fontes relevantes (manifesto/hashes incluindo arquivos novos e .meta), versão e pacotes deliberadamente selecionados. Não substitua Packages para forçar equivalência. Se houver divergência, pare com explicação e procedimento de sincronização revisável.
- A checagem de ocupação só procura Unity.exe com o caminho do runner. Confira também job ativo do Actions/Runner.Worker e processos de smoke que usam o checkout; falha da consulta PowerShell deve bloquear, não parecer runner livre. Não encerre processos automaticamente.
- `free_gib` mede disco, não memória RAM. Confira ambos com nomes distintos, além de Unity instalado e porta disponível. Os smokes têm seu próprio gate de memória, que deve continuar habilitado.
- O helper git ignora returncode. Uma falha/timeout precisa ser erro explícito, não um HEAD contendo mensagem de erro.
- `smoke()` registra código não zero, mas o programa termina com sucesso. Propague a falha no exit code final e registre o resultado completo. Não declare aprovação apenas porque o script chegou ao fim.
- Grave resultado e exceção em finally, inclusive em falha de preflight, timeout ou build. Registre etapa interrompida. Preserve logs e evidências para diagnóstico.
- `--step smoke` pode usar um SB6.exe antigo sem provar a identidade de suas fontes. Exija manifesto da build/hash e identificação do binário utilizado. Existência do executável não comprova build atual.
- `--step analyze` cria pasta nova e procura logs nela, logo não encontra os resultados da execução anterior. Aceite um caminho de resultados existente para análise ou remova essa etapa enganosa. Não sobrescreva capturas anteriores.
- Subprocessos longos capturados integralmente ficam sem progresso visível até o fim; produza heartbeat/log de acompanhamento sem vazar credenciais.

Confirme também o resultado Python lendo o sumário original: em unittest, “Ran 77 tests ... OK (skipped=15)” significa 77 executados/contabilizados no total, dos quais 15 pulados, não 77 aprovados mais 15 pulados. Registre counts e ambiente exatamente, sem equiparar essa execução Linux à cobertura Windows.

Sequência esperada: preparar cenário e critérios específicos de R01 → corrigir o orquestrador assistido → revisar identidade/ocupação → compilar/testar/build/smoke no Windows → analisar capturas → corrigir runtime se a reprodução demonstrar problema → repetir e atualizar checkpoint. Os smokes já existentes entram como regressão complementar. Nenhuma publicação está implícita nessa execução.
