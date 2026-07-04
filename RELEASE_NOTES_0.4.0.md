# Community Challenge

Compete with other players and win prizes!  
👉 https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/blob/main/COMMUNITY_CHALLENGE.md

---

# Super Bomberman 6 - Unity (Demo Release 0.4.0)

The **0.4.0 demo** is one of the biggest updates so far for **Super Bomberman 6 recreated in Unity**, adding a complete Battle Mode experience, stronger COM/AI behavior, new progression systems, more polish, and several quality-of-life improvements.

This project is in **active development** and keeps expanding the classic Hudson Soft-inspired Bomberman formula with new systems, local multiplayer chaos, and modern usability improvements.

---

## New in Demo 0.4.0

### Battle Mode

- Added **Battle Mode** as a new game mode from the title screen
- Increased local multiplayer support up to **6 players**
- Added a complete Battle Mode menu flow:
  - Skin selection
  - Team selection
  - Rule configuration
  - Stage selection
  - Music selection
  - Item selection
  - Louie selection
  - Handicap selection
  - Battle start options
- Added **15 Battle Mode stages**, each with unique layouts and mechanics
- Added Battle Mode HUD with timer, win counters, team support, player status, and power-up display
- Added match flow screens for round win, draw, time up, and match victory
- Added pause menu support during Battle Mode
- Added skip support for Battle Mode result screens
- Added Battle Mode music variations and random music selection

---

### Battle Mode Mechanics

- Added **Sudden Death** with falling tiles, warning shadows, accelerated music, and bomb destruction on tile drop
- Added **Revenge Bomber** with Mad Bomber cart, bomb launcher, movement, animations, and recharge indicator
- Added Battle Mode support for:
  - Magnet Bomb
  - Springs
  - Conveyors
  - Minecarts
  - Portals
  - Power zones
  - Dynamite / magnet mechanics
  - Rope impulse mechanics
  - Scorched ground
  - Tile bounce and bomb bounce behavior
- Added item drops when players are defeated
- Added support for team-based Battle Mode rules
- Added configurable round timer
- Improved win conditions and draw/time-up handling

---

### COM / AI Improvements

- Added COM players to the normal game
- Added a complete Battle Mode COM system with difficulty-based behavior
- COM players can now use several advanced abilities, including:
  - Bomb Kick
  - Bomb Punch
  - Power Glove
  - Control Bomb
  - Yellow Louie kick
  - Pink Louie jump
  - Green Louie dash
  - Black Louie push
  - Red Louie stun
  - Purple Louie ability
  - Tank shots
  - Mole drill escape
  - Magnet Bomb awareness
  - Rubber Bomb awareness
  - Pierce Bomb awareness
  - Bomb Pass / Destructible Pass awareness
  - Stage-specific Battle Mode mechanics
- Improved COM escape logic, item collection, offensive bomb placement, farming behavior, and sudden-death awareness
- Adjusted COM behavior by difficulty, including easier AI on Easy and more aggressive behavior on Hard
- Fixed multiple COM freezes, self-kill scenarios, movement issues, and Battle Mode edge cases

---

### Campaign, Progression & Difficulty

- Added **life system** to the normal game
- Added **Game Over** screen with continue animation
- Added **1-Up item**
- Added difficulty selection to normal game saves
- Added harder enemy behavior in Hard and Hardcore difficulties
- Added Hardcore rules where progress is deleted on death
- Added achievements system and achievements menu
- Added unlock achievements for stages and Hardcore difficulty
- Added credits at the end of the normal game
- Improved world map stage icons based on difficulty

---

### Language, UI & Accessibility

- Added full support for **4 languages**
- Added translations to the controls menu
- Added sound options to the title screen
- Added demo version display on the title screen
- Added options for Terms of Service and Privacy Policy
- Added the ability to hide touch buttons on mobile
- Improved Android control rounding and touch-control behavior
- Improved multiple menus, including Battle Mode, pause menu, achievements, controls, save file, and player selection
- Improved cursor behavior, responsiveness, visual effects, and menu flow across several screens

