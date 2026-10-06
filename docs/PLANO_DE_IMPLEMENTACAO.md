# Plano de Implementação — ConsoleMode-GamepadCompanion

Este documento define as fases atômicas de desenvolvimento do **ConsoleMode-GamepadCompanion**, estruturadas para garantir validação prática a cada entrega antes de avançar.

---

## Metodologia & Regras de Execução

1. **Parada Obrigatória:** Ao final de cada fase, a compilação deve estar limpa (0 erros e 0 warnings), e a IA para obrigatoriamente para o usuário testar com o controle em mãos.
2. **Autonomia de Processos:** Antes de qualquer compilação (`dotnet build`), o agente encerra preventivamente o processo do app caso esteja rodando.
3. **Autonomia de Logs:** Todo diagnóstico, teste ou leitura de logs é feito diretamente pelo agente via terminal/arquivos.
4. **Sem Commits/Push Prematuros:** Commitar apenas após validação aprovada pelo usuário. Push apenas sob solicitação explícita.
5. **Clean Code & Padrões:** Seguir as diretrizes das skills locais em `.agent/skills/` (`dotnet-pinvoke`, `csharp-pro`, `clean-code`, `ui-ux-pro-max`).

---

## 🚀 Fases de Implementação

### Fase 1: Scaffolding Modular & Camada de Hardware (XInput)
* **Objetivo:** Criar a estrutura limpa da solução e implementar a captura confiável dos controles via XInput P/Invoke.
* **Tarefas:**
  - Criar a solução e projeto `ConsoleMode-GamepadCompanion.csproj` (.NET Framework 4.8 / WinForms nativo).
  - Criar estrutura de diretórios: `Core/`, `Hardware/`, `Engine/`, `Profiles/`, `UI/`.
  - Implementar interfaces desacopladas em `Core/Interfaces/` (`IGamepadService`, `IInputSimulator`, etc.).
  - Implementar `Hardware/XInputNative.cs` com suporte a `xinput1_4.dll` e leitura estendida para botão Guide (`XInputGetStateEx`).
  - Implementar `Hardware/GamepadService.cs` com polling thread de alta precisão (120Hz-250Hz) e auto-detecção dos 4 slots de controle.
* **Critério de Aceite & Validação do Usuário:**
  - Projeto compila com 0 erros e 0 warnings.
  - Execução inicial reporta no terminal/console os controles conectados e a leitura em tempo real dos botões e sticks analógicos ao mexer no controle real.

---

### Fase 2: Visual Debugger do Gamepad & Janela de Configurações
* **Objetivo:** Fornecer uma interface gráfica rica onde o usuário consiga enxergar seu controle e ver os botões acendendo e sticks se movendo ao vivo.
* **Tarefas:**
  - Criar a janela `UI/SettingsForm.cs` com design limpo e moderno.
  - Implementar o componente `UI/Controls/GamepadVisualDebugger.cs`:
    - Representação visual clara do controle estilo Xbox / 8BitDo.
    - Iluminação em tempo real dos botões faciais (A, B, X, Y), D-Pad, Bumpers (LB, RB) e Menu (Start, Back, Guide).
    - Indicadores analógicos dos gatilhos (LT e RT) e posições dos analógicos (L-Stick e R-Stick com círculo de deadzone).
  - Adicionar seletor dropdown para escolher entre os controles conectados (Jogador 1 a 4).
  - Adicionar controle de slider de sensibilidade do mouse e botão de toggle Ativar/Pausar.
* **Critério de Aceite & Validação do Usuário:**
  - O usuário abre o app e vê o painel do controle.
  - Ao apertar qualquer botão ou mover qualquer analógico no 8BitDo/Xbox, a interface reflete a ação instantaneamente na tela.

---

### Fase 3: Motor de Emulação de Entrada (SendInput & Curva de Mouse)
* **Objetivo:** Implementar o envio de comandos de teclado e movimentação de mouse ultra-suave via Win32 `SendInput`.
* **Tarefas:**
  - Implementar `Hardware/SendInputNative.cs` (marshaling seguro de `INPUT`, `KEYBDINPUT`, `MOUSEINPUT`).
  - Implementar `Engine/MouseCurveCalculator.cs`:
    - Deadzone circular precisa.
    - Curva exponencial com aceleração suave (potência ~2.0) para emulação do analógico direito como mouse.
  - Implementar `Hardware/InputSimulator.cs` para despacho de teclas (KeyDown, KeyUp) e deltas de mouse (MouseMove, RightClick).
  - Implementar testes unitários para validar a matemática do `MouseCurveCalculator` (garantia de zero drift).
* **Critério de Aceite & Validação do Usuário:**
  - Ao mover o analógico direito, o cursor do mouse no Windows se move com suavidade e precisão de videogame moderno.
  - Clicar o R3 emite um clique do botão direito do mouse.

---

### Fase 4: Perfil Gaming Octowow & Mecânica do Smart Mouse Look (`F9`)
* **Objetivo:** Implementar o mapeamento completo do layout de combate do ConsoleMode e a lógica do `F9`.
* **Tarefas:**
  - Implementar `Profiles/GamingProfile.cs` com todas as ações mapeadas:
    - **Botões Faciais:** A (Espaço + `F9`), B (`3`), X (`1`), Y (`2`).
    - **D-Pad:** Cima (`7`), Baixo (`8`), Esquerda (`9`), Direita (`0`).
    - **Bumpers & Triggers:** LB (`Tab`), RB (`Ctrl`), LT (`Shift` digital), RT (`Alt` digital).
    - **Menus:** Select (`M`), Start (`F11`).
  - Implementar a mecânica do Smart Mouse Look (`Engine/SmartMouseLookHandler.cs`):
    - Analógico esquerdo (WASD) ou botão A (Espaço): envia `F9 Down` ao sair da deadzone e `F9 Up` ao retornar à deadzone.
* **Critério de Aceite & Validação do Usuário:**
  - O usuário testa no WoW (ou ferramenta de teste de teclado):
    - Mover o analógico esquerdo anda em WASD e ativa a câmera Mouselook contínua no WoW.
    - Botões disparam exatamente os números e comandos esperados.
    - Gatilhos LT e RT agem como teclas rápidas (Shift e Alt) para os combos de combate.

---

### Fase 5: Detecção de Janela, System Tray & Overlay Guide
* **Objetivo:** Integrar o ciclo de vida completo do aplicativo com auto-pause fora do jogo, minimização para a bandeja e acionamento via botão central.
* **Tarefas:**
  - Implementar `Hardware/WindowTracker.cs`: monitora o processo ativo na tela (`turtle-wow.exe` e `WoW.exe`).
  - Implementar `Engine/ProfileManager.cs`:
    - WoW em foco -> perfil **Gaming** ativo.
    - Outra janela -> perfil **Desktop** (silencioso por padrão, sem enviar teclas indesejadas no Windows).
  - Implementar `UI/TrayAppContext.cs`:
    - Ícone no System Tray (ao lado do relógio do Windows).
    - Clique no ícone abre a janela de configurações.
    - Fechar a janela apenas esconde o formulário, mantendo o serviço vivo na bandeja.
  - Implementar Toggle via Botão Guide/Home:
    - Pressionar o botão central do controle abre/fecha a janela de configurações por cima da tela.
* **Critério de Aceite & Validação Final:**
  - Dar Alt+Tab pausa o envio de inputs no Windows.
  - Voltar para o WoW reativa tudo automaticamente.
  - Pressionar o botão central do controle abre e fecha o painel de configurações.
  - Experiência completa 100% independente do Steam Input.
