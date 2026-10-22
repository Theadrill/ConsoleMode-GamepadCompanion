# Plano de Implementação — Perfil do Modo Desktop (Windows Navigation)

> **Documento de Arquitetura Técnica e Roteiro de Fases Testáveis**  
> Criação e integração do Perfil do Modo Desktop para navegação contínua no Windows via Gamepad através de acesso dedicado na Barra Lateral (Sidebar) e chaveamento por fallback no motor de foco.

---

## 1. Visão Geral & Regras de Negócio

### 1.1. O Problema
Atualmente, o **ConsoleMode-GamepadCompanion** monitora e ativa perfis de controle exclusivamente quando janelas de jogos cadastrados entram em foco. No Windows comum (ou no Steam Deck com o Lizard Mode desativado), ao minimizar o jogo, fechar o jogo ou utilizar o sistema operacional (navegador, explorador, Discord, etc.), o gamepad não possui perfil ativo atribuído, deixando o usuário sem controle couch gaming no desktop.

### 1.2. A Solução (Regra de Chaveamento por Foco)
1. **Janela de Jogo Rastreado em Foco:** O Companion ativa e executa o perfil específico daquele jogo (`GamingProfile`).
2. **Qualquer Outra Janela em Foco (Windows Desktop, Navegador, Explorer, etc.):** O Companion chaveia automaticamente para o **Modo Desktop** (se ativado nas configurações).
3. **Janela do Próprio Companion Aberta e em Foco:** A emulação de mouse/teclas do Desktop é suspensa temporariamente para que o controle seja usado 100% na navegação da interface do app sem disparar auto-cliques ou interferir com o cursor.

---

## 2. Decisão Arquitetural: Acesso via Barra Lateral (Sidebar)

Em vez de poluir a lista de jogos na biblioteca com um card fake, o **Modo Desktop** é uma funcionalidade global do sistema e pertence naturalmente à **Barra Lateral (Sidebar)** do `SettingsForm`:

* **Botão na Sidebar:** `[ 🖥️ MODO DESKTOP ]` com badge dinâmico de status `[ATIVADO]` ou `[DESATIVADO]`.
* **Área de Conteúdo:** Ao clicar ou selecionar o botão via controle na sidebar, abre diretamente o `GameProfileEditorControl` carregado com o perfil do Desktop:
  - Título: `PERFIL DE CONTROLE — MODO DESKTOP (WINDOWS)`
  - Toggle dedicado para habilitar ou desabilitar o Modo Desktop globalmente.
  - Sliders de sensibilidade do mouse e limiar de gatilhos.
  - Mapeamento completo idêntico ao dos jogos (ABXY, D-Pad, Sticks, Triggers, Bumpers, Botões Traseiros L4/L5/R5/R4 no Steam Deck).
* **Persistência:** O perfil desktop é salvo de forma limpa e isolada em `profiles/desktop.json` e o estado do toggle em `Config.ini` (`DesktopModeEnabled=true`).

---

## 3. Roteiro de Fases de Execução

### FASE 1: Modelo de Domínio, Preset Padrão & Persistência 🚀
* **Objetivo:** Criar a estrutura de dados completa do Modo Desktop, persistência em disco e preset ergonômico padrão para Windows.
* **Tarefas:**
  1. `GameProfilePresets.CreateDesktopDefault()`:
     - Stick Esquerdo: Movimento de mouse suave (`StickMode.MouseMove`).
     - RT: Clique Primário do Mouse (Botão Esquerdo).
     - LT: Clique Secundário do Mouse (Botão Direito).
     - RB / LB: Rolagem ou Navegação (Browser Forward / Browser Back).
     - Botão A: Tecla `Enter` (Confirmação).
     - Botão B: Tecla `Esc` (Cancelar / Voltar).
     - Botão X: Tecla `Espaço`.
     - Botão Y: Tecla `Backspace`.
     - D-Pad: Setas direcionais (`Up`, `Down`, `Left`, `Right`).
     - Start: Tecla `Windows` (Menu Iniciar).
     - Select / Back: `Alt+Tab` (`CompanionActionType.AltTab`).
     - Cliques L3 / R3: Clique do meio do mouse / Mostrar Desktop (`Win+D`).
     - Botões Traseiros Steam Deck (L4/L5/R4/R5): Volume, Task View ou ações rápidas.
  2. `DesktopProfile`: Evolução da classe para suportar `GameProfile` ativo, controle de união de estados por frame e respeito à flag `DesktopModeEnabled`.
  3. `AppSettings`: Adicionar campo `DesktopModeEnabled` (padrão: `true`) com persistência em `Config.ini`.
  4. Persistência de arquivo: Carregamento e salvamento em `profiles/desktop.json`.
  5. Testes unitários cobrindo preset, persistência e ciclo de reset.

### FASE 2: Interface na Sidebar (`SettingsForm`) & Integração com o Editor Visual
* **Objetivo:** Permitir ao usuário acessar, visualizar e customizar o perfil do Modo Desktop diretamente pela interface do aplicativo.
* **Tarefas:**
  1. Adicionar o botão `Modo Desktop` na sidebar do `SettingsForm`, com suporte a navegação por Gamepad e badge visual `[ATIVADO / DESATIVADO]`.
  2. Conectar a ação do botão à exibição do `GameProfileEditorControl` com o contexto desktop.
  3. Salvamento do perfil desktop ao pressionar `[RT] Salvar` ou confirmar no editor.

### FASE 3: Motor de Chaveamento em Tempo Real (`ProfileManager` + `WindowTracker`)
* **Objetivo:** Conectar o ciclo de vida do Modo Desktop ao foco de janelas do Windows.
* **Tarefas:**
  1. Atualizar o `ProfileManager`:
     - Se `_windowTracker.IsGameFocused`: usa o perfil do jogo.
     - Se `!_windowTracker.IsGameFocused`: se `DesktopModeEnabled == true`, ativa o `DesktopProfile`.
  2. Detectar quando a janela do próprio Companion (`SettingsForm`) estiver focada e suspender o envio de inputs simulados para não interferir na navegação da UI.
  3. Testes unitários integrados validando as transições de foco.

---

## 4. Critérios de Aceite da Fase 1
1. `GameProfilePresets.CreateDesktopDefault()` retorna um perfil válido com controles ergonômicos de mouse e teclado.
2. `DesktopProfile` é capaz de receber e executar o perfil desktop de forma robusta e silenciosa quando desativado.
3. `AppSettings` serializa e deserializa `DesktopModeEnabled`.
4. Todos os novos testes unitários passam com 100% de sucesso e 0 avisos no build.
