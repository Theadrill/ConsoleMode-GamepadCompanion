# Brainstorming & Especificação de Arquitetura — ConsoleMode-GamepadCompanion

Este documento registra todas as decisões de design, regras de negócio e requisitos técnicos definidos na sessão de **Grill Me** para a construção do **ConsoleMode-GamepadCompanion**.

---

## 1. Contexto e Motivação

* **Addon Alvo:** [ConsoleModeVanilla](https://github.com/Theadrill/ConsoleModeVanilla) — Interface de console/gamepad para World of Warcraft 1.12.1 (Turtle WoW / cliente Vanilla).
* **Problema:** No Steam Deck, o Steam Input funciona nativamente via SteamOS. No Desktop (Windows), o usuário não quer depender do cliente da Steam rodando em segundo plano para conseguir mapear o controle e jogar.
* **Objetivo:** Criar um aplicativo companion nativo em C# (.NET Framework 4.8), ultraleve, portátil, que rode na bandeja do sistema (system tray) e traduza os inputs de um controle selecionado (Xbox / 8BitDo via XInput) para teclado e mouse, reproduzindo com fidelidade total o mapeamento feito originalmente no Steam Input (`Octowow`).

---

## 2. Mapeamento de Entradas (Layout Octowow)

Baseado nas configurações de controle validadas:

| Entrada Física (Gamepad) | Ação / Tecla Emitida | Comportamento & Função |
| :--- | :--- | :--- |
| **Alavanca Esquerda (L-Stick)** | `W`, `A`, `S`, `D` + `F9` | Movimento do personagem. Dispara `F9 Down` ao sair da deadzone e `F9 Up` ao retornar à deadzone. |
| **Alavanca Direita (R-Stick)** | Movimento do Mouse | Controle de câmera e mira. Emulação de mouse via curva exponencial suave. |
| **Clique R3 (RS Click)** | Botão Direito do Mouse | Trava/destrava de câmera e interação com o mundo. |
| **Botão A** | `Espaço` + `F9` | Pulo e confirmação de interface. Dispara `F9 Down` ao pressionar e `F9 Up` ao soltar. |
| **Botão B** | `3` | Barra de ação (Combate) / Fechar-Cancelar (Navegação de UI). |
| **Botão X** | `1` | Barra de ação. |
| **Botão Y** | `2` | Barra de ação. |
| **D-Pad Cima** | `7` | Barra de ação / Navegação vertical de interface. |
| **D-Pad Baixo** | `8` | Barra de ação / Navegação vertical de interface. |
| **D-Pad Esquerda** | `9` | Barra de ação / Navegação horizontal de interface. |
| **D-Pad Direita** | `0` | Barra de ação / Navegação horizontal de interface. |
| **LB (Left Bumper)** | `Tab` | Seleção de alvos (`CM_SMART_TAB`). |
| **RB (Right Bumper)** | `Ctrl` | Modificador de Página 3 (`R1`). |
| **LT (Left Trigger)** | `Shift` | Modificador de Página 2 (`L2`). Comportamento digital instantâneo. |
| **RT (Right Trigger)** | `Alt` | Modificador de Página 4 (`R2`). Comportamento digital instantâneo. |
| **Select / View / Back** | `M` | Abertura/fechamento do Mapa e Menu Principal do ConsoleMode. |
| **Start / Menu** | `F11` | Menu do jogo e cancelamentos. |
| **Botão Central (Guide / Home / 8BitDo)** | Overlay / Configurações | Abre ou oculta a janela de configurações do Companion. |

---

## 3. Decisões do Grill Me (Árvore de Decisão)

### Ramo 1: Mecânica do `F9` e Smart Mouse Look
* **Propósito no WoW:** No cliente Vanilla 1.12.1, o `F9` está atrelado ao comando `CM_MOUSELOOK_START` (que chama internamente `MouselookStart()`). Isso trava a câmera no mouse em estilo de jogo de ação (sem necessidade de segurar o botão direito do mouse manualmente).
* **Solução Técnica:** 
  * Ao mover o analógico esquerdo além da zona morta: emite `F9 Down` junto com o `WASD`.
  * Ao soltar o analógico esquerdo: emite `F9 Up`.
  * Ao pressionar o botão `A`: emite `F9 Down` junto com `Espaço`.
  * Ao soltar o botão `A`: emite `F9 Up`.
  * Quando o personagem para de andar, o WoW mantém o estado do `MouselookStart()` ativo até nova ordem, garantindo rotação contínua da câmera.

### Ramo 2: Emulação de Mouse no Analógico Direito (R-Stick)
* **Curva de Resposta:** Curva exponencial com potência (~2.0) e deadzone circular centralizada.
  * Deflexões leves no analógico: movimentação lenta e cirúrgica para micro-ajustes de mira e cursor.
  * Deflexões acentuadas: aceleração dinâmica para permitir giros rápidos de 180° e 360°.
* **Clique R3:** Emite evento `Down` e `Up` do Botão Direito do Mouse padrão da Win32 API.

### Ramo 3: Gatilhos Analógicos LT e RT
* Apesar de serem eixos analógicos contínuos de hardware (valores de 0 a 255 no XInput), devem agir como **chaves digitais puras**:
  * Ao ultrapassar o threshold inicial de acionamento (~15-20% de curso), disparam imediatamente `Shift Down` (LT) ou `Alt Down` (RT).
  * Ao retornar abaixo do threshold, soltam imediatamente a respectiva tecla.

### Ramo 4: Gerenciamento de Perfis e Foco de Janela
* O Companion terá uma arquitetura de perfis (`ProfileManager`) com dois modos:
  1. **Gaming:** Ativado automaticamente quando a janela em primeiro plano for o World of Warcraft (`turtle-wow.exe` ou `WoW.exe`). Executa todo o mapeamento acima.
  2. **Desktop:** Ativado quando qualquer outra janela estiver em foco (ex: navegador, Discord, desktop do Windows).
* **Comportamento inicial do modo Desktop:** O envio de comandos do gamepad fica silenciado/inativo, deixando o código estruturado com os `if/else` e interfaces necessárias prontos para quando a tela de mapeamento customizado de desktop for desenvolvida no futuro.

### Ramo 5: Interface da Bandeja (System Tray) e Overlay
* **Interação com a Bandeja:** Qualquer clique no ícone (esquerdo ou direito) abre diretamente a janela de configurações.
* **Janela de Configurações:**
  * Dropdown para seleção de controle ativo (`Jogador 1`, `Jogador 2`, `Jogador 3`, `Jogador 4`), com indicação visual de conexão em tempo real e auto-seleção inteligente do primeiro gamepad plugado.
  * Botão de ativação / pausa manual do mapeamento.
  * Slider de sensibilidade do mouse para o analógico direito.
  * Status visual do perfil ativo (`Gaming [WoW]` vs `Desktop [Inativo]`).
  * **Visual Debugger do Gamepad (Testador em Tempo Real):** Representação gráfica/visual do controle no painel com feedback ao vivo dos botões pressionados (A, B, X, Y, D-Pad, LB, RB, LT, RT, Start, Back, Guide) e movimentação dos eixos analógicos (L-Stick e R-Stick) para o usuário validar na hora se o controle está respondendo perfeitamente.
  * Fechamento da janela oculta para a bandeja em vez de matar o processo.
  * Botão explícito para "Sair" (encerra o aplicativo).
* **Botão Guide / Home:** Pressionar o botão central do controle atua como toggle (abre/fecha a janela de configurações centralizada na tela, similar ao Steam Overlay).

---

## 4. Stack Tecnológica Escolhida

* **Linguagem / Framework:** C# / .NET Framework 4.8 (Windows Forms para janela leve e `NotifyIcon`).
* **Compatibilidade:** Windows 7 SP1 até Windows 11 (incluso nativamente no SO sem necessidade de instalação de runtimes adicionais).
* **Acesso ao Gamepad:** P/Invoke nativo para `xinput1_4.dll` (com fallback para `xinput9_1_0.dll` / `xinput1_3.dll`). Leitura estendida para o botão Guide (`XInputGetStateEx`).
* **Despacho de Comandos:** P/Invoke para `SendInput` da Win32 API (`user32.dll`), garantindo latência de ~1ms e compatibilidade total com o cliente de jogo.

---

## 5. Roadmap & Próximos Passos (TODO)

- [ ] **Overlay Estilo Steam:** Interface de overlay transparente/imersiva acionada pelo botão Guide/Home do controle, permitindo ajustar configurações rápidas por cima do jogo sem perder o contexto visual.
- [ ] **Perfil Desktop (Uso no Windows):** Mapeamento dedicado para navegar e usar o computador normalmente pelo controle quando o WoW não estiver em foco (movimentação de mouse livre no stick esquerdo/direito, cliques esquerdo/direito nos gatilhos/botões frontais, scroll e atalhos multimídia).

---

## 6. Ecossistema de Skills Integradas (Importadas de DeskQuadra e Base de Conhecimento)

O projeto incorpora em `.agent/skills/` um conjunto selecionado de skills especializadas para guiar o desenvolvimento e as decisões arquiteturais:

* **P/Invoke & Interoperabilidade com Windows:**
  * `dotnet-pinvoke`: Padrões de marshaling seguro e de alta performance para chamadas Win32 (`SendInput`, `GetForegroundWindow`) e `XInput`.
* **Qualidade de Código & Engenharia C#:**
  * `csharp-pro` e `clean-code`: Princípios SOLID, Clean Architecture e design modular (zero monólito).
  * `csharp-refactoring`: Estratégias de refatoração cirúrgica sem quebra de contratos.
  * `coding-guidelines`: Diretrizes de estilo e robustez corporativa em .NET.
* **Performance & Baixa Latência:**
  * `analyzing-dotnet-performance` e `microbenchmarking`: Otimização do loop de polling (120Hz-250Hz), zero alocações na hot path e prevenção de GC pauses.
* **Interface & Experiência de Usuário:**
  * `ui-ux-pro-max`, `ui-visual-validator`, `better-colors`, `contrast-checker`: Excelência visual para a janela de configurações e futuro overlay.
  * `wpf-windows-desktop`: Padrões avançados de desktop Windows.
* **Testes & Confiabilidade:**
  * `run-tests`, `assertion-quality`, `test-anti-patterns`, `test-smell-detection`: Testes automatizados focados na matemática de curvas de aceleração e deadzones.
* **Operação de Shell & Build:**
  * `windows-shell-reliability` e `msbuild-modernization`: Execução confiável de scripts de terminal, controle de processos e builds sem travas.
* **Documentação:**
  * `docs-writer`: Manutenção de documentação clara e viva.


