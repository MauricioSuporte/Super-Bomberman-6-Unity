# Arquivo das evidências da predição — 11/11/2026

Os mesmos arquivos canônicos de plano/checklist/matriz/notas continuam versionados. Os artefatos antigos foram identificados para limpeza sem apagar implementação, testes ou conhecimento.

- Arquivo local: `C:/Users/mauri/SB6-Prediction-Archive/2026-11-11/evidence.zip`.
- SHA-256: `4ef513d8271b96145e8b5fcd0e74285ab3be63539b9f81bc731749e3a4ca4b74`.
- Tamanho comprimido: 0.85 GiB; 12112 arquivos de evidência/relatórios/scripts/prints preservados, com CRC verificado.
- Índice: `manifest.json` ao lado do ZIP, com caminho original → caminho no ZIP, SHA de cada arquivo retido e snapshots dos alvos.
- Prefixos internos: `project/Logs/...`, `project/Build/...`, `user/SB6-...` e `movement/motion-...`.
- Alvos de limpeza: 1370, incluindo 481 capturas referenciadas por logs de testes. Total identificado: 31.95 GiB.

**Exclusão automática não executada:** a política da ferramenta rejeitou o comando de remoção em lote com `blocked by policy`, sem motivo mais específico. Nenhum alvo foi apagado; os caminhos antigos continuam disponíveis por enquanto. O ZIP já foi criado, portanto ainda não há ganho de espaço. Depois da remoção, o ganho líquido estimado em relação ao estado anterior ao arquivo é de 31.10 GiB.

Há um script preparado para revisão/execução MANUAL em `C:/Users/mauri/SB6-Prediction-Archive/2026-11-11/cleanup-manual.ps1`. Ele verifica SHA do ZIP, escopo de cada caminho absoluto, processos ativos, arquivos versionados, links e snapshots de conteúdo/timestamp antes de remover qualquer alvo. Não foi executado pelo agente após a rejeição. Se encontrar alterações desde o arquivo, recusa a remoção para não sobrescrever trabalho novo.

Não são alvos: Assets/Tools/testes/skills/documentação versionados, saves e recovery, capturas originais enviadas pelo usuário de25/10, histórico/configurações globais Codex/Claude, MCP instalado nem cache ativo do runner. Binários antigos são regeneráveis e não foram incluídos no ZIP; ficam os manifestos/hashes e as evidências.

Quando a limpeza manual for executada, `cleanup-result.json` registra os bytes e os alvos removidos. Para consultar uma referência histórica que vier a ser removida de Logs, usar manifest.json e extrair apenas o arquivo necessário do ZIP. Um caminho arquivado não representa uma nova execução ou revalidação.
