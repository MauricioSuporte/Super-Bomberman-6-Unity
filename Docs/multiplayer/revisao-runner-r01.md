# Revisão das divergências do runner R01 — 06/11/2026

Comparação somente de leitura realizada pelo Codex no Windows. Nenhum arquivo do runner ou Packages foi alterado; não houve build, smoke ou publicação. Claude permanece responsável pela implementação e execução.

## Resultado por arquivo

- As três fontes C# são as modificações R01 preparadas pelo Claude. Usar cópia temporária com backup e restauração condicionada a hashes; não fazer reset/clean global. Verificar ocupação antes de copiar e restaurar somente após todos os processos terminarem.
- `Assets/Resources/OnlineBattleAudio.asset`: o diff do runner contém apenas espaço após `contentHash:` e duas entradas extras de áudio. GUID `70ecab0eb72a4e5fbe85b811b9ba9136` = `Assets/Resources/Sounds/OnlineChat/player_leave.wav`; GUID `aeb7e87aa90a4d3a8021e0741171a159` = `player_join.wav`. Não foi encontrada alteração de física, pose ou movimento nesse diff. A diferença pode afetar a cobertura de áudio; não declarar equivalência audiovisual completa. Preferir igualar temporariamente ao original com backup se o mecanismo permitir, em vez de habilitar aceitação genérica de quaisquer arquivos locais do runner. Não ativar automaticamente `acceptRunnerLocalDiffs` com base neste parecer.
- `Assets/Resources/OnlineBuildIdentity.txt`: original contém `16f96722fc3cc5ef227798a436d97e24f7f27a98`; runner contém `b391172d18e0303ddbee30e93c1387f26202057b`. Não são prova das fontes dirty atuais. `Tools/Ci/release.py` grava esse recurso com o commit durante build de release; `Assets/Editor/EditorOnlineBuildIdentity.cs` o atualiza antes do Play Mode interativo. Não usar nenhum desses valores como identificação suficiente do teste R01. O manifesto das fontes e assemblies da build deve identificar o teste dirty. Uma cópia temporária do recurso deve ser registrada e restaurada, sem falsificar identidade de publicação.
- `.meta` da identidade: original GUID `79d9496dfe1b13640945cf1791854b7c`; runner GUID `d825233113a388b49b3da42c393c226f`. Não é somente quebra de linha. Caso sincronize o recurso, tratar o par `.txt`/`.meta` explicitamente e verificar referências ao GUID; não criar GUID novo.
- `Packages/manifest.json`: original usa `com.unity.sdk.linux-arm64`, `com.unity.sdk.linux-x86_64` e `com.unity.toolchain.win-x86_64-linux` em 1.1.1; runner usa 1.1.0. O lock acompanha essas versões e `com.unity.sysroot.base`. São ferramentas/sysroots para build Linux, mas não foi demonstrada equivalência geral dos ambientes.

## Continuação sem perda das alterações do usuário

Preparar no procedimento assistido uma sincronização temporária revisável das fontes e pares de assets necessários, incluindo backup do estado preexistente do runner e detecção de alterações posteriores antes do restore. Não substituir os Packages do original, não commitá-los como parte do R01 e não presumir autorização para ignorar diferenças.

Para Packages, a solução preferida para testar exatamente o ambiente atual é propor uma cópia TEMPORÁRIA dos dois arquivos originais para o checkout isolado, preservando integralmente os arquivos anteriores para restauração. Isso altera apenas o runner, pode importar/downloadar pacotes e precisa ser explicitado antes de executar, já que o fluxo acordado até aqui não mexia em Packages. Se for escolhido manter os pacotes antigos para um smoke exclusivamente Windows, registrar claramente a limitação e obter decisão específica; não tratar como validação Linux ou da configuração atual completa.

Este parecer fornece os dados que faltavam ao Claude, não autoriza flags genéricas de aceitação nem comprova compilação. Preparar as operações concretas e continuar pelo caminho de computer use já confirmado, sem devolver tarefas genéricas de comparação ao usuário.
