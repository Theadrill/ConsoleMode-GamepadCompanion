# Brainstorming: Sistema de Biblioteca & Launcher de Games (Visão Futura)

Este documento registra a concepção de arquitetura, fluxo de UX e design da funcionalidade **GAMES (Biblioteca / Game Launcher)** do **ConsoleMode-GamepadCompanion**.

> 💡 **Nota de Escopo:** Esta funcionalidade é para maturação e desenvolvimento futuro. O companion continuará inicialmente monitorando o `WoW.exe` e `turtle-wow.exe` de forma direta e estável.

---

## 🎯 **PRIORIDADE ZERO: Navegação por Gamepad na Interface Atual**

> ⚠️ **IMPLEMENTAR PRIMEIRO antes de qualquer feature de biblioteca de games.**

Antes de criar o Grid de Games, precisamos implementar navegação completa por gamepad na **interface atual** (SettingsForm + Gamepad Visual Debugger).

### Requisitos:
- Navegação por **D-Pad** entre os controles da interface (sliders, botões, dropdown).
- **Foco visual** claro nos elementos navegáveis.
- **Botão A** confirma/ativa o elemento focado.
- **Botão B** cancela/volta.
- Sliders ajustáveis via **D-Pad esquerda/direita** ou **analógicos**.
- Suporte a vibração para feedback tátil (pavimentado mesmo se não usado inicialmente).

### Arquitetura:
- Criar módulo `UI/Navigation/GamepadNavigationManager.cs` para gerenciar foco e navegação.
- Integrar com `Hardware/XInput/XInputReader.cs` existente.
- Manter separação de responsabilidades (navegação ≠ lógica de negócio).

---

## 1. Visão Geral & Integração na Interface

Na barra lateral da janela principal (abaixo dos sliders de Sensibilidade, Deadzone e Gatilho):
* Teremos um botão de alternância chamado **GAMES** (ou atalho rápido).
* Ao ser acionado:
  * A área da direita (que atualmente exibe o **Gamepad Visual Debugger**) dá lugar a uma **Grade de Games (Game Grid / Covers)** estilo biblioteca de console / Steam.
  * O app ganha navegação completa nativa por gamepad dentro da própria interface, sem precisar usar mouse.

---

## 2. Navegação 100% pelo Gamepad na UI

A experiência é pensada como um console "Couch Gaming":

1. **Acesso à Grade:**
   * O botão **`Y`** do controle alterna diretamente para a área da biblioteca de games.
   * Quando já estiver na biblioteca, o botão **`B`** retorna à tela anterior (Gamepad Debugger).
2. **Navegação Espacial nas Capas:**
   * O **D-Pad (Direcional)** é capturado para navegar entre as capas dos jogos (cima, baixo, esquerda, direita).
   * A capa em foco ganha destaque/borda e efeito de escurecimento com dois botões centrais sobrepostos:
     * **INICIAR**
     * **CONFIGURAR**
3. **Ações Diretas sem Clique:**
   * Estando com o foco em um jogo, o usuário **não precisa clicar com o mouse**:
     * Botão **`A`**: Dispara diretamente a ação **INICIAR** (executa o jogo).
     * Botão **`Y`**: Dispara diretamente a ação **CONFIGURAR** (abre painel de propriedades).
     * Botão **`X`**: Abre a **BARRA DE PESQUISA** com teclado virtual para filtrar jogos.
4. **Último Slot da Grade:**
   * O último item da grade é sempre um retângulo com o ícone **`+` (Adicionar Novo Jogo)**, que ao ser selecionado e confirmado com `A` abre a tela de cadastro.

---

## 3. Painel de Configuração do Jogo (Estilo Steam Shortcut)

Ao acionar **CONFIGURAR** (via botão `Y` ou clique), a área central do grid se transforma no formulário de propriedades do jogo (inspirado no painel da Steam):