---

### Gameplay Polish & Balance

- Added Skull item with movement, animation, collision transfer, bounce behavior, and multiple effects
- Added stun animation and stun sound effects for players and mounts
- Added Pierce Bomb blue/pink explosion visual adjustments
- Added new hard-mode attacks and behavior for Clown Mask Boss and Sun Mask Boss
- Improved Sun Mask Boss behavior and animation timing
- Improved player walk animation with additional frames and speed-based animation scaling
- Improved Louie movement sprites and ability consistency
- Improved Pink Louie jump, Yellow Louie kick, Green Louie ability, Black Louie behavior, and Mole drill timing
- Improved bomb chain explosions, bomb bounce, bomb kick, bomb punch, power glove, rubber bomb, and magnet bomb interactions
- Improved mount/dismount damage immunity and fixed several mount-related softlocks
- Improved item destruction animations and item movement visuals
- Improved stage labels, stage names, shadows, overlays, and end-stage flow

---

### Audio, Performance & Technical

- Added Discord Rich Presence integration
- Added new game icon and executable icon
- Updated the Unity project version to **600.4.5f1**
- Added looped music across stages, menus, world map, Boss Rush, and Battle Mode
- Added and adjusted several SFX for menus, abilities, explosions, stun, item loss, unlocks, and round results
- Improved memory usage
- Fixed FPS drops in 6-player gameplay
- Fixed music restart issues when the game loses focus
- Removed multiple logs and warnings across the project
- Added Android build files and updated mobile build support

---

### Fixes

- Fixed controls not being recognized by the game in some cases
- Fixed several Battle Mode menu, cursor, HUD, tag match, and result-screen issues
- Fixed Revenge Bomber freezes and movement issues
- Fixed Battle Mode player transparency and HUD status issues
- Fixed multiple softlocks involving mounts, minecarts, springs, portals, Pink Louie, Black Louie, and Yellow Louie
- Fixed crashes and edge cases involving Yellow Louie, Rubber Bombs, kicked bombs, and redirected bombs
- Fixed Discord freeze issue
- Fixed stage and boss edge cases, including Sun Mask Boss and Stage 13 softlock behavior
- Fixed item, bomb, skull, and mount collision/visual issues
- Fixed Android/mobile-specific warnings and control behavior
- Fixed many smaller gameplay, UI, audio, and performance issues

---

## About This Demo (0.4.0)

### Gameplay & Modes

- Up to **6 local players**
- **Battle Mode** with multiple rules, teams, stages, items, Louies, music, and handicap options
- **Boss Rush Mode** with up to 4 players
- **4 difficulty levels** in Boss Rush
- **2 complete worlds** inspired by *Super Bomberman 1 & 2*
- Campaign system with **3 save slots**
- Difficulty selection for normal game saves
- Life system, Game Over flow, achievements, and up to **200% completion**

---

### Characters & Progression

- **8 selectable Bombers + 12 unlockable**
- Progression-based unlock system
- Achievements and unlockable stage progression
- Full controller support
- Support for COM players
- Option to reset full game progress

---

### Mount System (Louies & More)

- All **7 mounts from Super Bomberman 5**, including **Warooi**
- Tank and Mole support in Battle Mode
- Rebalanced abilities for better gameplay
- Mount system includes:
  - Egg queue system
  - Mount / dismount mechanics
  - Damage immunity during mount and dismount transitions
- Functional abilities inspired by *Super Bomberman 2* and expanded for Battle Mode

---

### Items & Power-ups

- More than **20 power-up types**
- Magnet Bomb, Skull item, 1-Up item, Power Glove, Pierce Bomb, Rubber Bomb, Control Bomb, and more
- Expanded item logic for Battle Mode, player defeat drops, and COM behavior

