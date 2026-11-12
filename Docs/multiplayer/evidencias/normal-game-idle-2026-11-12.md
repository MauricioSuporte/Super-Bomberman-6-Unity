# Normal Game online: degradação na Stage_1-3 — 12/11/2026

Correção implementada e validada no servidor dedicado Linux, sem atualizar o serviço público. Código-base `5be13c3475bca4283cf4e7d3f9551a844b21b450` com o patch desta tarefa; Unity 6000.6.0f1, protocolo 71. Identidades completas, patch, comandos reutilizáveis e hashes estão em `Build/ConnectionIdle/evidence-manifest.json` e nos scripts desse diretório.

## Evidência original e causa

A sala real `796eb953be2f0de2d42e7d816c8de850` entrou na Stage_1-3 com um jogador às 20:28:06 UTC e registrou timeout KCP de 10 s às 20:29:37 UTC, antes do prazo normal de inatividade. Os quatro arquivos de eventos retidos contêm 141.165 avisos `Trying to access a shader but no shaders were included in the build because Dedicated Server Optimizations is enabled.` As métricas chegam a 10,28 FPS, com frame de 6.706 ms; a última amostra durante a desconexão tem 6,63 FPS. Arquivos originais preservados em `Build/ConnectionIdle/events.jsonl*` e `production-metrics.jsonl`.

A captura cooperativa consulta material/shader para transportar a apresentação aprovada. `dedicatedServerOptimizations: 1` removia esses dados no subtarget Server e fazia as consultas gerarem avisos continuamente. Um Windows Player iniciado com `-server -nographics` não reproduz essa remoção: por isso o primeiro controle local de 300 s ficou conectado, apesar do tráfego excessivo.

## Alterações

- `ProjectSettings/ProjectSettings.asset`: desativar Dedicated Server Optimizations, preservando os dados consultados pela captura. O servidor continua headless, subtarget Server e URP. Não há mudança de protocolo ou de formato de mensagens.
- `OnlineCoopWorld.CaptureCoopRenderer`: considerar renderer desabilitado e `forceRenderingOff`, além de GameObject inativo. Não capturar ramos nunca visíveis ou já ocultos. Publicar desaparecimento uma vez, manter a identidade existente e enviar estado atual completo quando reaparece. Preservar as bordas de animação dos ramos visíveis.
- Regressão no owner existente: 6.000 capturas ocultas com movimento e reaparição, para cada uma das três formas de ocultação. Notas e skill de predição atualizadas.

## Gates executados

| Verificação | Resultado |
| --- | --- |
| Unity EditMode completo, configuração final | 1.126 Passed, zero Failed/Skipped |
| OnlineRoomEditModeTests | 1.023 Passed, incluindo as três novas variantes |
| BattleModeComEditorTests, classe-base no XML BattleModeComEditModeTests | 92 Passed |
| OnlineChatEditModeTests / OnlineLobbyInputEditModeTests | 8 / 3 Passed |
| Python RoomDirectory / Ci | 16 / 218 Passed, incluindo alinhamento real do protocolo |
| Linux Dedicated Development / Windows Development | Succeeded, errors=0 nos dois relatórios de build |
| UTF-8/BOM e diff | Verificados; nenhuma alteração de cena/prefab necessária |

XML, logs e saídas Python: `Build/ConnectionIdle/final-tests.*`, `python-*-tests.log`, `linux-build.log` e `windows-build.log`. Não há `error CS` ou `warning CS`. Os logs do Editor também registram mensagens internas de cache/DataStore/allocator do Unity 6000.6; os builds concluíram com zero erros reportados. O aviso ComputeBuffer no encerramento dos clients headless também existe no controle anterior e não é apresentado como resolvido por esta tarefa.

## Runtime

- **Stage_1-3, Easy, dedicado Linux + dois clientes Mirror reais, sem input sintético:** `fixed-idle-ssh`. A sala permaneceu conectada até o encerramento nativo por inatividade de cinco minutos, sem timeout KCP e sem o aviso de shaders. As 29 amostras estáveis de servidor tiveram média de 59,83 FPS e última amostra de 60,00 FPS. Tráfego médio total: 101,98 KiB/s para dois clientes. O máximo dessas métricas foi 804,6 ms, em uma ocorrência inicial; não houve a sequência progressiva de travadas de vários segundos. O HitchProbe completo retém essa ocorrência (819,1 ms incluindo espera) e os demais eventos de carregamento.
- **Pausa, retomada e teardown de cena:** `fixed-world-ssh`, 110 s, mesma fase/dificuldade e peers. Dois ciclos nativos de pausa/retomada, recarga da Stage_1-3, novo stream recebido nos dois clientes e conclusão do fixture confirmados. Sem timeout KCP ou avisos de shaders.
- **Offline local 2P:** fixture P0 na Stage_1-1, caminhada, pausa/retomada e Blue Louie, conclusão confirmada em `offline/player.log`.
- DLL servidor: `f7dbff6e6b58fc2192272947343c566a9d0d7a7383c76c3eafdb30eee7a5c5de`; DLL cliente: `829677f76fb70cfaad0171ce37c7e85c3ed2e49b28383d409e222d553f8b06ce`. Ambos usam a identidade de build do mesmo commit-base e protocolo 71. Não foi contornado o autenticador.

A porta UDP isolada da VM não recebeu conexões diretas. A tentativa `fixed-idle` ficou no lobby e foi interrompida; **não passou**. As execuções aprovadas usam KCP/UDP através de ponte SSH isolada em loopback. São dois clientes reais e um binário Linux Server real, mas não constituem aceitação de UDP/WAN pública. Saves de todas as execuções aprovadas foram isolados por processo. O fixture mantém vida 200 para permitir a espera; não valida morte natural, dano ou disputa de itens. A tentativa adicional de banda limitada foi bloqueada pela reserva de RAM e não foi tratada como teste executado.

## Limites e preservação

As capturas são headless; não certificam apresentação GPU, percepção humana de fluidez ou latência universal. Houve hitches isolados nos clientes, inclusive um de 1.348 ms predominantemente na fase de espera/scheduling; foram preservados, sem removê-los da evidência. O cenário original com um jogador foi observado nos logs de produção; a validação nova foi com dois peers para cobrir owner/observador. Boss Rush, host e novas mortes naturais não tiveram smoke específico. Battle/COM não usa CaptureCoopWorld; seu baseline EditMode passou. A alteração de stripping afeta o build Server, não os builds offline.

Preservar shaders aumenta os dados do servidor: o build Development Linux reportou 330 MB, e o processo da espera ficou aproximadamente entre 356 e 370 MiB residentes. As capturas JSONL foram copiadas após quit/flush, decodificadas integralmente e não contêm eventos de limite/drop/overflow; contagens em `capture-summary.json`. Os resultados não são um benchmark A/B idêntico: há diferenças de plataforma, RNG e número de jogadores em relação à sala original.

Builds, tentativas incompletas, logs originais, capturas, XML e harnesses permanecem em `Build/ConnectionIdle/`; o teste remoto permanece em `/home/mauricio/sb6-tests/normal-idle-20261112-2045`, separado do serviço publicado. Antes de restaurar os efeitos do Editor em fontes/Physics2D, eles foram arquivados em `editor-side-effects.patch`; a migração de schema Physics2D é do Editor 6000.6 e os valores de gameplay não foram alterados nesta correção. Manifesto de limpeza: `cleanup-manifest.json`. Nenhum save de usuário, recovery ou cache de runner foi removido. A publicação pública continua pendente.
