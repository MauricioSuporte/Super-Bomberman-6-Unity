# Fechamento da implementação de predição online

P0 concluído tecnicamente em 11/11/2026. Checklist **33/33 IDs concluídos**, com consolidação das notas e da skill. Os aceites humanos e as limitações continuam restritos aos cenários registrados. Nenhum commit, push ou publicação nesta execução.

As fontes canônicas continuam sendo o [plano por ID](plano-aplicacao-predicao-online.md), o [checklist/checkpoint](progresso-predicao-online.md), a [matriz](matriz-validacao-predicao-online.md) e as [notas de implementação](prediction-and-reconciliation.md). Este relatório as complementa.

## O que P0 entregou

- Reprodução opt-in Development em Stage_1-1: mesma programação de inputs horizontais, loadout inicial e Blue Louie, com pausa nativa, offline e dois clientes reais. Saves isolados e terreno preparado pela autoridade sem pickups. Velocidade observada de 3,5 nos modos comparados.
- Polling cedo e observação do root no final da apresentação CPU (ordem 31999), preservando a captura global 32000. Tick original de envio/recebimento mantido até resposta/cancelamento; origem desconhecida permanece explícita.
- Observações substituídas resolvidas só após a apresentação: movimento de FixedUpdate no frame da soltura não vira falso cancelamento. Fila limitada a 32; overflow invalida completude. Nenhuma alteração de simulação ou de protocolo 70.
- Analisador incremental com qualidade da captura, contextos/papéis separados, bloqueios na entrada/resposta, cancelamentos, mediana/p95/máximo e hitches preservados. Nenhuma latência inferida de habilidade sem instrumento.
- Skill e notas consolidadas;19 relatórios históricos conferidos por UTF-8/JSON/hash, sem misturar suas builds com os dados finais.

## Build e validação

Candidata06, Unity 6000.6.0f1/Windows Development, protocolo 70. Mesmo binário no offline, servidor e clientes. SHA-256 da Assembly-CSharp.dll: `fd4f299a7edf4fb343f8452d2a6677f8021239174f2a9d2ac837a420ead8abc7`.

- **1109/1109 Unity** (inclui três regressões da observação diferida); **216/216 Python**; quick_validate da skill aprovado.
- Sete arquivos C# alterados com UTF8 estrito; runtime corresponde ao manifesto do build; todos os scripts e Packages conferem com o runner antes da restauração. Nenhum erro/aviso C# nos logs finais.
- Capturas finais completas: um offline, três arquivos no online normal e três no degradado; nenhum dropped/capture_limit/overflow. Mais de60 respostas livres por player/contexto; p95 exige pelo menos20.
- **497/497 respostas correlacionadas**:248 no normal e249 no degradado, sem origem desconhecida ou pares ausentes. Recebimento não demonstra aplicação sem descarte/replay de cada tick.
- Pausa observada no offline e nos owners dos dois cenários online: delta0 nas amostras pausadas. Duas respostas iniciadas durante pausa ficaram fora da distribuição livre.
- Runner:80 entradas de fonte/metas conferidas com o estado original. Arquivos novos do overlay removidos; Library/Build/projetos gerados não foram revertidos. Build imutável preservada em `C:/Users/mauri/SB6-P0/candidate06`.

## Comparação final de resposta

Intervalo em milissegundos entre input aceito e deslocamento observado no root final, dentro do relógio local. É uma medição CPU quantizada pelo loop/frame, sem medir teclado físico, pixel/GPU ou percepção. A sequência é comparável por cena/loadout/programação, sem replay bit a bit de RNG ou de ticks. Janelas pré-definidas pelos marcadores, com margens de 2s, afastam preparação/transições/recarga. UTC da mesma máquina seleciona janelas; não é subtraído para calcular latência entre peers.