---

### Technical Features

- Pixel-perfect rendering pipeline
- Configurable resolution settings
- Sound options
- Discord Rich Presence
- **4 language support**
- **Android version available**
- Touch-button visibility option on mobile

---

## Installation & How to Run

### Windows Version (.rar)

1. Download the `SuperBomberman6.rar` file
2. Extract it anywhere
3. Run **SuperBomberman6.exe**

---

### Android Version (.rar / APK)

1. Download the `SuperBomberman6Android.rar` file
2. Extract the `.apk`
3. Transfer it to your device if needed
4. Enable **Install from unknown sources**
5. Install and open the APK

---

## Open Source - Join the Project!

This project is **open source**, and contributions are welcome:

- Star the repository
- Report bugs
- Suggest improvements
- Submit pull requests

---

### We especially need:

- **Pixel artists / spriters**
- UI improvements
- Animations and effects
- Battle Mode feedback and balancing suggestions

---

Have fun!

---

# Super Bomberman 6 - Unity (Demo Release 0.4.0)

A demo **0.4.0** é uma das maiores atualizações até agora de **Super Bomberman 6 recriado na Unity**, trazendo uma experiência completa de Battle Mode, melhorias fortes na IA/COM, novos sistemas de progressão, mais polimento e várias melhorias de qualidade de vida.

O projeto segue em **desenvolvimento ativo**, expandindo a fórmula clássica inspirada nos Bomberman da Hudson Soft com novos modos, multiplayer local e melhorias modernas de usabilidade.

---

## Novidades da Demo 0.4.0

### Battle Mode

- Adicionado o **Battle Mode** como novo modo de jogo na tela inicial
- Suporte local aumentado para até **6 jogadores**
- Novo fluxo completo de menus para Battle Mode:
  - Seleção de skins
  - Seleção de times
  - Configuração de regras
  - Seleção de estágios
  - Seleção de músicas
  - Seleção de itens
  - Seleção de Louies
  - Seleção de handicap
  - Opções de início da batalha
- Adicionados **15 estágios de Battle Mode**, cada um com layouts e mecânicas próprias
- Adicionado HUD de Battle Mode com timer, placar de vitórias, suporte a times, status dos jogadores e exibição de power-ups
- Adicionadas telas de vitória da rodada, empate, tempo esgotado e vitória da partida
- Adicionado menu de pausa no Battle Mode
- Adicionado suporte para pular telas de resultado
- Adicionadas variações de músicas e seleção aleatória de músicas no Battle Mode

---

### Mecânicas do Battle Mode

- Adicionado **Sudden Death** com queda de blocos, sombras de aviso, música acelerada e destruição de bombas na queda dos blocos
- Adicionado **Revenge Bomber** com carrinho Mad Bomber, lançador de bombas, movimentação, animações e indicador de recarga
- Adicionado suporte no Battle Mode para:
  - Magnet Bomb
  - Molas
  - Esteiras
  - Carrinhos
  - Portais
  - Zonas de poder
  - Dinamite e mecânicas magnéticas
  - Impulso por corda
  - Chão queimado
  - Quicar jogadores e bombas
- Itens agora podem aparecer quando jogadores são derrotados
- Adicionado suporte a regras por times
- Adicionado timer configurável por rodada
- Melhoradas as condições de vitória, empate e tempo esgotado

---

### Melhorias de COM / IA

- Adicionados jogadores COM ao jogo normal
- Adicionado sistema completo de COM para Battle Mode, com comportamento baseado em dificuldade
- COMs agora conseguem usar várias habilidades avançadas, incluindo:
  - Chutar bombas
  - Socar bombas
  - Power Glove
  - Control Bomb
  - Chute do Yellow Louie
  - Pulo do Pink Louie
  - Dash do Green Louie
  - Empurrão do Black Louie
  - Stun do Red Louie
  - Habilidade do Purple Louie
  - Tiros do Tank
  - Escapada com Mole Drill
  - Reconhecimento de Magnet Bomb
  - Reconhecimento de Rubber Bomb
  - Reconhecimento de Pierce Bomb
  - Reconhecimento de Bomb Pass e Destructible Pass
  - Mecânicas específicas dos estágios de Battle Mode
