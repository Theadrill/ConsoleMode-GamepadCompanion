# Brainstorming: Sistema de Biblioteca & Launcher de Games (Visão Futura)

Este documento registra a concepção de arquitetura, fluxo de UX e design da funcionalidade **GAMES (Biblioteca / Game Launcher)** do **ConsoleMode-GamepadCompanion**.

> 💡 **Nota de Escopo:** Esta funcionalidade é para maturação e desenvolvimento futuro. O companion continuará inicialmente monitorando o `WoW.exe` e `turtle-wow.exe` de forma direta e estável.

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
   * O botão `X` do controle foca/alterna diretamente para a área da biblioteca de games.
2. **Navegação Espacial nas Capas:**
   * O **D-Pad (Direcional)** é capturado para navegar entre as capas dos jogos (cima, baixo, esquerda, direita).
   * A capa em foco ganha destaque/borda e efeito de escurecimento com dois botões centrais sobrepostos:
     * **INICIAR**
     * **CONFIGURAR**
3. **Ações Diretas sem Clique:**
   * Estando com o foco em um jogo, o usuário **não precisa clicar com o mouse**:
     * Botão **`A`**: Dispara diretamente a ação **INICIAR**.
     * Botão **`Y`**: Dispara diretamente a ação **CONFIGURAR**.
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

## 5. Resumo do Fluxo

```
[UI Principal]
       │
       ├── (Botão X ou Clique) ──► Alterna visual: Gamepad Debugger ◄──► Grid de Games
                                                                           │
       ┌───────────────────────────────────────────────────────────────────┘
       ▼
 [Grade de Jogos]
       ├── D-Pad: Navega entre as capas
       ├── Botão A: Executa o TargetPath (INICIAR)
       ├── Botão Y: Abre Painel de Configurações do Jogo
       └── Slot [+]: Adiciona novo jogo
             │
             ▼
 [Configuração do Jogo]
       ├── Nome do Game
       ├── Nome do Launcher (Opcional)
       ├── Nome do Executável Principal (para monitoramento de foco)
       ├── Destino (TargetPath)
       ├── Iniciar Em (WorkingDirectory)
       ├── Opções de Inicialização (Args)
       └── Botão para trocar Cover Art
```
