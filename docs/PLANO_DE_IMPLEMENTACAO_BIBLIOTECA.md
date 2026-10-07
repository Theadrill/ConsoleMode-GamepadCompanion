# Plano de Implementação — Sistema de Biblioteca de Games & Teclado Virtual

> Este documento define as fases atômicas de desenvolvimento do **Sistema de Biblioteca de Games, Busca Fuzzy e Teclado Virtual Integrado** do **ConsoleMode-GamepadCompanion**.

---

## Metodologia & Regras de Execução

1. **Parada Obrigatória:** Ao final de cada fase, a compilação deve estar limpa (0 erros e 0 warnings), e a IA para obrigatoriamente para o usuário testar com o controle em mãos.
2. **Autonomia de Processos:** Antes de qualquer compilação (`dotnet build`), o agente encerra preventivamente o processo do app caso esteja rodando (`Stop-Process -Name ConsoleMode.GamepadCompanion -ErrorAction SilentlyContinue`).
3. **Autonomia de Logs:** Todo diagnóstico, teste ou leitura de logs é feito diretamente pelo agente via terminal/arquivos.
4. **Sem Commits/Push Prematuros:** Commitar apenas após validação aprovada pelo usuário. Push apenas sob solicitação explícita.
5. **Clean Code & Padrões:** Seguir as diretrizes das skills locais em `.agent/skills/` (`dotnet-pinvoke`, `csharp-pro`, `clean-code`, `ui-ux-pro-max`).

---

## Paleta de Cores da UI

- **Fundo Principal:** `Color.FromArgb(30, 32, 40)` (`#1E2028`)
- **Fundo Secundário (Debugger/Grids):** `Color.FromArgb(24, 26, 32)` (`#181A20`)
- **Fundo Elementos/Teclas:** `Color.FromArgb(34, 37, 46)` (`#22252E`)
- **Fundo Idle:** `Color.FromArgb(52, 56, 66)` (`#343842`)
- **Bordas/Outlines:** `Color.FromArgb(120, 126, 140)` (`#787E8C`)
- **Texto Principal:** `Color.FromArgb(230, 232, 240)` (`#E6E8F0`)
- **Texto Secundário:** `Color.FromArgb(160, 170, 185)` (`#A0AAB9`)
- **Destaque/Foco (Azul):** `Color.FromArgb(120, 190, 255)` (`#78BEFF`) / `Color.FromArgb(90, 170, 255)`
- **Ativo/Confirmado (Verde):** `Color.FromArgb(120, 230, 150)` (`#78E696`) / `Color.FromArgb(150, 220, 160)`
- **Overlay Escurecido:** `Color.FromArgb(190, 15, 17, 22)`

---

## 🚀 Fases de Implementação

---

### FASE 1: Navegação por Gamepad na Interface Atual (SettingsForm)

* **Objetivo:** Permitir controle 100% da barra lateral esquerda do `SettingsForm` apenas usando o D-Pad e botões do controle, com foco inicial no topo, overlay escurecido em sliders/dropdown, e alteração suave de valores.
* **Tarefas:**
  1. **Atualização de Strings & Renomeação:**
     - Modificar `Strings.cs` para substituir os textos de estado de mapeamento por ações claras: `"Ativar Companion"` (quando pausado) e `"Desativar Companion"` (quando ativo).
  2. **Motor de Navegação de UI (`GamepadNavigationManager`):**
     - Criar `UI/Navigation/INavigableControl.cs` para abstrair controles focáveis.
     - Implementar `UI/Navigation/ButtonNavigable.cs`, `SliderNavigable.cs`, `DropdownNavigable.cs`.
     - Implementar `UI/Navigation/GamepadNavigationManager.cs` gerenciando a lista de controles da barra esquerda com suporte a auto-repeat com aceleração progressiva ao segurar direções.
  3. **Overlay de Foco (`FocusOverlayPanel`):**
     - Criar `UI/Controls/FocusOverlayPanel.cs` para escurecer a interface inteira (`Color.FromArgb(190, 15, 17, 22)`) destacando apenas o elemento em edição ativa.
  4. **Integração no `SettingsForm`:**
     - Integrar navegação no `SettingsForm.cs`, tratando eventos de D-Pad, Botão A (confirma/ativa) e Botão B (cancela/restaura).

* **🛑 Critério de Aceite & Validação do PO:**
  1. Iniciar o app com controle conectado e abrir a janela de configurações.
  2. **Navegar na lista:** Usar D-Pad ↑ e ↓. O foco visual deve se mover entre os botões, dropdown e sliders na barra esquerda.
  3. **Testar Botão de Ativar/Desativar:** Focar nele e apertar `A`. O status do Companion deve alternar entre "Desativar Companion" (verde) e "Ativar Companion" (vermelho).
  4. **Testar Slider com Edição & Cancelamento:** Focar no slider de Sensibilidade. Apertar `A`. A tela deve escurecer destacando o slider. Alterar o valor com D-Pad ←→. Apertar `B`. O valor deve retornar ao original sem salvar.
  5. **Testar Slider com Confirmação:** Apertar `A` no slider, mudar o valor, segurar para testar aceleração progressiva, e apertar `A`. O valor deve ser salvo e o overlay desaparecer.
  6. **Testar Dropdown:** Focar no dropdown de controle ativo, apertar `A`, navegar nos slots com D-Pad ↑↓ e confirmar com `A` ou cancelar com `B`.

