# ConsoleMode - Gamepad Companion

App companion para substituir o Steam Input e usar o gamepad com o addon ConsoleMode-Vanilla.
Desenvolvido em C# (.NET Framework 4.8) para Windows, leve, 100% portátil e com inicialização direta na bandeja do sistema (system tray).

## ✨ Funcionalidades Principais
- **Mapeamento Couch Gaming de Baixa Latência:** Poll de entrada a ~60 FPS compatível com XInput e controllers Xbox/PlayStation.
- **Smart Mouse Look:** Integração automática de controle de câmera via analógico direito com alternância F9.
- **Sistema de Perfis de Controle por Jogo & Editor Estilo Steam Input:**
  - Perfis individuais persistidos no `games.ini` para cada jogo da biblioteca.
  - Presets prontos em 1 clique: *WoW Classic (Octowow)*, *RPG / MMO (Câmera Botão Direito)* e *Padrão PC (WASD + Mouse)*, além de clonagem de perfis entre jogos cadastrados.
  - Editor visual completo em GDI+ ocupando 100% da tela, com diagrama do controle cercado por cards interativos.
  - Calibração de Sensibilidade do Mouse, Zonas Mortas (Global ou Custom) e Limiar de Gatilhos LT/RT (Global ou Custom).
  - Alternância de modos dos analógicos (`4 Direções`, `Movimento do Mouse`, `Desativado`), com comandos adicionais e sincronização de cliques L3/R3.
  - Toggle de Input do Companion por Jogo no `GameConfigPanel` para jogos com suporte nativo a controle.
- **Captura Física de Input & Combos:**
  - Modo de escuta física com gancho de teclado de baixo nível (`WH_KEYBOARD_LL`) para sequestrar atalhos do Windows (`Win+R`, `Win+D`, `Alt+Tab`, `Win+Shift+S`, etc.) durante o mapeamento.
  - Acumulação simultânea de teclas, botões de mouse e scroll em tempo real.
- **Seletor Visual de Input em 3 Abas:**
  - **1. Teclado:** Layout ANSI completo com navegação espacial ergonômica (funções F1-F12, numérico, letras, modificadores e setas).
  - **2. Mouse:** Ilustração vetorial dinâmica do mouse com cards para cliques esquerdo, direito, meio e scroll up/down.
  - **3. Sistema & Especial:** Painel em 3 colunas para controles de áudio/mídia, atalhos de janelas/captura e funções do Companion (`Centralizar Cursor`, `Toggle Confinar Cursor`).
  - Navegação 100% por controle: seleção direta de tecla/botão com `(A)`, confirmação e salvamento com `[RT]`, montagem de combos com `(X)`, limpeza com `(Y)`, cancelamento com `(B)` e troca de abas com `[LB]` / `[RB]`.
- **Biblioteca de Jogos Integrada:**
  - Navegação 100% nativa por controle (D-Pad, analógicos, atalhos A/B/X/Y e gatilhos).
  - Pesquisa rápida de títulos em tempo real.
  - Integração com a API do **SteamGridDB** para download instantâneo de capas em alta resolução.
  - Geração automática de **Hero Backgrounds Widescreen** com desfoque atmosférico e vinheta suave.
  - Teclado Virtual integrado com prevenção de missclicks, suporte a controle e digitação direta por teclado físico.
  - Persistência portátil de configurações e jogos via `config.ini` e `games.ini`.
- **Visual Debugger:** Visualização gráfica em tempo real dos botões, analógicos com zonas mortas calibráveis e sensibilidade de gatilhos.

## 🗺️ Roadmap & Próximos Passos (TODO)
- [x] Mapeamento Gaming 100% idêntico ao layout `Octowow` do Steam Input (com Smart Mouse Look F9 e cliques L3/R3)
- [x] Visual Gamepad Debugger em tempo real com sliders de Deadzone e Gatilho
- [x] Persistência local em arquivos `.ini` e ciclo de vida em segundo plano na bandeja (System Tray)
- [x] Biblioteca de Games (Launcher com navegação 100% por gamepad, configuração de executáveis, SteamGridDB, hero backgrounds e teclado virtual)
- [x] Fase 1 dos Perfis: Motor de Perfis por Jogo, Limpeza da Barra Lateral e Toggle de Input do Companion por Jogo
- [x] Fase 2 dos Perfis: Tela do Editor Visual de Perfil (`GameProfileEditorControl`) com diagrama do controle e slots
- [x] Fase 3 dos Perfis: Tela de Escolha de Edição & Captura Física de Combos
- [x] Fase 4 dos Perfis: Seletor Visual em 3 Abas (`Teclado` | `Mouse` | `Sistema & Especial`)
- [ ] **Toggle de Múltiplas Instâncias por Jogo (`Permitir Múltiplas Instâncias`):**
  - Adicionar no `GameConfigPanel` um botão toggle idêntico ao do Input (`[ (●) PERMITIR MÚLTIPLAS INSTÂNCIAS / (○) INSTÂNCIA ÚNICA (Padrão) ]`).
  - Se estiver em **Instância Única (Desativado - Padrão)**: ao acionar `[A] INICIAR`, caso o executável principal já esteja em execução, em vez de abrir outro processo, o app traz a janela do jogo já existente para primeiro plano e oculta a interface do Companion.
  - Se estiver ativado: permite abrir múltiplas instâncias em paralelo.
- [ ] Sistema de Páginas / Camadas por Modificador (alternar entre páginas de mapeamento dentro do jogo, como Navegação vs. Interação/Menus, cada uma com sua própria sensibilidade de mouse e botões)
- [ ] Avaliar expansão de comandos especiais do sistema na Aba 3
- [ ] Overlay estilo Steam acionado pelo botão central do controle (Guide/Home)
- [ ] Perfil Desktop para usar o gamepad no Windows normalmente
- [ ] **Suporte Nativo a Steam Deck no Windows:**
  - Identificação de hardware Valve (Jupiter / Galileo).
  - Modal na área de conteúdo para instalação e checagem do driver virtual ViGEmBus.
  - Driver de hardware HID para captura nativa, desativação de Lizard Mode e suporte aos botões traseiros (L4, L5, R4, R5) e trackpads.

## 🙏 Créditos & Agradecimentos
- **[ayufan / Steam Deck Tools](https://github.com/ayufan/steam-deck-tools):** Pelo trabalho pioneiro e referência técnica de engenharia reversa do protocolo USB HID do controle Neptune da Valve e controle do Lizard Mode.
- **[Nefarius Software Solutions](https://github.com/nefarius) (Benjamin Höglinger-Stelzer):** Pelo desenvolvimento do driver de kernel **ViGEmBus** e do client .NET, possibilitando a emulação de gamepads virtuais no Windows.

