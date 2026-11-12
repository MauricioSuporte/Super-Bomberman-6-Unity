# Orientações do projeto

## Offline e online: requisito permanente

Leia e siga [AGENTS.md](AGENTS.md) e [o contrato de validação offline/online](Docs/multiplayer/contrato-validacao-offline-online.md) antes de qualquer ajuste no jogo. Preserve os mesmos arquivos canônicos de plano, checklist, matriz e notas vinculados nesse contrato. Toda funcionalidade nova precisa funcionar offline e online; não entregar um caminho somente offline. Execute os gates Unity/Python e os checks de peers relevantes, registre limites reais e mantenha a validação pendente quando não puder executá-la. As skills em .codex/skills/super-bomberman-6-{unity,online,online-prediction} registram os workflows e invariantes aprendidos; consulte as relevantes.
