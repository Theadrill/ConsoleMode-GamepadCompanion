# Plano de Implementação: Suporte Nativo ao Steam Deck (Windows 10/11)

**Projeto:** `ConsoleMode-GamepadCompanion`  
**Escopo:** Suporte completo de hardware, captura de entrada HID, virtualização X360, UX de onboarding e representação visual modular de Handheld.  
**Referências e Créditos:** [Steam-Deck-Tools](https://github.com/mKenfenheuer/steam-deck-tools) (licença GPL-3.0) por mKenfenheuer e [ViGEmClient](https://github.com/nefarius/ViGEmClient) por Nefarius.

---

## 🧭 Visão Geral e Arquitetura

O Steam Deck executando Windows não possui drivers oficiais de gamepad para o hardware embutido (*Valve Neptune Controller*). Ao ligar o aparelho no Windows, a controladora HID inicia em **Lizard Mode** (emulando um mouse e teclado básicos no desktop).

Para transformá-lo em um controle de console transparente e de alta performance integrado ao ecossistema do `ConsoleMode-GamepadCompanion`:
1. **Identificação de Hardware:** Detectar se a máquina anfitriã é um Steam Deck (LCD ou OLED) via WMI (`Win32_ComputerSystem` / `Win32_BaseBoard`).
2. **Onboarding & Driver Virtual ViGEmBus:** Verificar se o driver `ViGEmBus` está instalado e ativo no sistema. Se não estiver, apresentar um aviso amigável na área de conteúdo (sem popups intrusivos) com botão de instalação silenciosa em segundo plano.
3. **Serviço de Entrada Direta HID & Gamepad Virtual:** Conectar diretamente ao dispositivo USB HID (`0x28DE:0x1205`), desativar o Lizard Mode via relatórios de recurso (*feature reports*), ler pacotes brutos de 64 bytes em alta taxa e alimentar um Gamepad Virtual Xbox 360 no Windows usando `ViGEmClient`. Ao fechar ou pausar o companion, reativar o Lizard Mode para manter a navegação padrão do Windows.
4. **Representação Visual de Handheld Completo:** Adaptar o `GamepadVisualDebugger` (tela inicial) e o `GameProfileEditorControl` (editor de mapeamento de perfis) para exibir a silhueta de um Handheld com tela central, dual trackpads e a linha horizontal unificada de botões traseiros (`[ L4 ] [ L5 ]   [ R5 ] [ R4 ]`), comutando automaticamente para o gamepad clássico quando um controle padrão for selecionado.
5. **Remap de Hardware em Nível de Driver:** Permitir substituição direta de botões (ex: contornar um `L1` físico quebrado remapeando-o para o `L4`), funcionando instantaneamente tanto em jogos que leem o XInput/ViGEm quanto no motor de atalhos e combos do Companion.

---

## 📋 Fases de Execução

### FASE 1: Detecção de Hardware (Steam Deck) & Verificação do Driver ViGEmBus ✅ (CONCLUÍDO)
* **Objetivo:** Identificar se o Windows está rodando em um Steam Deck e verificar se o driver do virtual gamepad (`ViGEmBus`) está instalado e em execução no sistema operacional.
* **Tarefas Concluídas:**
  1. `SteamDeckDetector`: Detecção via WMI e BIOS de `Valve Corporation` e produtos `Jupiter` (LCD) / `Galileo` (OLED).
  2. `ViGEmBusService`: Verificação da presença de `ViGEmBus.sys` e do serviço do Windows `ViGEmBus`.
  3. `SteamDeckDetectionTests`: 100% dos testes unitários passando.

---

### FASE 2: Interface de Onboarding & Instalador Silencioso em Background ✅ (CONCLUÍDO)
* **Objetivo:** Quando detectado que é um Steam Deck e o driver ViGEmBus NÃO estiver instalado, exibir um aviso modal incorporado na área de conteúdo ao lado da sidebar com instalação automatizada.
* **Tarefas Concluídas:**
  1. `ViGEmBusInstaller`: Execução silenciosa do instalador offline (`/quiet /norestart`) e suporte a download automático com fallback.
  2. `SteamDeckSetupControl`: Interface gráfica escura, com badge de alerta, barra de progresso, botão de instalação rápida e navegação Couch Gaming via gamepad.

---

### FASE 3: Captura Direta USB HID, Desativação do Lizard Mode & Emulação de Gamepad Virtual ✅ (CONCLUÍDO)
* **Objetivo:** Abrir a interface de controle do Steam Deck (`0x28DE:0x1205`), desativar a emulação de teclado/mouse nativa (Lizard Mode), ler os pacotes de 64 bytes em tempo real e alimentar um Gamepad Virtual Xbox 360.
* **Tarefas Concluídas:**
  1. `VirtualXbox360Controller`: Conexão, desconexão e despacho de relatórios do virtual Xbox 360 via `ViGEmClient`.
  2. `SteamDeckInputPacket`: Decodificação completa dos botões (ABXY, D-Pad, L1/R1, L2/R2, L3/R3, L4/L5/R4/R5, View/Menu/Steam/QuickAccess) e analógicos/trackpads.
  3. `SteamDeckControllerService`: Thread de alta prioridade com desativação do Lizard Mode via feature reports (`0x87, 0x03, 0x08, 0x07` e `0x81, 0x00`) e restauração limpa ao fechar.
  4. `SteamDeckManager`: Ciclo de vida global unificado no companion.

---

### FASE 4: Representação Visual de Handheld (Visual Debugger & Editor de Mapeamento) & Remap de Hardware ✅ (CONCLUÍDO)
* **Objetivo:** Transformar a interface visual de demonstração e de mapeamento para exibir o design de um **Handheld completo** quando o controle ativo for o Steam Deck (ou portátil compatível), com trackpads e linha unificada de botões traseiros (`L4, L5, R4, R5`), comutando automaticamente para o diagrama clássico de Gamepad quando for conectado um controle comum, e suportar remapeamento de hardware em nível de driver.
* **Tarefas Concluídas:**
  1. **Detecção Dinâmica do Modo Visual (`ControllerVisualMode`):**
     - Enum `ControllerVisualMode`: `StandardGamepad`, `HandheldSteamDeck`.
     - `ControllerVisualModeResolver.Resolve(selectedSlot)` comuta automaticamente: se for Steam Deck ativo e slot automático/0 ➔ `HandheldSteamDeck`; se selecionado slot de controle externo ➔ `StandardGamepad`.
  2. **Atualização do `GamepadVisualDebugger`:**
     - Renderização da carcaça do handheld com bordas arredondadas ergonômicas e tela central com branding "STEAM DECK".
     - Dois trackpads interativos com visualização em tempo real de toque analógico (`TouchDot`) e clique (`Press`).
     - Linha horizontal única de botões traseiros no rodapé: `[ L4 ] [ L5 ]   [ R5 ] [ R4 ]` com iluminação em tempo real ao pressionar.
  3. **Atualização do `GameProfileEditorControl`:**
     - Diagrama central desenha o handheld estilizado com trackpads e carcaça quando em modo `HandheldSteamDeck`.
     - Linha horizontal de cards interativos para os 4 botões traseiros (`L4, L5, R5, R4`) navegáveis com D-Pad e clicáveis com mouse.
     - Suporte a mapeamento de teclas/mouse (`InputAction`) e exibição de badge `[HW: {btn}]` quando configurado remap de hardware.
  4. **Remap de Hardware em Nível de Driver (`SteamDeckControllerService`):**
     - Propriedades `L4Remap`, `L5Remap`, `R4Remap`, `R5Remap` (`SteamDeckHardwareRemap`).
     - Aplica substituição de botões físicos (ex: `L4 -> LeftShoulder` para contornar L1 quebrado) sintetizando no relatório enviado ao ViGEm e emitido para o Windows.
  5. **Integração com `GamingProfile`:**
     - Botões `L4, L5, R4, R5` avaliados em cada frame para acionar atalhos de perfis customizados por jogo.
  6. **Persistência via `ConfigRepository`:**
     - Salva e carrega configurações de remapeamento de hardware em `Config.ini` sob a seção `[SteamDeck]`.
  7. **Testes Unitários:**
     - Adicionada suíte `SteamDeckVisualAndHardwareRemapTests` cobrindo o resolvedor dinâmico, remapeamento físico de hardware, visibilidade e manipulação dos slots traseiros no editor, isolamento do botão Quick Access (...) e persistência de configuração. Total de testes da solução elevado para 189 (100% aprovados).

---

## 🛑 Validação da Fase 4
1. **Visual Debugger Handheld:** Tela inicial do app no Steam Deck renderiza o layout de console portátil com tela, analógicos, D-Pad, ABXY, trackpads e os botões traseiros `[ L4 ] [ L5 ]   [ R5 ] [ R4 ]`.
2. **Feedback em Tempo Real:** Ao tocar ou pressionar os trackpads ou os botões traseiros físicos, a interface reage e acende instantaneamente.
3. **Editor de Perfis:** Abre perfis de jogos exibindo o layout Handheld e a linha de botões traseiros interativos, permitindo navegação via D-Pad ou clique para mapeamento.
4. **Remap de Hardware para L1 Quebrado:** Configurando `L4HardwareRemap = LeftShoulder`, ao pressionar L4 o driver emite `LeftShoulder` (LB/L1) para qualquer jogo do Windows.
5. **Comutação Dinâmica:** Selecionando um controle externo no dropdown da sidebar, a interface alterna instantaneamente para o diagrama clássico de Gamepad.