### Campos de Entrada (Inputs):
1. **Nome do Game:** Nome de exibição na biblioteca (ex: `Turtle WoW`).
2. **Nome do Launcher (Opcional):** Processo intermediário caso o jogo seja iniciado via script ou launcher (ex: script `.bat`, `cmd.exe`, `VanillaFixes.exe`).
3. **Nome do Executável Principal:** O executável real que o Companion deve monitorar ativamente (ex: `WoW.exe`, `turtle-wow.exe`).
   * *Motivação Técnica:* Muitos jogos executam scripts `.cmd` ou launchers para definir afinidade de CPU (`affinity`), prioridade de processo (`start /high`) ou aplicar patches antes de subir o binário principal. Monitorar o launcher faria o companion perder o rastreio logo após o launcher fechar. Ter o executável principal separado garante que o perfil Gaming permaneça ativo durante toda a gameplay.
4. **Destino (Target / Executable Path):** Caminho completo para o executável ou script a ser disparado (com botão "Procurar...").
5. **Iniciar Em (Working Directory):** Diretório de trabalho do executável (com botão "Procurar...").
6. **Opções de Inicialização (Arguments):** Parâmetros de linha de comando passados ao binário (ex: `-windowed`, `-console`, etc.).
7. **Alterar Capa (Cover Art):** Botão para escolher a imagem personalizada que será exibida na grade da biblioteca.

---

## 4. Persistência dos Jogos

Cada jogo configurado terá seus dados salvos em um arquivo de catálogo (ex: `games.ini` ou uma seção dedicada por jogo no próprio `config.ini` / pasta `profiles/`):
```ini
[Game_TurtleWoW]
Name=Turtle WoW
LauncherName=VanillaFixes.exe
MainExecutable=turtle-wow.exe
TargetPath="C:\Games\turtle wow\VanillaFixes.exe"
WorkingDirectory="C:\Games\turtle wow"
Arguments=""
CoverImagePath="covers/turtle_wow.png"
```

---

## 5. **[PENDING]** Teclado Virtual Integrado

> 🔄 **Status:** Especificação em andamento — seção GRILL ME contém questões e respostas.

### Objetivo:
Fornecer um teclado virtual navegável por gamepad para edição de todos os campos de texto (inputs) do painel de configuração e da barra de pesquisa, mantendo a experiência 100% sem mouse.

### Paleta de Cores (extraída do código):
```csharp
// Backgrounds
Background Principal:     Color.FromArgb(30, 32, 40)      // #1E2028
Background Secundário:    Color.FromArgb(24, 26, 32)      // #181A20
Background Elemento:      Color.FromArgb(34, 37, 46)      // #22252E
Background Idle:          Color.FromArgb(52, 56, 66)      // #343842

// Contornos & Bordas
Outline/Border:           Color.FromArgb(120, 126, 140)   // #787E8C

// Texto & Foreground
Texto Principal:          Color.FromArgb(230, 232, 240)   // #E6E8F0
Texto Secundário:         Color.FromArgb(160, 170, 185)   // #A0AAB9

// Estados & Feedback
Status Ativo (Verde):     Color.FromArgb(150, 220, 160)   // #96DCA0
Ativo Highlight (Verde):  Color.FromArgb(120, 230, 150)   // #78E696
Gaming Profile (Azul):    Color.FromArgb(120, 190, 255)   // #78BEFF
Azul Analógico:           Color.FromArgb(90, 170, 255)    // #5AAAFF
Amarelo Deadzone:         Color.FromArgb(255, 190, 60)    // #FFBE3C
Alerta (Laranja):         Color.FromArgb(255, 190, 90)    // #FFBE5A
Erro/Pressed (Vermelho):  Color.FromArgb(255, 120, 120)   // #FF7878

// Botões Toggle
Toggle ON:                Color.FromArgb(46, 125, 80)     // #2E7D50
Toggle OFF:               Color.FromArgb(150, 80, 60)     // #96503C
Texto Botão:              Color.White                      // #FFFFFF
Lit/Highlight:            Color.FromArgb(240, 240, 245)   // #F0F0F5
```

