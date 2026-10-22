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
4. **Representação Visual do Visual Debugger (Tela Inicial) & Remap de Hardware:** Silhueta de Handheld no Visual Debugger com tela, dual trackpads vivos e botões traseiros (`[ L4 ] [ L5 ]   [ R5 ] [ R4 ]`), com remap em nível de driver para botões defeituosos (ex: L4 -> L1).
5. **Reestruturação da Tela de Mapping para Handheld:** Reorganizar a tela de configuração de controles (`GameProfileEditorControl`) para acomodar os novos controles, botões traseiros no rodapé e placeholders para os trackpads, mantendo comutação para gamepad padrão.
6. **Configuração Expandida dos Trackpads:** Brainstorming e especificação dedicada.

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
  3. `SteamDeckControllerService`: Thread de alta prioridade com desativação do Lizard Mode via feature reports e restauração limpa ao fechar.
  4. `SteamDeckManager`: Ciclo de vida global unificado no companion.

---

### FASE 4: Visual Debugger Handheld na Tela Inicial & Remap de Hardware no Driver ✅ (CONCLUÍDO)
* **Objetivo:** Atualizar o demonstrador ao vivo (`GamepadVisualDebugger`) para a silhueta de Handheld e viabilizar a substituição direta de botões físicos via driver.
* **Tarefas Concluídas:**
  1. `ControllerVisualMode` e `ControllerVisualModeResolver`: Comutação dinâmica entre `HandheldSteamDeck` e `StandardGamepad`.
  2. `GamepadVisualDebugger`: Carcaça ergonômica de Handheld com tela central, dual trackpads vivos (toque + clique) e linha horizontal de botões traseiros (`[ L4 ] [ L5 ]   [ R5 ] [ R4 ]`).
  3. Isolamento completo de hardware entre o botão `STEAM` (Guide) e o botão `•••` (Quick Access).
  4. Remap de hardware no driver (`SteamDeckControllerService`): `L4Remap`, `L5Remap`, `R4Remap`, `R5Remap` persistidos em `Config.ini`.

---

### FASE 5: Reestruturação da Tela de Mapping para Acomodar Controles Handheld 🚀 (EM ANDAMENTO)
* **Objetivo:** Reorganizar a tela de configuração de controles (`GameProfileEditorControl`) para acomodar com elegância os novos controles Handheld:
  1. **Correção de Bounds (`EnsureLogicalBoundsPopulated`):** Registrar formalmente os limites dos novos botões traseiros no dicionário de bounds para eliminar qualquer `KeyNotFoundException`.
  2. **Reestruturação do Layout:** Reorganizar os cards e áreas da tela quando em modo `HandheldSteamDeck` para refletir o formato de console portátil (sticks superiores, D-Pad e face buttons, linha unificada inferior de botões traseiros `L4, L5, R5, R4`).
  3. **Placeholders de Trackpads:** Inserir placeholders visuais/cards de reserva para os trackpads esquerdo e direito, preparando o terreno visual para a configuração expandida.
  4. **Comutação Dinâmica:** Garantir que ao alternar para um controle tradicional, o layout retorne perfeitamente ao formato de Gamepad clássico.

---

### FASE 6: Configuração dos Trackpads Expandida 💡 (PLANEJADA)
* **Observação:** Ao chegar nessa fase, iremos fazer um brainstorming focado exclusivamente nela para desenhar e implementar a melhor arquitetura de configuração dos trackpads (modos mouse, scroll, cliques e menus radiais).

---

## 🛑 Critérios de Aceite & Validação da Fase 5
1. Abrir o Editor de Perfis de qualquer jogo no Steam Deck sem nenhum erro de exceção ou crash de dicionário.
2. Visualizar a tela de mapping acomodando com clareza os novos botões traseiros e os placeholders dos trackpads.
3. Navegar suavemente com o D-Pad até a linha de botões traseiros (`L4, L5, R5, R4`), mapear um botão e salvar com sucesso.
4. Alternar o slot para controle comum e ver a tela de mapping voltar à distribuição clássica.
