> [!IMPORTANT]
> **LEITURA OBRIGATÓRIA ANTES DE INICIAR QUALQUER EXECUÇÃO:** É estritamente obrigatório ler os documentos [`docs/REGRAS_DO_PROJETO.md`](./REGRAS_DO_PROJETO.md) e [`docs/BRAINSTORMING.md`](./BRAINSTORMING.md) antes de iniciar ou retomar qualquer fase deste plano.

# Plano de Implementação — Suporte Nativo ao Steam Deck & Handhelds no Windows

> **Documento de Arquitetura Técnica, Identidade Visual, UX e Roteiro de Fases Testáveis**  
> Implementação do sistema de reconhecimento de hardware da Valve (Steam Deck LCD / OLED), detecção e instalação assistida em segundo plano do driver de gamepad virtual (**ViGEmBus**), captura direta USB HID do controle Neptune, controle de *Lizard Mode*, representação visual dinâmica de Handheld (com trackpads e linha modular de botões traseiros **L4, L5, R4, R5** no Visual Debugger e no Editor de Perfis) e remapeamento de botões físicos.

---

## 1. Metodologia & Regras de Execução

1. **Parada Obrigatória por Fase:** Ao final de cada fase, a compilação deve estar limpa (`0 erros` e `0 warnings`), todos os testes unitários devem passar, e o desenvolvimento pausa obrigatoriamente para o usuário testar na tela com o controle em mãos.
2. **Nenhuma Fase Invisível:** Todas as fases entregam resultado visual e interativo testável diretamente no aplicativo.
3. **Aproveitamento Total da Área de Conteúdo (`Dock = DockStyle.Fill`):** Nenhuma tela do fluxo abre janelas externas (`Form.ShowDialog`) ou popups flutuantes fora do app. Todas as telas alternam suavemente dentro da área principal de conteúdo.
4. **Autonomia de Processos:** Antes de compilar (`dotnet build`), o agente encerra preventivamente qualquer instância em execução do `ConsoleMode-GamepadCompanion`.
5. **Sem Commits/Push Prematuros:** Commits e push somente após validação e solicitação explícita do usuário.
6. **Representação Visual Adaptativa (Handheld vs Gamepad Tradicional):**
   - Se o controle ativo for o controle embutido do Steam Deck (ou handheld compatível), renderiza o chassi de Handheld com tela central, os dois trackpads e a linha inferior unificada de botões traseiros (`[ L4 ] [ L5 ]   [ R5 ] [ R4 ]`).
   - Se o controle ativo for um gamepad externo conectado (ex: controle de Xbox via Bluetooth/USB plugado no dock, ou rodando em PC comum), comuta automaticamente para o diagrama clássico de Gamepad (ocultando trackpads e botões traseiros).

---

## 2. Identidade Visual & Wireframes das Telas Handheld

### 2.1. Visual Debugger em Modo Handheld (`GamepadVisualDebugger`)
```text
+---------------------------------------------------------------------------------------------+
|   VISUAL DEBUGGER — STEAM DECK                                                              |
|---------------------------------------------------------------------------------------------|
|      [ LT: 0% ]   [ LB ]                                           [ RB ]   [ RT: 0% ]      |
|                                                                                             |
|      +-------------------------------------------------------------------------------+      |
|     /  (View)                         STEAM DECK                         (Menu)       \     |
|    |   .---.                                                                 (Y)       |    |
|    |  / [L] \          +-----------------------------------+              (X)   (B)    |    |
|    |  \     /          |                                   |                 (A)       |    |
|    |   `---'           |                                   |                           |    |
|    |                   |           TELA CENTRAL            |                  .---.    |    |
|    |     [↑]           |                                   |                 / [R] \   |    |
|    |  [←] + [→]        |                                   |                 \     /   |    |
|    |     [↓]           +-----------------------------------+                  `---'    |    |
|    |                                                                                   |    |
|    |  +-------------+      (Steam)                   (Quick...)       +-------------+  |    |
|    |  | TRACKPAD ESQ|                                                 | TRACKPAD DIR|  |    |
|    |  |  (Touch/Pr) |                                                 |  (Touch/Pr) |  |    |
|     \ +-------------+                                                 +-------------+ /     |
|      +-------------------------------------------------------------------------------+      |
|                                                                                             |
|            BOTÕES TRASEIROS:   [ L4 ]   [ L5 ]       [ R5 ]   [ R4 ]                        |
|                                                                                             |
|  Sensibilidade do Mouse: [=======O-------] 50    Deadzone: [====O----------] 24%            |
+---------------------------------------------------------------------------------------------+
```

