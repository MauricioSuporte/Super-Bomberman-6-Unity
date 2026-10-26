# Matriz de validação manual da predição online

Esta matriz complementa o [checklist de desenvolvimento](progresso-predicao-online.md)
e o [plano completo](plano-aplicacao-predicao-online.md). Ela define a validação
manual ainda necessária; **nenhum cenário desta matriz foi executado entre dois
peers nesta retomada**. Criar o roteiro não conclui um ID nem comprova ganho.

## Preparação e evidência

1. Registrar build e protocolo do offline, servidor e clients. Confirmar a
   publicação no checkpoint/pipeline; commit enviado não comprova deploy.
2. Repetir mapa, loadout, direção e sequência de ações no offline e no online.
   Usar host, owner e observador; inverter os jogadores e repetir com dois
   clients humanos em servidor dedicado quando disponível. COM é caso adicional.
3. Cobrir início, continuação e término; depois pausa/retomada, morte, troca de
   posse/montaria, desconexão e nova rodada/cena conforme o mecanismo permitir.
4. Capturar JSONL com o diagnóstico existente nos peers envolvidos. Guardar os
   cabeçalhos, protocolo/build, cenário, papel e condições de rede. Para rede
   degradada, registrar a configuração de um ambiente de teste controlado, se
   disponível; não alterar o transporte da homologação para simular perda.
5. Correlacionar tick da ação, netId e revisão quando presentes. Medir durações
   dentro do mesmo relógio; não subtrair UTC de máquinas distintas como medida
   de input lag. Diferenciar espera de confirmação, correção visual e frame hitch.

Registrar cada execução com ID, builds, cenário, papéis, entrada, resultado,
arquivos de evidência e falha observada. Uma evidência que falta continua pendente.
Testes EditMode e compilação não substituem esta matriz.

O pacote Windows inclui `Start-Online-Diagnostics.cmd`; o runtime também aceita
`-gameplay-latency-logs` ou `-online-motion-logs`. JSONL fica em
`Application.persistentDataPath/Diagnostics/Movement`. A captura para após10 minutos
ou8MiB e tem orçamento de180 eventos/segundo; conferir `capture_limit`/`dropped`
antes de interpretar uma ausência como falha de gameplay. Para um teste novo,
iniciar uma captura nova com o mecanismo existente de ativação/reinício do jogo.

O comparador existente aceita mais de um arquivo online, inclusive o servidor;
mantém separados build, protocolo, papel, player e captura:

```powershell
python Tools/Diagnostics/analyze_prediction.py --online client.jsonl server.jsonl --offline offline.jsonl --output comparacao.json
```

Os nomes acima são exemplos. O resumo conta os eventos novos de SuddenDeath;
ele não calcula automaticamente latência dessa habilidade nem substitui a
correlação de células/hash e durações descrita abaixo.

## Cenários por ID

Todos os critérios abaixo aguardam comparação manual. IDs parcialmente
implementados também têm lacunas de código listadas no checkpoint; o roteiro
não autoriza considerar essas lacunas resolvidas.