### Requisitos Identificados:
- Layout **QWERTY próximo do completo**.
- Primeira linha: **símbolos mais usados no Windows** (`:`, `\`, `/`, `_`, `-`, `.`, etc.) + botão toggle para símbolos adicionais.
- Navegação via **D-Pad** (célula por célula, segurar = movimento contínuo).
- **LB/RB** alternância de maiúscula/minúscula (Caps Lock instantâneo).
- Botão **`A`** seleciona tecla com animação de press down + vibração.
- Botão **`RT`** confirma e fecha teclado.
- Botão **`B`** cancela e fecha teclado.
- **Overlay escuro** sobre a interface, destacando apenas o input atual e o teclado abaixo dele.

---

## 6. **[PENDING]** Barra de Pesquisa (Search Bar)

> 🔄 **Status:** Especificação pendente — aguardando término do GRILL ME do Teclado Virtual.

### Objetivo:
Permitir filtro rápido de jogos na biblioteca através de busca textual, totalmente operável por gamepad via botão **`X`**.

### Requisitos Identificados:
- Acionada pelo botão **`X`** quando na tela da biblioteca.
- Ao abrir, invoca automaticamente o **Teclado Virtual**.
- Filtragem em tempo real (ou após confirmar com Enter)?
- Suporte a busca parcial/fuzzy (ex: "turt" encontra "Turtle WoW").

---

## 7. Resumo do Fluxo

```
[UI Principal — Gamepad Debugger]
       │
       ├── (Botão Y) ──► Alterna para Grid de Games
       │
       ▼
 [Grade de Jogos]
       ├── D-Pad: Navega entre as capas dos jogos
       ├── Botão A: Executa o TargetPath (INICIAR jogo selecionado)
       ├── Botão Y: Abre Painel de Configurações do jogo selecionado
       ├── Botão X: Abre Barra de Pesquisa + Teclado Virtual [PENDING]
       ├── Botão B: Retorna ao Gamepad Debugger
       └── Slot [+]: Adiciona novo jogo (abre Painel vazio)
             │
             ├───────► [Painel de Configuração do Jogo]
             │         ├── Nome do Game
             │         ├── Nome do Launcher (Opcional)
             │         ├── Nome do Executável Principal
             │         ├── Destino (TargetPath) + botão "Procurar..."
             │         ├── Iniciar Em (WorkingDirectory) + botão "Procurar..."
             │         ├── Opções de Inicialização (Arguments)
             │         ├── Alterar Capa (Cover Art) + botão "Procurar..."
             │         └── [Ao focar em Input] ──► Teclado Virtual [PENDING]
             │
             └───────► [Barra de Pesquisa] [PENDING]
                       ├── Abre automaticamente com Teclado Virtual
                       ├── Filtragem em tempo real ou após Enter (a definir)
                       ├── D-Pad navega apenas entre resultados filtrados
                       ├── Botão B: Limpa busca e retorna à grade completa
                       └── Feedback visual: "X resultados" ou "Nenhum resultado"
```

### Legenda de Mapeamento de Botões (Xbox Layout):
- **`Y` (Amarelo/Superior):** Alternar entre Gamepad Debugger ↔ Grid de Games
- **`B` (Vermelho/Direito):** Voltar/Cancelar
- **`A` (Verde/Inferior):** Confirmar/Iniciar Jogo
- **`X` (Azul/Esquerdo):** Abrir Barra de Pesquisa
- **`D-Pad`:** Navegação espacial (cima, baixo, esquerda, direita)

---

## 8. GRILL ME — Discussão de Design & Especificação

Esta seção registra as questões críticas de design e suas respostas durante o planejamento.

---

### 🎹 **TECLADO VIRTUAL**

#### **1. Layout & Organização**

**Questão:** Quer um layout QWERTY completo (como teclado físico) ou um layout simplificado console-style (tipo Xbox/PlayStation)?

**Resposta:** Layout QWERTY próximo do completo. No lugar da linha de números, colocar uma linha de símbolos mais utilizados no Windows (`:`, `\`, `/`, `_`, `-`, `.`, etc.) + um botão de toggle para mostrar símbolos adicionais. Incluir tecla de Caps Lock.

**Status:** ✅ DEFINIDO

---

#### **2. Navegação & Input**

**Questão:** Como será a navegação? D-Pad puro ou analógico também? Navegação acelerada (bumper + D-Pad pula linha)?

**Resposta:** 
- **D-Pad** move célula por célula.
- Ao **segurar D-Pad**, ele vai se movendo automaticamente (repeat).
- **Analógico esquerdo:** sem função por enquanto.
- **Navegação acelerada:** não implementar.
- **Caps Lock:** Usar **LB/RB** para alternar maiúscula/minúscula instantaneamente (não precisa selecionar tecla Caps Lock no teclado virtual).

**Status:** ✅ DEFINIDO

---

#### **3. Feedback Visual & Identidade**

**Questão:** Paleta de cores? Feedback visual da tecla selecionada? Animação ao pressionar?

**Resposta:**
- **Paleta:** Extraída do código (ver seção 5).
- **Tecla selecionada:** Hover simples (borda/highlight), nada extravagante. Foco na UX funcional.
- **Ao pressionar A:** Animação de "press down" + **vibração** para feedback tátil.
- **Som:** Não implementar por enquanto, mas deixar pavimentado (métodos null/vazios para futuro).

**Status:** ✅ DEFINIDO

---

#### **4. Posicionamento & Transição**

**Questão:** Popup centralizado? Docked na parte inferior? Animação de entrada/saída?

**Resposta:**
- **Overlay escurece toda a interface**, deixando visível apenas:
  - O **input atual** (campo de texto sendo editado).
  - O **teclado virtual** posicionado embaixo do input.
- **RT** confirma e fecha o teclado/overlay.
- **B** cancela e fecha o teclado/overlay.
- Ao confirmar/cancelar: overlay desaparece, foco retorna ao input, usuário pode navegar novamente pela interface.

**Status:** ✅ DEFINIDO

**Observação:** LT/RT inicialmente cogitados para trocar de "tela de teclas" foram descartados. RT é apenas para confirmar.

---

#### **5. Botões Especiais & Layout Completo** ✅ **DEFINIDO**

**Questão:** Além das letras, quais botões especiais o teclado virtual precisa ter? Como organizar o layout?

**Resposta:**
- Layout baseado no teclado do **Kaspersky** (referência visual fornecida)
- **LT físico do controle** = Clear All (atalho rápido)
- **Botões de navegação ← →** no teclado virtual para mover cursor no texto
- **Copiar/Colar** usando clipboard do Windows (copia sempre o texto INTEIRO do input)
- **Backspace** em posição padrão (canto superior direito, botão grande)
- **Enter** em posição padrão (lado direito, botão grande)
- **Shift** visual na linha 4 (além do LB/RB físico)
- **Caps Lock** visual na linha 3 (além do LB/RB físico)
- **Toggle+** para símbolos adicionais
- **Tab** na linha 2 (navegação futura entre inputs?)

**Layout Completo (5 linhas):**

```
Linha 1 (símbolos): [ : ] [ \ ] [ / ] [ _ ] [ - ] [ . ] [ ( ] [ ) ] [ " ] [ @ ] [Toggle+] [Backspace═══]
Linha 2 (QWERTY):   [Tab] [ q ] [ w ] [ e ] [ r ] [ t ] [ y ] [ u ] [ i ] [ o ] [ p ] [ [ ] [ ] ] [ | ]
Linha 3 (ASDFGH):   [Caps] [ a ] [ s ] [ d ] [ f ] [ g ] [ h ] [ j ] [ k ] [ l ] [ ; ] [ ' ]  [Enter═══]
Linha 4 (ZXCVBN):   [Shift] [ z ] [ x ] [ c ] [ v ] [ b ] [ n ] [ m ] [ , ] [ . ] [ / ] [ ? ]
Linha 5 (utilitários): [Copiar] [Colar] [═══════ Espaço ═══════] [ ← ] [ → ] [Clear All]
```

**Símbolos da Linha 1 — Justificativa (paths do Windows):**
1. **`:`** — Unidade (C:, D:)
2. **`\`** — Separador de diretório Windows
3. **`/`** — Separador alternativo
4. **`_`** — Underscore em nomes de arquivo
5. **`-`** — Hífen em nomes de arquivo
6. **`.`** — Extensões (.exe, .bat)
7. **`(`** **`)`** — Program Files (x86)
8. **`"`** — Aspas para paths com espaço
9. **`@`** — E-mails ou paths especiais
10. **[Toggle+]** — Abre tela com símbolos adicionais: `!` `$` `%` `&` `*` `+` `=` `[` `]` `{` `}` `;` `'` `<` `>` `?` `|`

**Comportamentos:**
- **Copiar**: Usa `Clipboard.SetText()` do Windows, copia conteúdo INTEIRO do input sempre
- **Colar**: Usa `Clipboard.GetText()` e insere na posição do cursor
- **← →**: Move cursor 1 caractere (segurar = auto-repeat)
- **Clear All**: Limpa todo o texto do input (botão visual + LT físico)
- **Backspace**: Apaga 1 caractere antes do cursor (segurar = auto-repeat)
- **Enter**: Fecha teclado e salva (mesmo efeito do RT físico)
- **Tab**: Navega para próximo input do formulário (funcionalidade futura?)

**Removido da proposta:**
- ❌ Up/Down arrows — inputs são linha única, não precisam

**Status:** ✅ **DEFINIDO**

---

### 🔍 **BARRA DE PESQUISA** 

> ⏸️ **PAUSADO** — Aguardando término do GRILL ME do Teclado Virtual.

---

#### **1. Comportamento de Filtro** ✅ **DEFINIDO**

**Questão:** Live search (filtra enquanto digita cada letra) ou on-demand (só filtra ao pressionar Enter)? Se live, quer debounce (aguarda 300ms após última tecla antes de filtrar)?

**Resposta:**
- **Live search** a cada tecla digitada
- **Debounce inicial: 0ms** (filtro instantâneo)
- Implementar com valor configurável para ajuste posterior se necessário (ex: 300ms se performance for problema)

**Status:** ✅ **DEFINIDO**

---

#### **2. Visual & Posicionamento** ⏳ **EM ABERTO**

**Questão:** Barra fixa no topo da grade (sempre visível mas desfocada) ou popup centralizado (aparece só quando pressiona X)? Mostrar contador tipo "3 de 12 jogos" enquanto filtra?

**Resposta:** ⏳ Aguardando resposta.

**Status:** ⏳ **EM ABERTO**

---

#### **3. Comportamento de Navegação Pós-Filtro** ⏳ **EM ABERTO**

**Questão:** Cards que não batem com a busca ficam escondidos (removidos da grade) ou opacados/esmaecidos (visíveis mas claramente não-match)? D-Pad navega apenas entre matches ou ainda pode focar nos não-matches esmaecidos?

**Resposta:** ⏳ Aguardando resposta.

**Status:** ⏳ **EM ABERTO**

---

#### **4. Limpeza da Busca** ⏳ **EM ABERTO**

**Questão:** Botão B do controle limpa a busca e retorna tudo? Ou precisa Backspace até string vazia? Quer um ícone de "X" na barra para limpar com clique/A?

**Resposta:** ⏳ Aguardando resposta.

**Status:** ⏳ **EM ABERTO**

---

#### **5. Escopo da Busca** ⏳ **EM ABERTO**

**Questão:** Busca case-insensitive sempre? Busca apenas no Nome do Game ou também em Nome do Executável, LauncherName, Arguments? Busca partial match (contém substring) ou fuzzy (permite pequenos typos tipo "turt" → "Turtle")?

**Resposta:** ⏳ Aguardando resposta.

**Status:** ⏳ **EM ABERTO**

---

#### **6. Estado de "Nenhum Resultado"** ⏳ **EM ABERTO**

**Questão:** Se a busca retorna zero matches, mostrar mensagem tipo "Nenhum jogo encontrado" no centro da grade? Ou apenas deixa a grade vazia? Quer sugestão tipo "Pressione B para limpar a busca"?

**Resposta:** ⏳ Aguardando resposta.

**Status:** ⏳ **EM ABERTO**