---

### FASE 2: Catálogo de Jogos & Grid de Biblioteca (Games Grid)

* **Objetivo:** Permitir que o usuário alterne para a tela de Biblioteca via botão `Y`, navegue pelas capas dos jogos cadastrados via D-Pad, acione o jogo com `A`, acesse o painel de propriedades estilo Steam Shortcut com `Y` e cadastre novos jogos no slot `[+]`.
* **Tarefas:**
  1. **Persistência `games.ini` & Modelo `GameEntry`:**
     - Criar `Core/Models/GameEntry.cs` (`Id`, `Name`, `LauncherName`, `MainExecutable`, `TargetPath`, `WorkingDirectory`, `Arguments`, `CoverImagePath`).
     - Criar `Hardware/GameRepository.cs` para persistência INI com pré-cadastro do Turtle WoW caso o arquivo não exista.
  2. **Grid de Capas (`GamesGridControl`):**
     - Criar `UI/Controls/GameCoverCard.cs` com proporção de capa (~3:4), renderização de imagem/fallback escuro com nome e overlays de ação `[A] INICIAR` e `[Y] CONFIGURAR`.
     - Criar slot especial `[+] Adicionar Novo Jogo`.
     - Criar `UI/Controls/GamesGridControl.cs` com layout responsivo e rolagem suave automática.
  3. **Painel de Propriedades do Jogo (`GameConfigPanel`):**
     - Construir formulário estilo Steam com os 7 campos (Nome, Launcher, Executável Principal, TargetPath, WorkingDirectory, Arguments, CoverImagePath) e botões de arquivo/pasta.
  4. **Lançador de Jogos & Alternância de Visual:**
     - Criar `Engine/GameLauncher.cs` disparando o processo e registrando o `MainExecutable` no `WindowTracker` para ativação automática do perfil Gaming.
     - Integrar no `SettingsForm.cs`: Botão `Y` alterna visual (Debugger ↔ Grade de Games), `B` retorna ao Debugger.

* **🛑 Critério de Aceite & Validação do PO:**
  1. Abrir o companion e apertar `Y` no controle.
  2. **Verificar transição:** A área da direita deve trocar do Gamepad Debugger para a Grade de Jogos (com card do Turtle WoW e slot `[+]`).
  3. **Navegar na grade:** Usar D-Pad para navegar entre as capas e o slot `[+]`. O card selecionado deve ficar destacado.
  4. **Configurar Jogo:** Focar em um jogo e apertar `Y`. O painel de propriedades do jogo deve se abrir.
  5. **Adicionar Jogo:** Focar no slot `[+]` e apertar `A`. O painel de cadastro deve se abrir em branco.
  6. **Lançar Jogo:** Focar no jogo configurado e apertar `A`. O jogo/launcher deve ser iniciado no Windows.
  7. **Voltar:** Apertar `B` para retornar ao Gamepad Debugger.

---

### FASE 3: Barra de Pesquisa com Busca Fuzzy (Teclado Físico inicial)

* **Objetivo:** Permitir busca e filtragem dinâmica de jogos através de um algoritmo Fuzzy Search rápido, acionado pelo botão `X` do controle na grade de jogos, validando a lógica de filtro com teclado físico.
* **Tarefas:**
  1. **Motor de Busca Fuzzy (`FuzzySearchEngine`):**
     - Criar `Engine/Search/FuzzySearchEngine.cs` com busca case-insensitive e tolerante a pequenos erros de digitação (ex: "turtl wow" encontra "Turtle WoW").
     - Escopo de match: `GameEntry.Name` e `GameEntry.MainExecutable`.
  2. **Barra de Pesquisa Visual (`SearchBarControl`):**
     - Criar `UI/Controls/SearchBarControl.cs` fixo no topo da grade de jogos, com ícone de lupa, botão visual de limpar `[ ✕ ]` e destaque de foco.
     - Implementar estado de *"Nenhum resultado"* no `GamesGridControl` com as mensagens:
       - Linha 1: *"Nenhum jogo encontrado"*
       - Linha 2: *"Pressione B para limpar a busca"*
     - Reorganizar automaticamente os cards na grade (ocultar sem matches sem deixar buracos).
  3. **Integração na Grade:**
     - Botão `X` no controle foca na barra de pesquisa.
     - Digitação com Live Search (debounce 0ms configurável).
     - Botão `B` no controle ou clique no `[ ✕ ]` limpa o texto e restaura a grade inteira.