| ID | Cenário mínimo | Critério observável |
| --- | --- | --- |
| P0 | Capturas offline/online comparáveis; início/fim de ação e pausa | Eventos correlacionáveis, esperas intencionais separadas de travamentos e nenhuma latência inferida de ação não instrumentada. |
| A01 | Caminhar montado em quatro direções, curvas e paredes | Resposta local imediata onde elegível; replay conserva contexto e observador acompanha a montaria. |
| A02 | Plantar, esgotar capacidade, ocupar célula e rejeitar pedido | Proxy confirma ou desaparece sem bomba duplicada; estoque e bloqueio virtual são liberados. |
| A03 | Green dash livre, obstáculo próximo, pausa e interrupção | Gesto/trajeto contínuos; confirmação não reinicia dash, rejeição restaura estado. |
| A04 | Pink jump com sombra, pouso, morte e troca de mount | Chão, altura e sombra permanecem alinhados; client não ganha proteção local. |
| A05 | Black dash em parede, contato e hold de impacto | Não atravessa parede por suavização; push/stun ficam confirmados pelo servidor. |
| A06 | Red punch acertando e errando | Gesto começa localmente; som de acerto e consequências só após confirmação, sem áudio duplicado. |
| A07 | Preparação Mole, transição e cancelamento | Preparação não espera nova ida/volta; destino aleatório e proteção não são escolhidos no client. |
| A08 | Tank nas quatro direções, cooldown e alvo próximo/móvel | Proxy transfere para projétil real sem salto; pausa, impacto e rejeição não duplicam tiro/dano. |
| A09 | Line/Purple, estoque parcial e variantes de bomba | Cada célula confirma separadamente; comando não duplica som/estoque nem usa tick de outra ação. |
| A10 | ControlBomb com seleção e detonação, inclusive bomba ausente | Feedback responde ao comando correto; servidor decide detonação e não há ativação por replay. |
| A11 | Montar/desmontar, disputar pickup, fila de ovos e trocar posse | Arcos e seguidores mantêm fase, visibilidade e sorting; posse/ovo não são concedidos por previsão. |
| A12 | Revenge: entrada permitida, mira, movimento, lançamento e retorno | Validar primeiro transporte de input e elegibilidade; apresentação não reativa player eliminado. |
| A13 | Alterar velocidade/debuff/pass com inputs ainda pendentes | Replay usa contexto histórico confirmado; rejeição não apaga intenção direcional real. |
| A14 | Kick, punch, glove, Yellow/Blue, Search/Magnet, Rubber e bounce | Início contínuo, sem A duplicado, término ordenado e rollback completo em rejeição/interrupção. |
| C01 | Esteira reta/curva, mudança de fase e saída bloqueada | Força aplicada uma vez por tick; replay não altera fase global nem empurra duas vezes. |
| C02 | Canhão, mola e geyser com preparação, voo e pouso | Owner não salta entre reconciliações; curvas, efeitos e consequências permanecem aprovados. |
| C03 | Corda com ocupação concorrente e voo interrompido | Preparação/voo contínuos; deformação ambiental corresponde ao ator transportado. |
| C04 | Carrinho nas arenas 9/12, curva, embarque/saída e portal | Passageiro, cart, escala, estrelas e som se alinham; curva não corta diagonal e ocupação não é inventada. |
| C05 | Portal7/World3 com bomba carregada, máscara e saída ocupada | Teleporte tem corte explícito; escala/máscara/estrelas limpam em cancelamento e não deixam bomba duplicada visível. |
| C06 | Correnteza com freio/barreira de saída, plataforma multi-rider e barco | Suporte e rider usam a mesma timeline; saída/replay respeitam barreira e barco não concede pass local sem contexto. |
| C07 | Pusher enquanto o player também caminha, parede e recuperação | Compor força e input sem aplicar duas vezes; bounce posterior não apaga ou reinicia o empurrão. |
| C08 | Campo magnético, explosão que muda direção e redirecionamento | Client não sorteia alvo nem inicia simulação nativa; sequência aprovada não perde continuidade. |
| C09 | Caldeira, swap/blackout, dinamite, foguetes e tiles de força | Fase visual corresponde ao servidor; nenhuma edição, explosão ou recompensa nasce de hipótese local. |
| C10 | FallingBomb, teto aberto/transições, pólvora e Sudden Death | Aviso/alvo/fase aprovados; sem relógio ou ignição client independentes, sem decisão local de dano/RNG e sem teto fora de fase. |
| R01 | Dois players remotos, troca de mount, cabeça/pose e pausa | Transform e pose compartilham tempo; histórico não cruza gerações de montaria. |
| R02 | Co-op com paredes, props móveis e várias famílias de sprites | Geometria de consulta aprovada pronta antes da previsão; medir custo/atraso por família e preservar forma das curvas. |
| R03 | Frog/hop/snowman/korobokkuru: caminhada, salto e avanço | Trajeto aprovado contínuo sem prever alvo, pathfinding ou morte. |
| R04 | Hurricane, Magnet e bolha: captura/puxão/liberação com input | Força compõe com o motor quando caminhar é permitido; captura/dano/libertação são autoritativos. |
| R05 | Spear, rocket, ice, star e heart: spawn, curva/homing e impacto | Só percurso aprovado é apresentado; impacto encerra explicitamente sem collider de dano client. |
| R06 | SunMask, ClownMask, FreezerVenus e Mecha: telegraph/ataques | Pose, trajetória e som se alinham; client não anuncia área segura por adivinhar ataque. |
| R07 | Bomba em voo com owner inativo/destruído; itens, ovos e transferências | Motion confirmado continua, previsão cancela; VFX recebido sobrevive ao owner e cleanup devolve estado. Eventos sem owner são caso separado. |
| R08 | Barris, snowballs, bubbles e cenário móvel de World3 | Movimento visual não promete geometria diferente da aprovada; saída de cena limpa objetos e esperas. |

## Verificações específicas dos últimos lotes

- **Owner da bomba:** confirmar arco já recebido, desativar/destruir owner,
  interromper o client e voltar à cena. Verificar continuidade, collider, stop
  ordenado e ausência de nova habilidade local. Receber evento antes de destruir
  o owner é diferente de tentar enviá-lo depois que seu endpoint já desapareceu.
- **Teto:** entrar durante cada fase, pausar numa transição e acompanhar ambos
  os peers. Incluir teto aberto com tile nulo e uma nova rodada. Nenhum timer
  client deve escolher a próxima fase.
- **Pólvora:** uma ignição, nova ignição durante a anterior, término, pausa e
  desativação. Piso/matrizes devem voltar ao original; client não cria hitboxes
  nem inicia o estado nativo IgnitionRunning.
- **Sudden Death:** comparar `sudden_death_begin` (`path_count`, `path_hash` e
  `music_wait`) e `sudden_death_drop_begin/finished` por célula e papel. Esses
  eventos ajudam a investigar a timeline; não confirmam por si o commit de
  terreno nem encerram a auditoria das quedas.

## Critério de fechamento

Uma execução aprovada precisa de evidência dos papéis e interrupções relevantes,
comportamento offline preservado e ausência de dano/posse/terreno especulativos.
Se houver falha, registrar o ID e o evento de início/fim/correção, corrigir a
causa e repetir o cenário afetado. Atualizar o checkpoint com o resultado real;
não marcar todos os cenários como aprovados por uma única partida sem sintomas.