### 2.2. Editor de Perfis em Modo Handheld (`GameProfileEditorControl`)
Os botões traseiros ficam em uma **linha única horizontal abaixo do controle**, tornando a arquitetura modular para qualquer handheld (ROG Ally com 2 botões, Legion Go, ou Steam Deck com 4):

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
|   [ LB: Tab   ]          +-----------------------------------+              [ RB: Ctrl ]    |
|   [ View: M   ]         /                                     \             [ Menu: F11 ]   |
|   +-------------------+ |          DIAGRAMA HANDHELD          | +-------------------------+ |
|   | STICK ESQUERDO    | |    (Sticks, D-Pad, ABXY, Trackpads) | | BOTÕES FACIAIS          | |
|   | Modo: 4 Direções  | |                                     | | (Y): [ 2 ]    (B): [ 3 ]| |
|   | Adicional: [ F9 ] | \                                     / | (X): [ 1 ]    (A): [Esp]| |
|   +-------------------+  +-----------------------------------+  +-------------------------+ |
|   +-------------------+                                         +-------------------------+ |
|   | D-PAD (SETAS)     |                                         | STICK DIREITO           | |
|   | Cima:7  Baixo:8   |                                         | Modo: Movimento Mouse   | |
|   | Esq: 9  Dir:  0   |                                         | Clique R3: [ Mouse R ]  | |
|   +-------------------+                                         +-------------------------+ |
|                                                                                             |
|   BOTÕES TRASEIROS:                                                                         |
|   [ L4: LB (Remap L1) ]   [ L5: Espaço ]           [ R5: Ctrl ]   [ R4: F9 ]                |
|                                                                                             |
|  (A) Editar Entrada Selecionada    (X) Limpar Entrada    (RT) Salvar Perfil    (B) Voltar   |
+---------------------------------------------------------------------------------------------+
```

---

## 3. Fases Testáveis Incrementais

---

### FASE 1: Detecção de Hardware Steam Deck & Verificação do Driver ViGEmBus
*(Concluída)*
- Identificação WMI/SMBIOS (`Valve Jupiter/Galileo`).
- Verificação do driver no sistema (`ViGEmBusService`).
- Badges no `SettingsForm` e 176 testes unitários aprovados.

---

### FASE 2: Painel Modal de Setup do Driver na Área de Conteúdo (`SteamDeckSetupControl`)
*(Concluída)*
- Painel Couch Gaming `SteamDeckSetupControl` preenchendo a área de conteúdo.
- Instalador offline oficial do ViGEmBus embutido em `redist/ViGEmBus_Setup.exe`.
- Instalação silenciosa assistida com elevação UAC em segundo plano sem reiniciar o Windows.
- 178 testes unitários aprovados.

---

### FASE 3: Motor de Leitura Direta HID da Valve & Gamepad Virtual X360
*(Concluída)*
- Integração nativa com `hidapi.dll` vinculada à raiz da aplicação.
- Desativação de Lizard Mode via Feature Reports USB com restauração automática ao fechar.
- Pipeline de polling de alta frequência alimentando o controle virtual Xbox 360 (`ViGEmClient`).
- 181 testes unitários aprovados e controle virtual respondendo no Windows.

---

### FASE 4: Representação Visual de Handheld (Visual Debugger & Editor de Mapeamento) & Remap de Hardware

* **Objetivo:** Transformar a interface visual de demonstração e de mapeamento para exibir o design de um **Handheld completo** quando o controle ativo for o Steam Deck (ou portátil compatível), com trackpads e linha unificada de botões traseiros (`L4, L5, R4, R5`), comutando automaticamente para o diagrama clássico de Gamepad quando for conectado um controle comum.
* **Tarefas:**
  1. **Detecção Dinâmica do Modo Visual (`ControllerVisualMode`):**
     - Criar enum `ControllerVisualMode`: `StandardGamepad`, `HandheldSteamDeck`.
     - Regra de seleção:
       - Se `SteamDeckDetector.IsSteamDeck && SteamDeckManager.IsActive && (_settings.SelectedSlot == -1 || _settings.SelectedSlot == 0)` ➔ `HandheldSteamDeck`.
       - Se o usuário selecionar um slot de gamepad externo (ex: Gamepad 2 ou PC Desktop) ➔ `StandardGamepad`.
  2. **Atualização do `GamepadVisualDebugger`:**
     - No modo `HandheldSteamDeck`:
       - Renderizar a carcaça de handheld com bordas arredondadas e tela central.
       - Renderizar os dois trackpads com feedback em tempo real de toque (`Touch`) e pressão (`Press`).
       - Renderizar a linha horizontal de botões traseiros no rodapé do controle: `[ L4 ] [ L5 ]   [ R5 ] [ R4 ]` acendendo em tempo real ao pressionar os botões físicos.
     - No modo `StandardGamepad`:
       - Manter a renderização clássica de controle Xbox.
  3. **Atualização do `GameProfileEditorControl`:**
     - No modo `HandheldSteamDeck`:
       - Renderizar o diagrama central em formato de Handheld.
       - Adicionar a linha horizontal de cards interativos para os 4 botões traseiros no rodapé (`L4`, `L5`, `R5`, `R4`), navegáveis com o D-Pad e clicáveis com o mouse.
       - Permitir selecionar qualquer botão traseiro com `(A)` para abrir a tela de mapeamento (`InputBindingCaptureControl` ou `InputVisualSelectorControl`), ou configurar Remap de Hardware.
  4. **Remap de Hardware em Nível de Driver (`SteamDeckControllerService`):**
     - Permitir configurar substituição direta de botões (ex: `L4 -> L1/LB` para contornar botão quebrado no hardware).
     - Quando o usuário apertar L4 físico, o emulador ViGEm emite `LeftShoulder` instantaneamente, funcionando em qualquer jogo.
  5. **Testes Unitários da Fase 4:**
     - Testes cobrindo comutação de modo visual, persistência dos mapeamentos de L4/L5/R4/R5 e regras de remap.

* **🛑 Critério de Aceite & Validação (Fase 4):**
  1. No Steam Deck, abrir a tela inicial do Companion e verificar o **Visual Debugger no visual de Handheld** com os trackpads e os botões `[ L4 ] [ L5 ]   [ R5 ] [ R4 ]`.
  2. Pressionar os botões traseiros físicos e os trackpads e ver cada um acender imediatamente na tela.
  3. Entrar no **Editor de Perfis** de um jogo e verificar a linha dos botões traseiros presente.
  4. Mapear o botão `L4` para `L1` (ou uma tecla customizada), salvar e validar a resposta instantânea.
  5. Plugar um controle externo ou alternar o slot de controle e ver a interface comutar de volta para o gamepad clássico.