- Melhorada a lógica de fuga, coleta de itens, posicionamento ofensivo de bombas, farm e reação ao Sudden Death
- Ajustado o comportamento por dificuldade, com IA mais fácil no Easy e mais agressiva no Hard
- Corrigidos vários congelamentos, suicídios acidentais, problemas de movimentação e casos extremos da IA no Battle Mode

---

### Campanha, Progressão & Dificuldade

- Adicionado **sistema de vidas** ao jogo normal
- Adicionada tela de **Game Over** com animação de continue
- Adicionado item **1-Up**
- Adicionada seleção de dificuldade nos saves do jogo normal
- Inimigos ficam mais fortes e rápidos nas dificuldades Hard e Hardcore
- Adicionadas regras de Hardcore, com exclusão do progresso ao morrer
- Adicionado sistema de conquistas e menu de conquistas
- Adicionadas conquistas para desbloqueio de estágios e dificuldade Hardcore
- Adicionados créditos ao final do jogo normal
- Melhorados os ícones dos estágios no mapa conforme a dificuldade

---

### Idiomas, UI & Acessibilidade

- Adicionado suporte completo a **4 idiomas**
- Adicionadas traduções ao menu de controles
- Adicionadas opções de som na tela inicial
- Adicionada exibição da versão da demo na tela inicial
- Adicionadas opções para Termos de Serviço e Política de Privacidade
- Adicionada opção para esconder botões de toque na versão mobile
- Melhorados os controles Android e o comportamento dos botões de toque
- Melhorados vários menus, incluindo Battle Mode, pausa, conquistas, controles, save file e seleção de jogadores
- Melhorados cursores, responsividade, efeitos visuais e fluxo de navegação em várias telas

---

### Polimento de Gameplay & Balanceamento

- Adicionado item Skull com movimento, animação, transferência por colisão, comportamento ao quicar e múltiplos efeitos
- Adicionadas animações e sons de stun para jogadores e montarias
- Ajustados os visuais das explosões da Pierce Bomb
- Adicionados novos ataques e comportamentos em dificuldade Hard para Clown Mask Boss e Sun Mask Boss
- Melhorado o comportamento e o tempo das animações do Sun Mask Boss
- Melhorada a animação de caminhada dos jogadores, com mais frames e escala baseada na velocidade
- Melhorados sprites e consistência das habilidades dos Louies
- Melhorados Pink Louie, Yellow Louie, Green Louie, Black Louie e Mole
- Melhoradas interações de explosão em cadeia, quique de bomba, chute, soco, Power Glove, Rubber Bomb e Magnet Bomb
- Melhorada a imunidade durante montar/desmontar e corrigidos vários softlocks envolvendo montarias
- Melhoradas animações de destruição de itens e movimentação visual de itens
- Melhorados labels de estágios, nomes, sombras, overlays e fluxo de fim de fase

---

### Áudio, Performance & Técnico

- Adicionada integração com **Discord Rich Presence**
- Adicionados novo ícone do jogo e novo ícone do executável
- Projeto atualizado para **Unity 600.4.5f1**
- Adicionadas músicas em loop em estágios, menus, mapa, Boss Rush e Battle Mode
- Adicionados e ajustados vários efeitos sonoros de menus, habilidades, explosões, stun, perda de item, desbloqueios e resultados de rodada
- Melhorado o uso de memória
- Corrigidas quedas de FPS em partidas com 6 jogadores
- Corrigidos problemas de reinício de música quando o jogo perde foco
- Removidos vários logs e warnings do projeto
- Adicionados arquivos de build Android e melhorias no suporte mobile

