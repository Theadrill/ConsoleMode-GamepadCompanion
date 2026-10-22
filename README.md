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

---

## 🎮 Suporte Nativo ao Steam Deck (Windows 10/11)

No Windows, os controles embutidos do Steam Deck (*Valve Neptune Controller*) iniciam por padrão em **Lizard Mode** (emulando apenas mouse e teclado básicos no desktop). Sem um driver específico, os jogos de PC com suporte nativo a gamepad não identificam o aparelho como um controle de jogo.

O **ConsoleMode - Gamepad Companion** possui suporte nativo completo para o Steam Deck:

- **Detecção Automática de Hardware:** Identifica automaticamente os modelos **LCD (Jupiter / Aerith)** e **OLED (Galileo)** via BIOS e barramento de sistema.
- **Driver Virtual de Gamepad (ViGEmBus):** Para que o Windows e os jogos reconheçam o Steam Deck como um gamepad Xbox 360 padrão, é necessário ter o driver de kernel **ViGEmBus** ativo no sistema. Caso o driver ainda não esteja presente, o Companion detecta no início e exibe uma tela amigável de boas-vindas integrada na área de conteúdo com **instalação silenciosa em segundo plano em 1 clique**.
- **Comunicação Direta USB HID & Fim do Lizard Mode:** O aplicativo conecta-se diretamente à interface HID (`0x28DE:0x1205`) da Valve, desativa o Lizard Mode enquanto o Companion estiver ativo e o restaura com segurança ao encerrar o app, garantindo que o usuário não perca a navegação do Windows.
- **Interface Visual de Handheld Modular:**
  - O **Visual Debugger** (demonstração ao vivo) e o **Editor de Perfis** se adaptam dinamicamente para a silhueta de um Handheld com tela central, exibindo os dois **Trackpads** (com feedback tátil de toque analógico e clique) e uma linha horizontal inferior unificada de **Botões Traseiros** (`[ L4 ] [ L5 ]   [ R5 ] [ R4 ]`).
  - Caso o usuário selecione um slot de controle externo (ex: Gamepad 2 ou PC Desktop), a interface comuta automaticamente de volta para a visualização clássica de Gamepad convencional.
- **Remapeamento de Hardware em Nível de Driver (ViGEm):**
  - Permite redirecionar os botões traseiros diretamente para comandos do gamepad virtual (por exemplo, **mapear `L4` para emitir `L1 / LB`** para contornar um botão físico defeituoso no hardware), funcionando nativamente em qualquer jogo.
  - Suporta também a atribuição de comandos de teclado, mouse ou macros personalizadas por perfil de jogo.

---

## 🗺️ Roadmap & Próximos Passos (TODO)
- [x] Mapeamento Gaming 100% idêntico ao layout `Octowow` do Steam Input (com Smart Mouse Look F9 e cliques L3/R3)
- [x] Visual Gamepad Debugger em tempo real com sliders de Deadzone e Gatilho
- [x] Persistência local em arquivos `.ini` e ciclo de vida em segundo plano na bandeja (System Tray)
- [x] Biblioteca de Games (Launcher com navegação 100% por gamepad, configuração de executáveis, SteamGridDB, hero backgrounds e teclado virtual)
- [x] Fase 1 dos Perfis: Motor de Perfis por Jogo, Limpeza da Barra Lateral e Toggle de Input do Companion por Jogo
- [x] Fase 2 dos Perfis: Tela do Editor Visual de Perfil (`GameProfileEditorControl`) com diagrama do controle e slots
- [x] Fase 3 dos Perfis: Tela de Escolha de Edição & Captura Física de Combos
- [x] Fase 4 dos Perfis: Seletor Visual em 3 Abas (`Teclado` | `Mouse` | `Sistema & Especial`)
- [x] **Suporte Nativo a Steam Deck no Windows (Fases 1 a 4 Concluídas):**
  - [x] Identificação de hardware Valve (Jupiter / Galileo).
  - [x] Onboarding amigável na área de conteúdo e instalação silenciosa do driver virtual ViGEmBus.
  - [x] Captura direta USB HID, desativação/reativação do Lizard Mode e emulação de Xbox 360 virtual.
  - [x] Visual Debugger Handheld na Home (trackpads ao vivo e linha de botões traseiros L4/L5/R5/R4) & Remapeamento de hardware no driver.
