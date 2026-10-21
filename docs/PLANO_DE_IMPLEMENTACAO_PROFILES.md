# Plano de Implementação — Sistema de Perfis por Jogo & Editor Visual de Controles

> **Documento de Arquitetura Técnica, Identidade Visual, UX e Roteiro de Fases Testáveis**  
> Implementação do sistema híbrido de Perfis por Jogo, Toggle de Simulação de Input por Jogo, Comandos Adicionais/Combos em Botões e Analógicos, e Seletor Visual em 3 Abas estilo Steam Input aproveitando 100% da área de conteúdo do **ConsoleMode-GamepadCompanion**.

---

## 1. Metodologia & Regras de Execução

1. **Parada Obrigatória por Fase:** Ao final de cada fase, a compilação deve estar limpa (`0 erros` e `0 warnings`), todos os testes unitários devem passar, e o desenvolvimento pausa obrigatoriamente para o usuário testar na tela com o controle em mãos.
2. **Nenhuma Fase Invisível:** Todas as fases entregam resultado visual e interativo testável diretamente no aplicativo.
3. **Aproveitamento Total da Área de Conteúdo (`Dock = DockStyle.Fill`):** Nenhuma tela do fluxo abre janelas externas (`Form.ShowDialog`) ou popups flutuantes fora do app. Todas as telas (`GameConfigPanel` ➔ `GameProfileEditorControl` ➔ `InputActionSelectorControl`) alternam suavemente dentro da área principal de conteúdo, preservando o foco Win32/XInput e a legibilidade para Couch Gaming / Handheld.
4. **Autonomia de Processos:** Antes de compilar (`dotnet build`), o agente encerra preventivamente qualquer instância em execução do `ConsoleMode-GamepadCompanion`.
5. **Sem Commits/Push Prematuros:** Commits e push somente após validação e solicitação explícita do usuário.

---

## 2. Identidade Visual & Paleta de Cores do App

Todas as novas telas devem seguir rigorosamente a identidade visual escura, moderna e de alto contraste do aplicativo, incluindo integração com o **Dynamic Hero Background** (capa borrada com máscara de vinheta ao fundo quando aplicável) e renderização via GDI+ com `DoubleBuffered = true`:

- **Fundo Principal (`SettingsForm` / Painéis):** `Color.FromArgb(30, 32, 40)` (`#1E2028`)
- **Fundo Secundário (Área de Conteúdo / Editor):** `Color.FromArgb(24, 26, 32)` (`#181A20`)
- **Fundo de Cards / Teclas / Slots:** `Color.FromArgb(34, 37, 46)` (`#22252E`)
- **Fundo de Inputs / Caixas Internas:** `Color.FromArgb(20, 22, 28)` (`#14161C`)
- **Fundo em Foco do Controle (Hover/Selected):** `Color.FromArgb(35, 50, 78)` (`#23324E`)
- **Bordas Padrão (Idle):** `Color.FromArgb(120, 126, 140)` (`#787E8C`)
- **Borda de Destaque / Foco Gamepad (Azul):** `Color.FromArgb(120, 190, 255)` (`#78BEFF`)
- **Estado Ativo / Confirmado / Marcado no Combo (Verde):** `Color.FromArgb(120, 230, 150)` (`#78E696`)
- **Estado Alerta / Desativado / Limpar (Vermelho/Âmbar):** `Color.FromArgb(220, 95, 95)` / `Color.FromArgb(245, 158, 11)`
- **Texto Principal:** `Color.FromArgb(230, 232, 240)` (`#E6E8F0`)
- **Texto Secundário / Legendas:** `Color.FromArgb(160, 170, 185)` (`#A0AAB9`)
- **Tipografia:** `"Segoe UI"` (títulos em `Bold`, badges de botões com cantos arredondados e legendas de rodapé padronizadas).

---

## 3. Desenhos ASCII das Telas (Wireframes da Área de Conteúdo)

### 3.1. Barra Lateral Limpa (`SettingsForm`) + Tela do Jogo (`GameConfigPanel`)
A barra lateral esquerda mantém apenas a seleção do controle físico, status, acesso à biblioteca e saída. Os controles de mapeamento passam a viver dentro de cada jogo:

```text
+------------------------+--------------------------------------------------------------------+
| CONTROLE ATIVO         |  CONFIGURAR JOGO: Turtle WoW                                       |
| [ Automático       v ] |--------------------------------------------------------------------|
|                        |  Nome do Jogo:          [ Turtle WoW                             ] |
| Status: Conectado (#1) |  Launcher (opcional):   [ Launcher.exe                           ] |
| Perfil: Turtle WoW     |  Executável Principal:  [ WoW.exe                                ] |
|                        |  Caminho do Alvo:       [ C:\Games\TurtleWoW\WoW.exe ] [Procurar]  |
|                        |  Diretório de Trabalho: [ C:\Games\TurtleWoW         ] [Procurar]  |
|                        |  Argumentos:            [                                        ] |
| [  GAMES / DEBUGGER  ] |  Imagem de Capa:        [ covers\wow.png     ] [Arquivo] [SteamDB] |
|                        |                                                                    |
| [        Sair        ] |  ----------------------------------------------------------------  |
|                        |  INPUT DO COMPANION NESTE JOGO:                                    |
|                        |  [ (●) ATIVADO - Emular Teclado/Mouse ]   [ 🎮 Configurar Controles]|
|                        |  ----------------------------------------------------------------  |
|                        |                                                                    |
|                        |  [ Salvar (RT) ]        [ Cancelar (B) ]         [ Excluir Jogo ]  |
+------------------------+--------------------------------------------------------------------+
```

---

### 3.2. Tela do Editor Visual de Perfil (`GameProfileEditorControl`)
Ocupa toda a área de conteúdo (`Dock = DockStyle.Fill`). Apresenta os ajustes de sensibilidade (por página), deadzone e gatilhos (com toggle Global) no topo, e o diagrama do controle no centro cercado pelos cards de mapeamento:

```text
+---------------------------------------------------------------------------------------------+
|  PERFIL DE CONTROLE: Turtle WoW                         [ Importar Preset / Copiar Jogo v ] |
|---------------------------------------------------------------------------------------------|
|  Sensibilidade Mouse (Página 1): [=======O-------] 50                                       |
|  Deadzone Analógicos:            [====O----------] 24%  [X] Global                          |
|  Limiar de Gatilhos (LT/RT):     [===O-----------] 20%  [X] Global                          |
|---------------------------------------------------------------------------------------------|
|                                                                                             |
|   [ LT: Shift ]                                                             [ RT: Alt ]     |
|   [ LB: Tab   ]                                                             [ RB: Ctrl ]    |
|                                     .---.         .---.                                     |
|   [ Select: M ]                    / [L] \_______/ [R] \                    [ Start: F11 ]  |
|                                   |  (O)   (Guide) (Y)  |                                   |
|   +-----------------------+       |   +    [ ] [ ] (X)(B)|       +-----------------------+  |
|   | STICK ESQUERDO        |       |  / \     (O)   (A)  |       | BOTÕES FACIAIS        |  |
|   | Modo: [ 4 Direções  ] |       | /   \_________/   \ |       | (Y): [ 2            ] |  |
|   | Cima/Baixo: [W] / [S] |        `---'           `---'        | (B): [ 3            ] |  |
|   | Esq/Dir:    [A] / [D] |                                     | (X): [ 1            ] |  |
|   | Adicional:  [ F9    ] |                                     | (A): [ Espaço + F9  ] |  |
|   | Clique L3:  [ MouseL] |                                     +-----------------------+  |
|   +-----------------------+                                     +-----------------------+  |
|   +-----------------------+                                     | STICK DIREITO         |  |
|   | D-PAD (SETAS)         |                                     | Modo: [ Mov. Mouse  ] |  |
|   | Cima: [ 7 ] Baixo:[ 8]|                                     | Adicional: [ Nenhum ] |  |
|   | Esq:  [ 9 ] Dir:  [ 0]|                                     | Clique R3: [ MouseR ] |  |
|   +-----------------------+                                     +-----------------------+  |
|                                                                                             |
|  (A) Editar Entrada Selecionada    (X) Limpar Entrada    (RT) Salvar Perfil    (B) Voltar   |
+---------------------------------------------------------------------------------------------+
```

---

### 3.3. Tela de Escolha do Modo de Edição & Captura Física de Combos (`InputBindingView`)
Ao selecionar qualquer botão ou comando adicional na tela do perfil, a área de conteúdo apresenta as opções de mapeamento e o modo de escuta para captura de combos:

```text
+---------------------------------------------------------------------------------------------+
|  EDITAR MAPEAMENTO: BOTÃO (A) — Jogo: Turtle WoW                                            |
|---------------------------------------------------------------------------------------------|
|                                                                                             |
|                          Mapeamento Atual: [ Espaço + F9 ]                                  |
|                                                                                             |
|                     +-------------------------------------------------+                     |
|                     |          🎙️  CAPTURAR INPUT FÍSICO              |                     |
|                     |   (Pressione tecla, combo ou botão do mouse)    |                     |
|                     +-------------------------------------------------+                     |
|                                                                                             |
|                     +-------------------------------------------------+                     |
|                     |          🖮   ESCOLHER BOTÃO / BOTÕES            |                     |
|                     |    (Abrir seletor visual: Teclado/Mouse/Sist.)  |                     |
|                     +-------------------------------------------------+                     |
|                                                                                             |
|                     +-------------------------------------------------+                     |
|                     |          🗑️  LIMPAR / DESATIVAR ENTRADA         |                     |
|                     +-------------------------------------------------+                     |
|                                                                                             |
|  * No modo Captura Física: segure múltiplas teclas (ex: Ctrl + Shift + S) e solte para      |
|    gravar o combo completo automaticamente.                                                 |
|---------------------------------------------------------------------------------------------|
|  (A) Selecionar Opção                                                          (B) Voltar   |
+---------------------------------------------------------------------------------------------+
```

---

### 3.4. Seletor Visual em 3 Abas na Área de Conteúdo (`InputSelectorTabsView`)

#### Aba 1: Teclado (`[LB]` **[ 1. TECLADO ]** | `2. MOUSE` | `3. SISTEMA & ESPECIAL` `[RB]`)
```text
+---------------------------------------------------------------------------------------------+
|  [LB]   [ ● 1. TECLADO ]     [ ○ 2. MOUSE ]     [ ○ 3. SISTEMA & ESPECIAL ]           [RB]  |
|---------------------------------------------------------------------------------------------|
|  Editando: BOTÃO (A)   |   Combo Montado: [ LeftCtrl + LeftShift + S ]                      |
|---------------------------------------------------------------------------------------------|
|  [Esc]   [F1] [F2] [F3] [F4]  [F5] [F6] [F7] [F8]  [F9] [F10] [F11] [F12]    [Ins][Del][Hm] |
|                                                                                             |
|  [ ` ] [ 1 ] [ 2 ] [ 3 ] [ 4 ] [ 5 ] [ 6 ] [ 7 ] [ 8 ] [ 9 ] [ 0 ] [ - ] [ = ] [ Backspace] |
|  [ Tab ] [ Q ] [ W ] [ E ] [ R ] [ T ] [ Y ] [ U ] [ I ] [ O ] [ P ] [ [ ] [ ] ] [   \    ] |
|  [ Caps ] [ A ] [ *S* ] [ D ] [ F ] [ G ] [ H ] [ J ] [ K ] [ L ] [ ; ] [ ' ] [   ENTER   ] |
|  [ *LShift* ] [ Z ] [ X ] [ C ] [ V ] [ B ] [ N ] [ M ] [ , ] [ . ] [ / ] [    RShift     ] |
|  [ *LCtrl* ] [ Win ] [ LAlt ] [          ESPAÇO          ] [ RAlt ] [ RCtrl ]  [←][↑][↓][→] |
|---------------------------------------------------------------------------------------------|
|  (A) Confirmar e Salvar   (X) Adicionar/Remover no Combo   (Y) Limpar Combo    (B) Cancelar |
+---------------------------------------------------------------------------------------------+
```

#### Aba 2: Mouse (`[LB]` `1. TECLADO` | **[ 2. MOUSE ]** | `3. SISTEMA & ESPECIAL` `[RB]`)
```text
+---------------------------------------------------------------------------------------------+
|  [LB]   [ ○ 1. TECLADO ]     [ ● 2. MOUSE ]     [ ○ 3. SISTEMA & ESPECIAL ]           [RB]  |
|---------------------------------------------------------------------------------------------|
|  Editando: STICK DIREITO (Comando Adicional)   |   Selecionado: [ Clique Direito ]          |
|---------------------------------------------------------------------------------------------|
|                                                                                             |
|            .-------.---.-------.               +-----------------------------------+        |
|           /  Esq    |M|   Dir   \              |  [ 🖱️ Clique Esquerdo (Mouse L) ] |        |
|          |   (L)   |(|)|  (R)    |             |  [ 🖱️ Clique Direito  (Mouse R) ] |        |
|          |          `-'          |             |  [ 🖱️ Clique do Meio  (Mouse M) ] |        |
|          |                       |             |  [ 🔼 Roda para Cima  (Scroll+) ] |        |
|          |         MOUSE         |             |  [ 🔽 Roda para Baixo (Scroll-) ] |        |
|           \                     /              |  [ 🚫 Limpar / Desativado       ] |        |
|            `-------------------'               +-----------------------------------+        |
|                                                                                             |
|---------------------------------------------------------------------------------------------|
|  (A) Confirmar e Salvar   (X) Adicionar/Remover no Combo   (Y) Limpar Combo    (B) Cancelar |
+---------------------------------------------------------------------------------------------+
```

#### Aba 3: Sistema & Especial (`[LB]` `1. TECLADO` | `2. MOUSE` | **[ 3. SISTEMA & ESPECIAL ]** `[RB]`)
```text
+---------------------------------------------------------------------------------------------+
|  [LB]   [ ○ 1. TECLADO ]     [ ○ 2. MOUSE ]     [ ● 3. SISTEMA & ESPECIAL ]           [RB]  |
|---------------------------------------------------------------------------------------------|
|  Editando: SELECT   |   Selecionado: [ Centralizar Cursor ]                                 |
|---------------------------------------------------------------------------------------------|
|   ÁUDIO & MÍDIA                   JANELAS & CAPTURA              FUNÇÕES DO COMPANION       |
|  +---------------------------+   +---------------------------+  +-------------------------+ |
|  | [ 🔊 Volume +           ] |   | [ 📸 Captura PrintScreen] |  | [ 🎯 Centralizar Cursor]| |
|  | [ 🔉 Volume -           ] |   | [ 🖥️ Tela Cheia Alt+Ent ] |  | [ 🔒 Toggle ClipCursor ]| |
|  | [ 🔇 Mutar Áudio        ] |   | [ 🔄 Trocar Janela A+Tab] |  +-------------------------+ |
|  | [ ⏯️ Play / Pause Mídia ] |   | [ 🪟 Mostrar Desktop W+D] |                              |
|  | [ ⏭️ Próxima Faixa      ] |   | [ ❌ Fechar Jogo Alt+F4 ] |                              |
|  | [ ⏮️ Faixa Anterior     ] |   | [ 🪟 Tecla Windows (Win)] |                              |
|  +---------------------------+   +---------------------------+                              |
|---------------------------------------------------------------------------------------------|
|  (A) Confirmar e Salvar   (X) Adicionar/Remover no Combo   (Y) Limpar Combo    (B) Cancelar |
+---------------------------------------------------------------------------------------------+
```

---

## 4. Fases Testáveis Incrementais

---

### FASE 1: Motor de Perfis por Jogo, Limpeza da Sidecar & Toggle de Input no Jogo

* **Objetivo:** Implementar a estrutura de dados de perfil por jogo (já preparada com `ProfilePage`), refatorar o motor `GamingProfile` para executar qualquer perfil dinamicamente (com união de estados e comandos adicionais nos analógicos), limpar a barra lateral esquerda e adicionar o **Toggle de Input por Jogo** no `GameConfigPanel`.
* **Tarefas:**
  1. **Modelos e Presets (`Core/Models/`):**
     - Expandir `VirtualKey.cs` com todas as teclas de teclado, mídia, sistema e navegação.
     - Criar `InputAction.cs` (suportando Tecla, Clique de Mouse, Scroll e Ações Especiais do Companion).
     - Criar `StickConfig.cs` (`Mode`: `DirectionalKeys`, `MouseMove`, `Disabled`; direções e `AdditionalActions`).
     - Criar `ProfilePage.cs` (contendo `MouseSensitivity`, `LeftStick`, `RightStick` e `ButtonMappings` com `List<InputAction>` para cada botão).
     - Criar `GameProfile.cs` (contendo `UseGlobalDeadzone`, `CustomDeadzonePercent`, `UseGlobalTriggerThreshold`, `CustomTriggerThresholdPercent` e `Pages`).
     - Criar `GameProfilePresets.cs` com os presets: **WoW Classic (Octowow)**, **RPG / MMO (Câmera Botão Direito)** e **Padrão PC (WASD + Mouse)**.
  2. **Persistência (`GameEntry.cs` e `GameRepository.cs`):**
     - Adicionar `EnableCompanionInput` (padrão `true`) e `Profile` em `GameEntry`.
     - Persistir e carregar o perfil do jogo no `games.ini` (garantindo que o Turtle WoW carregue o preset *WoW Classic* por padrão).
  3. **Motor Dinâmico (`WindowTracker`, `ProfileManager` e `GamingProfile`):**
     - Atualizar `WindowTracker` para identificar qual `GameEntry` está ativo em foco.
     - Refatorar `GamingProfile` para ler as ações do `GameProfile` ativo com **união de estados por frame** (garantindo que comandos compartilhados como `F9` ou `Mouse:Right` nunca sejam soltos enquanto outra entrada ativa ainda precisar deles, e que `MouseDown` ocorra antes de `MouseMoveRelative`).
     - No `ProfileManager`: se o jogo em foco tiver `EnableCompanionInput == false`, manter perfil silencioso e liberar `ClipCursor`, preservando apenas o botão **Guide** para abrir o overlay do Companion.
  4. **UI — Sidecar Limpa & Toggle no `GameConfigPanel`:**
     - Remover da barra lateral esquerda do `SettingsForm` o botão de toggle global e os 3 sliders antigos.
     - Adicionar no `GameConfigPanel` a seção de Input do Companion com o botão toggle **`[ (●) ATIVADO / (○) DESATIVADO ]`** e o botão **`[ 🎮 Configurar Controles ]`**, integrados à navegação por D-Pad.
  5. **Testes Unitários da Fase 1:**
     - Cobrir serialização do perfil no `games.ini`, união de estados no `GamingProfile` e desativação de input + `ClipCursor` quando `EnableCompanionInput == false`.

* **🛑 Critério de Aceite & Validação do PO (Fase 1):**
  1. Abrir o app e verificar que a barra lateral esquerda está enxuta (Controle Ativo, Status, Perfil Ativo, botão `GAMES` e `Sair`).
  2. Entrar em `GAMES` e abrir a configuração de um jogo (`Y`).
  3. Testar pelo controle ou mouse o novo botão **Input do Companion neste Jogo (`ATIVADO` / `DESATIVADO`)** e salvar com `RT`.
  4. Com o jogo marcado como `DESATIVADO`, focar na janela do jogo e confirmar que o controle não envia teclas simuladas nem prende o mouse, mas o botão **Guide** continua abrindo o Companion.
  5. Com o jogo marcado como `ATIVADO`, focar na janela do jogo e confirmar que o perfil do jogo é ativado normalmente.

---

### FASE 2: Tela do Editor Visual de Perfil do Jogo (`GameProfileEditorControl`)

* **Objetivo:** Implementar a tela visual estilo Steam Input ocupando 100% da área de conteúdo ao clicar em `[🎮 Configurar Controles]`, permitindo ajustar Sensibilidade da Página, Deadzone (Global/Individual), Limiar de Gatilho (Global/Individual), trocar o Modo dos Analógicos (`4 Direções` / `Movimento do Mouse` / `Desativado`), importar Presets ou copiar perfil de outro jogo, e visualizar todos os botões mapeados ao redor do diagrama do controle.
* **Tarefas:**
  1. **Componente `GameProfileEditorControl.cs` (`Dock = DockStyle.Fill`):**
     - Barra superior com título do jogo e seletor **`[Importar Preset / Copiar Jogo]`** (permitindo aplicar em 1 clique o preset *WoW Classic*, *RPG Câmera Botão Direito*, *Padrão PC* ou copiar o perfil de qualquer outro jogo cadastrado).
     - Sliders de **Sensibilidade do Mouse (Página 1)**, **Deadzone dos Analógicos** (com toggle `[X] Global`) e **Limiar de Gatilhos** (com toggle `[X] Global`), todos operáveis por controle (D-Pad ←→) e mouse.
     - Diagrama central do controle renderizado em GDI+ cercado pelos cards interativos de cada botão (`LT`, `LB`, `Select`, `Stick Esquerdo`, `D-Pad`, `RT`, `RB`, `Start`, `Botões Y/B/X/A`, `Stick Direito`).
     - Permitir alternar diretamente o **Modo do Stick Esquerdo e Direito** (`4 Direções`, `Movimento do Mouse`, `Desativado`) e limpar qualquer entrada com `X`.
  2. **Integração no `SettingsForm` e `GameConfigPanel`:**
     - Ao clicar em `[🎮 Configurar Controles]` no `GameConfigPanel`, alternar a área de conteúdo para o `GameProfileEditorControl`.
     - Botão `RT` (ou botão Salvar) confirma as alterações no perfil do jogo e retorna ao `GameConfigPanel`; botão `B` cancela e volta.

* **🛑 Critério de Aceite & Validação do PO (Fase 2):**
  1. Entrar na configuração de um jogo e acionar **`[🎮 Configurar Controles]`**.
  2. Verificar que o Editor Visual do Controle ocupa toda a área de conteúdo mantendo a identidade visual do app.
  3. Navegar com o D-Pad pelos sliders do topo: testar a alteração da **Sensibilidade da Página**, e testar marcar/desmarcar o toggle **`[Global]`** de Deadzone e Gatilho.
  4. Testar o botão **`[Importar Preset / Copiar Jogo]`**: alternar entre o preset *WoW Classic* (vendo o `F9` no Stick Esquerdo e no A) e o preset *RPG Câmera Botão Direito* (vendo o `Clique Direito` como comando adicional no Stick Direito).
  5. Navegar pelos cards ao redor do controle com o D-Pad, testar limpar um botão com `X`, salvar com `RT` e verificar que ficou persistido no jogo.

---

### FASE 3: Tela de Escolha de Edição & Captura Física de Input / Combos

* **Objetivo:** Permitir que, ao apertar `A` (ou clicar) em qualquer botão, direção de analógico ou slot de Comando Adicional dentro do `GameProfileEditorControl`, abra a tela de edição com os botões **`[🎙️ Capturar Input Físico]`**, **`[🖮 Escolher Botão / Botões]`** e **`[🗑️ Limpar / Desativar]`**, implementando por completo o detector de **Captura Física de Teclas, Combos Simultâneos e Cliques de Mouse**.
* **Tarefas:**
  1. **Componente `InputBindingCaptureControl.cs` (`Dock = DockStyle.Fill`):**
     - Tela de escolha inicial exibindo o botão do controle que está sendo editado, o mapeamento atual e os 3 botões de ação grandes.
     - Modo **Aguardando Comando (Captura Física):**
       - Intercepta `KeyDown` / `KeyUp` e cliques de mouse.
       - Acumula múltiplas teclas pressionadas simultaneamente na ordem em que foram apertadas (ex: segurar `LeftCtrl`, depois `LeftShift`, depois `S` exibe `[ LeftCtrl + LeftShift + S ]` na tela em tempo real).
       - Finaliza e aplica automaticamente o combo assim que as teclas são soltas (ou permite cancelar com `B` no controle).

* **🛑 Critério de Aceite & Validação do PO (Fase 3):**
  1. No Editor Visual de Controles, focar em qualquer botão (ex: `Botão A` ou `Comando Adicional do Stick Direito`) e apertar `A`.
  2. Selecionar **`[🎙️ Capturar Input Físico]`**.
  3. Testar apertar uma única tecla no teclado físico (ex: `Espaço` ou `F9`) e ver que ela é gravada imediatamente ao soltar.
  4. Testar gravar um **combo de 3 teclas** (ex: segurar `Ctrl + Shift + S` juntos e soltar) e verificar que o botão passa a exibir `LeftControl + LeftShift + S`.
  5. Testar clicar com o **Botão Direito do Mouse** durante a captura no slot de Comando Adicional do Stick Direito, salvar o perfil e testar no jogo/debugger.

---

### FASE 4: Seletor Visual em 3 Abas (`Teclado` | `Mouse` | `Sistema & Especial`) & Documentação

* **Objetivo:** Implementar o seletor visual completo navegável 100% pelo controle (trocando abas com `LB` / `RB`) quando o usuário escolhe **`[🖮 Escolher Botão / Botões]`**, permitindo montar tanto comandos simples quanto combos diretamente pelo controle sem precisar tocar no teclado físico.
* **Tarefas:**
  1. **Componente `InputVisualSelectorControl.cs` (`Dock = DockStyle.Fill`):**
     - Cabeçalho de Abas: **`[ 1. TECLADO ]`**, **`[ 2. MOUSE ]`**, **`[ 3. SISTEMA & ESPECIAL ]`** alternadas com `LB` / `RB` ou clique.
     - Barra de visualização do **Combo Montado** no topo.
     - **Aba 1 (Teclado Visual ANSI Completo):** Renderização GDI+ das fileiras `Esc + F1..F12`, numérica, `QWERTY`, modificadores (`LShift`, `LCtrl`, `LAlt`, `Win`, `Espaço`, `Enter`) e bloco de `Setas` + navegação.
     - **Aba 2 (Mouse):** Ilustração do mouse + cards para `Clique Esquerdo`, `Clique Direito`, `Clique do Meio`, `Scroll para Cima`, `Scroll para Baixo` e `Limpar`.
     - **Aba 3 (Sistema & Especial):** Cards organizados em 3 colunas:
       - *Áudio & Mídia:* `Volume +`, `Volume -`, `Mutar Áudio`, `Play/Pause`, `Próxima Faixa`, `Faixa Anterior`.
       - *Janelas & Captura:* `Print Screen`, `Tela Cheia (Alt+Enter)`, `Trocar Janela (Alt+Tab)`, `Mostrar Desktop (Win+D)`, `Fechar Janela (Alt+F4)`, `Tecla Windows`.
       - *Funções do Companion:* `Centralizar Cursor na Janela`, `Alternar Confinamento de Cursor (ClipCursor)`.
     - Interação por Controle:
       - `(A)`: Se o combo estiver vazio, seleciona o item focado e já confirma; se já houver itens marcados no combo via `(X)`, confirma o combo montado.
       - `(X)`: Adiciona ou remove o item focado no Combo Montado (permitindo montar ex: `Ctrl + Shift + S` ou `Espaço + F9` usando apenas o controle!).
       - `(Y)`: Limpa o combo atual.
       - `(B)`: Cancela e volta.
  2. **Atualização do `README.md` (Roadmap & TODOs):**
     - Documentar o sistema de Perfis por Jogo e Editor Visual de Controles.
     - Registrar no TODO do projeto:
       1. *Sistema de Páginas / Camadas por Modificador (alternar entre páginas de mapeamento dentro do jogo, como Navegação vs. Interação/Menus, cada uma com sua própria sensibilidade de mouse e botões).*
       2. *Avaliar expansão de comandos especiais do sistema na Aba 3.*

* **🛑 Critério de Aceite & Validação do PO (Fase 4):**
  1. No Editor de Controles, selecionar um botão com `A` e clicar em **`[🖮 Escolher Botão / Botões]`**.
  2. Trocar entre as 3 abas (`1. Teclado`, `2. Mouse`, `3. Sistema & Especial`) usando `LB` e `RB`.
  3. Na **Aba 1 (Teclado)**, testar escolher uma tecla simples com `A`, e depois testar montar um combo com `X` (ex: marcar `Espaço` com `X`, navegar até `F9`, marcar com `X` e confirmar com `A`).
  4. Na **Aba 2 (Mouse)**, testar selecionar `Clique Direito` ou `Scroll Up`.
  5. Na **Aba 3 (Sistema & Especial)**, testar mapear `Volume +`, `Print Screen` ou `Centralizar Cursor` em um botão e validar o funcionamento.