---

### Correções

- Corrigidos casos em que os controles não eram reconhecidos pelo jogo
- Corrigidos vários problemas de menus, cursores, HUD, Tag Match e telas de resultado do Battle Mode
- Corrigidos congelamentos e problemas de movimentação do Revenge Bomber
- Corrigidos problemas de transparência de jogadores e status no HUD do Battle Mode
- Corrigidos vários softlocks envolvendo montarias, carrinhos, molas, portais, Pink Louie, Black Louie e Yellow Louie
- Corrigidos crashes e casos extremos envolvendo Yellow Louie, Rubber Bombs, bombas chutadas e bombas redirecionadas
- Corrigido problema de travamento relacionado ao Discord
- Corrigidos casos específicos em estágios e chefes, incluindo Sun Mask Boss e softlock no Stage 13
- Corrigidos problemas de colisão e visual em itens, bombas, Skull e montarias
- Corrigidos warnings e comportamentos específicos da versão Android/mobile
- Várias correções menores de gameplay, UI, áudio e performance

---

## Sobre a Demo (0.4.0)

### Jogabilidade & Modos

- Até **6 jogadores locais**
- **Battle Mode** com regras, times, estágios, itens, Louies, músicas e handicap configuráveis
- **Modo Boss Rush** com até 4 jogadores
- **4 níveis de dificuldade** no Boss Rush
- **2 mundos completos** inspirados em *Super Bomberman 1 e 2*
- Sistema de campanha com **3 slots de save**
- Seleção de dificuldade no jogo normal
- Sistema de vidas, Game Over, conquistas e até **200% de conclusão**

---

### Personagens & Progressão

- **8 Bombers jogáveis + 12 desbloqueáveis**
- Sistema de progressão por desbloqueio
- Conquistas e progressão de estágios desbloqueáveis
- Suporte completo a controles
- Suporte a jogadores COM
- Opção de resetar o progresso do jogo

---

### Sistema de Montarias (Louies e mais)

- Todos os **7 Louies do Super Bomberman 5**, incluindo **Warooi**
- Suporte a Tank e Mole no Battle Mode
- Habilidades reequilibradas
- Sistema completo com:
  - Fila de ovos
  - Mecânica de montar / desmontar
  - Imunidade durante transições de montar e desmontar
- Habilidades inspiradas em *Super Bomberman 2* e expandidas para o Battle Mode

---

### Itens & Power-ups

- Mais de **20 tipos de power-ups**
- Magnet Bomb, Skull, 1-Up, Power Glove, Pierce Bomb, Rubber Bomb, Control Bomb e mais
- Lógica expandida de itens para Battle Mode, drops ao derrotar jogadores e comportamento dos COMs

---

### Recursos Técnicos

- Pipeline pixel-perfect
- Configuração de resolução
- Opções de som
- Discord Rich Presence
- Suporte a **4 idiomas**
- **Versão Android disponível**
- Opção para esconder botões de toque no mobile

---

## Instalação e Execução

### Versão Windows (.rar)

1. Baixe o arquivo `SuperBomberman6.rar`
2. Extraia em qualquer pasta
3. Execute **SuperBomberman6.exe**

---

### Versão Android (.rar / APK)

1. Baixe o arquivo `SuperBomberman6Android.rar`
2. Extraia o `.apk`
3. Transfira para o celular, se necessário
4. Ative **Fontes desconhecidas**
5. Instale e abra o APK

---

## Open Source - Contribua!

Este projeto é **open source**, e contribuições são bem-vindas:

- Dê uma estrela no repositório
- Reporte bugs
- Sugira melhorias
- Envie pull requests

---

### Precisamos especialmente de:

- **Pixel artists / spriters**
- Melhorias de UI
- Animações e efeitos
- Feedback e sugestões de balanceamento para o Battle Mode

---

Divirta-se!