| Condição | Player | Contexto | N livre | Mediana ms | p95 ms | Máximo ms |
| --- | --- | --- | --- | --- | --- | --- |
| Offline | P1 | A pé | 63 | 17.27 | 17.53 | 17.80 |
| Offline | P2 | A pé | 62 | 17.25 | 17.56 | 17.95 |
| Offline | P2 | Blue Louie | 61 | 17.44 | 17.84 | 18.08 |
| Offline | P1 | Blue Louie | 61 | 17.44 | 17.77 | 18.12 |
| Online local | P1 | A pé | 61 | 17.51 | 18.49 | 340.16 |
| Online local | P1 | Blue Louie | 61 | 17.77 | 34.46 | 34.74 |
| Online local | P2 | A pé | 61 | 34.25 | 34.61 | 34.90 |
| Online local | P2 | Blue Louie | 63 | 17.98 | 18.87 | 135.24 |
| Online degradado | P1 | A pé | 62 | 19.19 | 34.36 | 34.90 |
| Online degradado | P1 | Blue Louie | 63 | 17.74 | 18.02 | 20.35 |
| Online degradado | P2 | A pé | 61 | 17.56 | 17.92 | 18.10 |
| Online degradado | P2 | Blue Louie | 63 | 17.85 | 18.67 | 19.56 |

No online normal, as medianas de RTT por grupo foram16,67–16,86ms. No degradado,129,75–134,50ms; p95 de resposta livre permaneceu17,92–34,36ms. A resposta não acompanhou o aumento do RTT nesses cenários; não há promessa literal de 0ms. O offline teve p95 de17,53–17,84ms; diferenças de até cerca de um frame permanecem explícitas.

O relay foi exclusivo de loopback:50ms por direção, jitter configurado±5ms, probabilidade de perda1%, seed70. Foram130538 datagramas encaminhados e1296 descartados pelo sorteio;33 notificações Windows de porta fechada no encerramento foram contadas, sem erro do relay. Isso não é um teste WAN ou uma garantia de perda/atraso real idêntico em outra máquina.

## Esperas, hitches e reconciliação

Os máximos livres de340,16ms (P1 a pé) e135,24ms (P2 montado), no normal, foram preservados. As respectivas janelas de FrameStats tiveram trabalho máximo de307,74ms e119,22ms. É associação em janela de1s, não atribuição exata a GC/rede nem prova da causa do hitch. FrameMs no evento pode ainda refletir o início do frame; seu contador sozinho não mede uma pausa ocorrida dentro desse frame. O relatório mantém o máximo e os registros longos, além do p95.

As respostas de183,96ms e134,29ms iniciadas com prediction_block=paused foram classificadas como espera intencional e excluídas da distribuição livre. Duas tentativas sem deslocamento, uma offline e uma online, terminaram nas bordas com vizinho indestrutível na direção pedida; permanecem listadas, sem virar latência zero ou prova de input perdido. Bloqueio de controle ausente não significa geometria livre.

Reconciliações foram registradas separadamente do deslocamento apresentado. O delta interno antes/depois do replay é limitado por amostragem e não comprova um snap visível. As amostras de root10Hz não constituem uma trajetória completa nem demonstram ausência universal de travamentos.

## Artefatos para auditoria

- `Logs/P0-final-summary.json`: dados, identidade e limites finais.
- `Logs/P0-candidate06/identity.json`, `tests.xml`, `build.log`; `Logs/P0-python-final-error.log`.
- `Logs/P0-comparacao-candidate06.json` e `P0-comparacao-candidate06-online-delay.json`.
- `Logs/P0-correlation-candidate06-online.json` e `P0-correlation-candidate06-online-delay.json`.
- `Logs/P0-motion-audit-candidate06-{offline,online,online-delay}.json`; `P0-boundary-cancellations-candidate06.json`.
- `Logs/P0-{offline,online,online-delay}-candidate06`: logs/capturas brutas, configuração e saves isolados; `network.json` no degradado.
- `Logs/P0-source-comparison.json`, `P0-source-encoding.json`, `P0-runner-restore-check.json`, `P0-evidence-audit.json` e `P0-skill-validation.txt`.

Custos e comparações anteriores continuam em R01-comparacao/R01-custo; R02c-cost/repro; R03-viewcache2-reports/entrega; R05-candidate14; R06-candidate12/R06-comparacao; R08-candidate07/R08-comparacao. Caminhos exatos e hashes estão na auditoria e nos registros por ID do checkpoint. Não reatribuir esses custos à instrumentação nova ou tratá-los como uma baseline única de todo o projeto.

Uma execução final por condição não equivale a repetições independentes de processo. P0 não mede input físico/GPU, todas as combinações de habilidades, WAN, Android ou runtime Linux; os escopos de QA anteriores ficam preservados. Retenções instrumentadas do R08 continuam registradas e não são declaradas corrigidas por este teste. Controles02–05 e tentativas falhas permanecem no histórico, sem serem misturados à06. Implementação/checklist concluídos no escopo documentado; publicação é uma ação separada.
