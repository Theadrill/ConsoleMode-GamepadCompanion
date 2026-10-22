# Plano de Implementação — Perfil do Modo Desktop (Windows Navigation)

> **Documento de Arquitetura Técnica e Roteiro de Fases Testáveis**  
> Criação e integração do Perfil do Modo Desktop para uso contínuo do Gamepad no Windows (quando nenhum jogo mapeado estiver em foco) com representação visual fixa na biblioteca de jogos.

---

## 1. Visão Geral & Problema a Resolver
Atualmente, o **ConsoleMode-GamepadCompanion** monitora e ativa perfis de controle exclusivamente quando janelas de jogos cadastrados entram em foco. No Windows comum (ou no Steam Deck rodando Windows com o Lizard Mode desativado pelo driver nativo), ao minimizar o jogo ou utilizar o sistema operacional, o gamepad não possui perfil ativo atribuído, deixando o usuário sem navegação couch gaming no desktop.

O **Modo Desktop** resolve esse gargalo funcionando como o **Perfil Global de Fallback do Sistema**:
1. Quando um jogo mapeado está em primeiro plano: aplica o perfil do jogo.
2. Quando o usuário está no Windows (Desktop, navegador, explorador, launcher de terceiros): o Companion assume automaticamente o **Perfil Desktop** (se ativado).

---

## 2. Decisões Arquiteturais de Tech Lead

### 2.1. Desacoplamento no Domínio: Não Poluir `GameEntry`
* **Problema:** Um `GameEntry` representa um executável rastreável com caminhos de disco, contagem de horas, SteamGridDB IDs e argumentos de launcher. Modo Desktop **não é um jogo**.
* **Solução:** Criar uma abstração ou identificador reservado de sistema (`GameEntry.DesktopModeId = "__system_desktop_mode__"` ou `SystemProfileEntry`), com flags claras:
  - `IsSystemPinned = true`
  - `CanLaunch = false`
  - `CanDelete = false`
  - `CanEditPaths = false` (sem campos de executável ou launcher)
* O arquivo de persistência do perfil desktop é salvo como `desktop.profile.json` (ou referenciado de forma fixa em `Config.ini`), imune a exclusões acidentais da biblioteca de jogos.

### 2.2. Card Especial Fixo na Grid da Biblioteca
* O primeiro item da grid da biblioteca passa a ser fixo: **"Modo Desktop"** (ou "Desktop Mode").
* **Identidade Visual:** Capa estilizada fixa com o logo do Windows 11 / Desktop OS (embutida diretamente nos recursos locais do app, sem dependência do SteamGridDB).
* **Navegação Couch Gaming:**
  - Em cards comuns de jogos: `[A]` dispara `Iniciar Jogo`, `[Menu]/Configurar` abre as configurações.
  - No Card de Desktop: `[A]` abre diretamente a tela de **Configurar Perfil / Mapeamento**, e o painel de configuração exibe apenas o Toggle Ativo/Inativo e o botão de configurar mapeamento.

### 2.3. Transição Inteligente no Motor de Foco (`DynamicWindowFocusService`)
* O serviço de foco em background avalia continuamente o processo ativo em primeiro plano via Win32:
  - Se o processo corresponder a um jogo cadastrado com input ativado: aplica o perfil daquele jogo.
  - Se nenhum jogo cadastrado estiver ativo:
    - Se `DesktopMode.Enabled == true`: aplica o perfil desktop.
    - Se `DesktopMode.Enabled == false`: mantém o input em repouso (pass-through puro ou desativado).

---

## 3. Roteiro de Fases de Execução

### FASE 1: Modelo de Domínio, Persistência & Preset Desktop Padrão 🚀
* **Objetivo:** Criar a estrutura de dados do perfil desktop e os presets ideais de uso no Windows.
* **Tarefas:**
  1. `DesktopProfile`: Estrutura persistente com toggle `Enabled` (habilitado/desabilitado por padrão) e mapeamento de botões.
  2. `DesktopProfilePresets`: Preset ergonômico padrão para desktop (Stick esquerdo/direito para mouse de alta precisão, gatilhos/bumpers para botões esquerdo/direito/scroll, D-Pad para setas/navegação, botão central/guia para tarefas).
  3. Testes unitários de persistência e clonagem.

### FASE 2: Card Pinned de Sistema na Grid da Biblioteca (`GamesGridControl`)
* **Objetivo:** Exibir o card fixo do Modo Desktop na primeira posição da biblioteca de jogos.
* **Tarefas:**
  1. Renderização do Card de Sistema: arte fixa (ícone moderno Windows 11), badge de status `[ATIVO / DESATIVADO]`.
  2. Ajuste na máquina de estados de navegação por Gamepad na grid: seleção do card desktop bloqueia exclusão `[X]` e direciona `[A]` para tela de configuração.
  3. `GameConfigPanel` adaptado para o Modo Desktop: esconde campos irrelevantes (caminhos de executável, launcher, instâncias múltiplas) e exibe com destaque o Toggle de Ativação e o botão de editar mapeamento.

### FASE 3: Integração com o Motor de Foco & Chaveamento em Tempo Real
* **Objetivo:** Fazer o chaveamento dinâmico entre perfis de jogos e o perfil de desktop em tempo real.
* **Tarefas:**
  1. Atualização do `DynamicWindowFocusService` / `InputEngine`: se nenhuma janela de jogo estiver em foco, chavear para o `DesktopProfile`.
  2. Prevenção de loop ou conflito quando a janela do próprio Companion estiver em foco (navegação de UI suspende a emulação de mouse/teclas para evitar auto-cliques).
  3. Validação no Steam Deck: garantia de que o desarmamento do Lizard Mode mantém o controle desktop ativo de forma ininterrupta.

---

## 4. Critérios de Aceite
1. O Card do Modo Desktop aparece como o primeiro item imutável da biblioteca, com arte temática do Windows 11.
2. O usuário consegue configurar os botões do Modo Desktop da mesma forma que configura qualquer jogo.
3. Ao alternar entre a área de trabalho do Windows e um jogo cadastrado, o Companion chaveia automaticamente o perfil correto em segundo plano.
4. Ao desativar o toggle do Modo Desktop, o controle repousa no desktop sem emitir teclas não desejadas.