- [x] **Fase 5 (Steam Deck):** Reestruturação da Tela de Mapping para Controles Handheld (botões traseiros e placeholders para trackpads).
- [x] **Perfil do Modo Desktop (Navegação Windows 10/11) 🚀:**
  - Acesso direto e toggle rápido via botão dedicado na Sidebar (`[ 🖥️ MODO DESKTOP [ATIVADO/DESATIVADO] ]`).
  - Chaveamento automático de foco: executa o perfil do jogo quando o jogo está ativo e chaveia para o Modo Desktop em qualquer outra janela do Windows.
  - Suspensão instantânea de emulação quando o Companion está em primeiro plano para garantir navegação fluida da UI sem interferência.
  - Detalhado em [`docs/PLANO_DE_IMPLEMENTACAO_MODO_DESKTOP.md`](./docs/PLANO_DE_IMPLEMENTACAO_MODO_DESKTOP.md).
- [ ] **Fase 6 (Steam Deck):** Configuração dos Trackpads (Mouse/Trackball de alta precisão, Scrollwheel vertical contínuo, D-Pad e Botão Único com Feedback Háptico). Detalhado em [`docs/PLANO_DE_IMPLEMENTACAO_TRACKPADS.md`](./docs/PLANO_DE_IMPLEMENTACAO_TRACKPADS.md).
- [ ] **Menu Radial para Trackpads (Couch Gaming Overlay):**
  - Overlay circular translúcido exibido em tela enquanto o polegar estiver em contato com o trackpad (`Touch`).
  - Suporte a 4, 8 ou 12 fatias para atalhos de magias, poções, macros e comandos rápidos de sistema sem poluir a tela.
- [ ] **Explorer Customizado Interno do Aplicativo:**
  - Navegador e gerenciador de arquivos e pastas nativo embutido no aplicativo com navegação 100% Couch Gaming via gamepad (D-Pad, botões [A] entrar/selecionar, [B] voltar nível de pasta, [X] buscar/filtrar).
  - Permite localizar e selecionar executáveis (`.exe`) de jogos, diretórios de instalação e artes/imagens personalizadas diretamente pela TV ou tela do Steam Deck, sem depender do Windows Explorer convencional.
- [ ] **Toggle de Múltiplas Instâncias por Jogo (`Permitir Múltiplas Instâncias`):**
  - Adicionar no `GameConfigPanel` um botão toggle idêntico ao do Input (`[ (●) PERMITIR MÚLTIPLAS INSTÂNCIAS / (○) INSTÂNCIA ÚNICA (Padrão) ]`).
  - Se estiver em **Instância Única (Desativado - Padrão)**: ao acionar `[A] INICIAR`, caso o executável principal já esteja em execução, em vez de abrir outro processo, o app traz a janela do jogo já existente para primeiro plano e oculta a interface do Companion.
  - Se estiver ativado: permite abrir múltiplas instâncias em paralelo.
- [ ] Sistema de Páginas / Camadas por Modificador (alternar entre páginas de mapeamento dentro do jogo, como Navegação vs. Interação/Menus, cada uma com sua própria sensibilidade de mouse e botões)
- [ ] Avaliar expansão de comandos especiais do sistema na Aba 3
- [ ] Overlay estilo Steam acionado pelo botão central do controle (Guide/Home)

---

## 🙏 Créditos & Agradecimentos
- **[ayufan / Steam Deck Tools](https://github.com/ayufan/steam-deck-tools):** Pelo trabalho pioneiro e referência técnica de engenharia reversa do protocolo USB HID do controle Neptune da Valve e controle do Lizard Mode.
- **[Nefarius Software Solutions](https://github.com/nefarius) (Benjamin Höglinger-Stelzer):** Pelo desenvolvimento do driver de kernel **ViGEmBus** e do client .NET, possibilitando a emulação de gamepads virtuais no Windows.
