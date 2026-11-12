# Chameleon, fade e opções do título — 12/11/2026

## Alterações e identidade

- Unity 6000.6.0f1, branch v0.5.0, protocolo 71 (jogo e supervisor).
- Stage_1-3 inclui sheets Generated de todos os personagens no catálogo compartilhado. Sem alteração de RNG/dano/IA.
- Fades cooperativos simples interpolam snapshots de 100 ms por frame não escalado e concluem antes de ocultar. Offline mantém o fluxo nativo. Spotlight não foi alterado.
- Discord libera o lock do menu antes de abrir o link. Atualizar fica oculto até confirmar um sourceCommit publicado válido e diferente; igualdade, erro, dados ausentes/inválidos não exibem a opção. Navegação mantém a seleção de Sair quando a linha é inserida.

## Evidência executada

- Primeira baseline Unity: 1122/1122 Passed, zero failed; XML Build/CI/chameleon-fade-tests.xml, log Logs/chameleon-fade-tests.log. Compilação sem novos avisos/erros CS.
- Python final: RoomDirectory 16 testes e Ci 218 testes, ambos OK; inclui regressão real de protocolo.
- Development Windows: OnlineClientBuild.BuildPerformancePlayer concluído com sucesso. DLL SHA256 9dfd3ec29aacee52eb248e4e9e90d815184c404399c840872f147f28df9b474d. Esse build cobre Chameleon/fade, anterior aos ajustes adicionais do título.
- Smoke: python Build/CI/run_chameleon_smoke.py, modo NormalGame, Stage_1-3, servidor -server headless e dois clientes Mirror reais com renderização, 120 segundos, --coop-world/requireUnlocked, saves isolados por papel em Build/CI/chameleon-saves. Resultado em Build/CI/chameleon-online/result.json: zero erros/warnings, owners P1/P2 liberados, duas pausas/retomadas, recarga e teardown concluídos. Mesmo DLL nos três processos.
- Client1: 12550 amostras visíveis, zero sprite negativo; Client2: 12529, zero sprite negativo. 400/408 amostras pausadas. Capturas world-010 de ambos, 04:54:46 UTC, inspecionadas: sete disfarces visíveis incluindo skins de P1/P2.
- Offline: -r06-offline-stage Stage_1-3, save isolado Build/CI/chameleon-offline-native-save, exit 0 e [R06Offline] completed; Logs/chameleon-offline-native.log. Tentativa inicial usando fixture R03 foi inválida: ela exige Frog/HopRobot/SnowMan/Korobokkuru e rejeita uma fase apenas com Chameleon; timeout preservado em Logs/chameleon-offline.log, não considerado sucesso.

## Limites e publicação

- Host/COM de Battle não usam o catálogo de inimigos cooperativo; o caminho offline nativo não mudou. Não foi executado host cooperativo nesta rodada.
- A interpolação do fade foi exercitada pela regressão e pelo ciclo online; capturas de 1 Hz não medem suavidade de GPU nem substituem avaliação perceptual do fade em hardware do jogador.
- Navegação física por controle/mobile, abertura real de aplicativo Discord e APK Android não foram testadas interativamente. São usados os mesmos inputs e URL existentes.
- Baseline final após ajustes de título: 1123/1123 Passed, zero failed; Build/CI/chameleon-fade-title-tests.xml e Logs/chameleon-fade-title-tests.log. Sem warning/error CS. Publicação pelo Actions full/both concluída para servidor e canal dos clientes: revisão 4656e1b49f4a92b4eeb8bc24c192d4bcb9570075.

## Preservação e limpeza

Logs, XML, result/config JSON, hashes e capturas desta execução permanecem em Logs/chameleon-* e Build/CI/chameleon-*. Não foram removidos saves do usuário, recovery, caches ativos, código ou testes. Deltas automáticos de fontes/Physics2D e arquivos PerformanceTestRun gerados pela suíte devem ser restaurados/removidos apenas após encerrar o runner, por terem sido produzidos a partir de checkout inicialmente limpo.

Limpeza executada após o runner encerrar: restaurados somente Assets/Resources/Fonts/PressStart2P-Regular SDF.asset e ProjectSettings/Physics2DSettings.asset, alterações automáticas do teste; nenhum output de evidência removido. Arquivo compacto local: Build/CI/chameleon-evidence.zip.

## Publicação confirmada

- Actions: https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/actions/runs/43158541576 — gates de testes/build, deploy e publicação do canal concluídos com sucesso; anexação dos artefatos ainda em andamento no registro final.
- Ativação 2026-11-12 05:33:43 UTC (01:33:43 America/Cuiaba), success=true. Serviço sb6-rooms active. API pública /health: ok=true, protocolo 71, buildIdentity 4656e1b49f4a92b4eeb8bc24c192d4bcb9570075.
- Servidor Linux Assembly-CSharp SHA256 8dc300dc2546ea81abb493f3f51c560b440b47204630b5759a13d360b9d17dff, workingTreeDirtyBeforeBuild=false, benchmarkHarness=false.
- Manifesto público da release online-v0.5.0 conferido: sourceCommit correspondente e três URLs apontando para assets uploaded. Windows/Linux/Android e servidor compilados com sucesso; Unity da release 1123/1123 Passed.
- Manifests, XML da release e verificação pública preservados em Build/CI/chameleon-release-evidence. ZIP anterior SHA256 40e7cdaa313b46da823bd4b251b235cbd225cc54d63c341c6bf033a8337b9cf5. Nenhuma remoção adicional de outputs.