* **🛑 Critério de Aceite & Validação do PO:**
  1. Entrar na tela de Games (`Y`).
  2. Pressionar `X` no controle: a barra de pesquisa no topo deve receber o foco.
  3. Digitar no teclado físico: testar buscas parciais e com erros (ex: "turt", "warcrft", "wow").
  4. **Verificar Live Search:** Os cards correspondentes devem aparecer imediatamente e os outros devem sumir sem deixar buracos na grade.
  5. **Verificar Estado Vazio:** Digitar algo inexistente (ex: "xyz123"). A tela deve mostrar *"Nenhum jogo encontrado"* e *"Pressione B para limpar a busca"*.
  6. **Verificar Limpeza:** Pressionar `B` no controle (ou clicar no `[ ✕ ]`). O texto deve sumir e todos os jogos reaparecerem.

---

### FASE 4: Teclado Virtual Integrado (Couch Gaming)

* **Objetivo:** Integrar o teclado virtual completo de 5 linhas estilo Kaspersky na paleta escura do app, navegável 100% por gamepad com D-Pad, LT para limpar, RT/Enter para confirmar, B para cancelar, LB/RB para Caps, Copiar/Colar com clipboard do Windows e setas para mover cursor.
* **Tarefas:**
  1. **Layout e Definição de Teclas:**
     - Criar `UI/VirtualKeyboard/VirtualKeyDefinition.cs` e `KeyboardLayoutProvider.cs` com 5 linhas:
       - *Linha 1 (Símbolos Windows):* `[ : ] [ \ ] [ / ] [ _ ] [ - ] [ . ] [ ( ] [ ) ] [ " ] [ @ ] [Toggle+] [Backspace]`
       - *Linha 2 (QWERTY):* `[Tab] [ q ] [ w ] [ e ] [ r ] [ t ] [ y ] [ u ] [ i ] [ o ] [ p ] [ [ ] [ ] ] [ | ]`
       - *Linha 3 (ASDFGH):* `[Caps] [ a ] [ s ] [ d ] [ f ] [ g ] [ h ] [ j ] [ k ] [ l ] [ ; ] [ ' ] [Enter]`
       - *Linha 4 (ZXCVBN):* `[Shift] [ z ] [ x ] [ c ] [ v ] [ b ] [ n ] [ m ] [ , ] [ . ] [ / ] [ ? ]`
       - *Linha 5 (Utilitários):* `[Copiar] [Colar] [        Espaço        ] [ ← ] [ → ] [Clear All]`
       - *Sub-layout Toggle+:* Símbolos adicionais (`! $ % & * + = { } < > ? |`).
  2. **Componente Visual `VirtualKeyboardControl`:**
     - Renderização de alta fidelidade na paleta do app com animação de press down.
     - Criar `Hardware/GamepadVibrationService.cs` para feedback háptico sutil via XInput.
  3. **Edição de Texto & Clipboard:**
     - Criar `UI/VirtualKeyboard/TextInputBuffer.cs` gerenciando inserção, cursor, clipboard Windows (`Clipboard.SetText` para texto inteiro e `Clipboard.GetText`).
     - Mapear atalhos físicos: `LT` (Clear All), `RT`/`Enter` (Confirmar), `B` (Cancelar), `LB`/`RB` (Caps instantâneo), `A` (digita tecla + vibração).
  4. **Integração no Sistema:**
     - Integrar o Teclado Virtual na Barra de Pesquisa e nos campos de entrada do `GameConfigPanel` com overlay escurecido destacando o input ativo e o teclado.

* **🛑 Critério de Aceite & Validação do PO:**
  1. Abrir a Biblioteca de Jogos (`Y`) e apertar `X`: o Teclado Virtual deve aparecer com overlay escurecido abaixo da barra de busca.
  2. **Digitar pelo controle:** Usar D-Pad para navegar nas teclas e apertar `A` para digitar. Sentir a leve vibração tátil e animação de press down.
  3. **Testar Caps:** Apertar `LB` ou `RB`. As letras devem mudar para maiúsculas imediatamente.
  4. **Testar Símbolos Windows:** Digitar caminhos usando a primeira linha (`:`, `\`, `-`, `.`).
  5. **Testar Clipboard:** Apertar `[Copiar]` para copiar o texto digitado. Ir no Notepad do Windows e dar `Ctrl+V` para verificar se copiou exatamente o texto. Copiar algo no Windows e apertar `[Colar]` no teclado virtual.
  6. **Testar Navegação de Cursor:** Usar os botões `[ ← ]` e `[ → ]` para inserir texto no meio de uma palavra.
  7. **Testar Limpar:** Puxar o gatilho `LT` (ou clicar em `[Clear All]`). O texto deve ser apagado inteiro.
  8. **Testar Confirmação e Cancelamento:** Apertar `RT` para confirmar ou `B` para fechar.
  9. **Testar em Formulário:** Abrir o Painel de Configuração do Jogo (`Y`), focar no campo "Nome do Game" ou "Destino" e apertar `A`. O teclado virtual deve se abrir perfeitamente posicionado.
