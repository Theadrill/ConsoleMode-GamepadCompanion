> [!IMPORTANT]
> **LEITURA OBRIGATÓRIA ANTES DE INICIAR QUALQUER EXECUÇÃO:** É estritamente obrigatório ler os documentos [`docs/REGRAS_DO_PROJETO.md`](./REGRAS_DO_PROJETO.md) e [`docs/BRAINSTORMING.md`](./BRAINSTORMING.md) antes de iniciar ou retomar qualquer fase deste plano.

# Plano de Implementação — Suporte Nativo ao Steam Deck no Windows

> **Documento de Arquitetura Técnica, Identidade Visual, UX e Roteiro de Fases Testáveis**  
> Implementação do sistema de reconhecimento de hardware da Valve (Steam Deck LCD / OLED), detecção e instalação assistida em segundo plano do driver de gamepad virtual (**ViGEmBus**), captura direta USB HID do controle Neptune, controle de *Lizard Mode* e suporte a remapeamento dos botões traseiros (**L4, L5, R4, R5**) aproveitando a área de conteúdo do **ConsoleMode-GamepadCompanion**.

---

## 1. Metodologia & Regras de Execução

1. **Parada Obrigatória por Fase:** Ao final de cada fase, a compilação deve estar limpa (`0 erros` e `0 warnings`), todos os testes unitários devem passar, e o desenvolvimento pausa obrigatoriamente para o usuário testar na tela com o controle em mãos.
2. **Nenhuma Fase Invisível:** Todas as fases entregam resultado visual e interativo testável diretamente no aplicativo.
3. **Aproveitamento Total da Área de Conteúdo (`Dock = DockStyle.Fill`):** Nenhuma tela do fluxo abre janelas externas (`Form.ShowDialog`) ou popups flutuantes fora do app. O painel de setup do Steam Deck ocupa suavemente a área de conteúdo ao lado da barra lateral.
4. **Autonomia de Processos:** Antes de compilar (`dotnet build`), o agente encerra preventivamente qualquer instância em execução do `ConsoleMode-GamepadCompanion`.
5. **Sem Commits/Push Prematuros:** Commits e push somente após validação e solicitação explícita do usuário.
6. **Portabilidade & Modo Offline:** Preferência por instalador embutido/redist local para que o Steam Deck funcione perfeitamente sem exigir conexão à internet.

---

## 2. Identidade Visual & Paleta de Cores do Painel

O painel de configuração do Steam Deck segue a identidade visual do app:
- **Fundo Principal:** `Color.FromArgb(24, 26, 32)` (`#181A20`)
- **Fundo do Card Central:** `Color.FromArgb(34, 37, 46)` (`#22252E`)
- **Bordas em Destaque:** `Color.FromArgb(120, 190, 255)` (`#78BEFF`)
- **Texto Principal:** `Color.FromArgb(230, 232, 240)` (`#E6E8F0`)
- **Texto Secundário / Explicação:** `Color.FromArgb(160, 170, 185)` (`#A0AAB9`)
- **Botão de Ação Primária:** Azul `#2563EB` com foco em `#3B82F6`
- **Indicador de Sucesso:** Verde `#78E696`

---

## 3. Desenho ASCII da Tela (Wireframe da Área de Conteúdo)

### 3.1. Painel de Boas-Vindas & Setup do Driver (`SteamDeckSetupControl`)
Exibido automaticamente na área de conteúdo ao lado da sidebar quando o app detecta que está rodando em hardware Steam Deck, mas o driver de gamepad virtual ainda não está instalado ou ativo:

```text
+------------------------+--------------------------------------------------------------------+
| CONTROLE ATIVO         |  STEAM DECK DETECTADO                                              |
| [ Automático       v ] |--------------------------------------------------------------------|
|                        |                                                                    |
| Status: Aguardando     |      +------------------------------------------------------+      |
| Perfil: N/A            |      |  📟 HARDWARE VALVE STEAM DECK RECONHECIDO            |      |
|                        |      |  Modelo: Valve Jupiter (Aerith)                      |      |
|                        |      |                                                      |      |
| [  GAMES / DEBUGGER  ] |      |  Para que os controles embutidos funcionem tanto     |      |
|                        |      |  no Companion quanto em jogos com suporte nativo,    |      |
| [        Sair        ] |      |  é necessário o driver de Gamepad Virtual (ViGEm).   |      |
|                        |      |                                                      |      |
|                        |      |  Status do Driver: [ ⚠️ NÃO INSTALADO ]             |      |
|                        |      |                                                      |      |
|                        |      |  +------------------------------------------------+  |      |
|                        |      |  | [⚡] INSTALAR DRIVER AUTOMATICAMENTE           |  |      |
|                        |      |  +------------------------------------------------+  |      |
|                        |      |  (Instalação silenciosa em background. Exige clique   |      |
|                        |      |   único de confirmação no UAC do Windows)            |      |
|                        |      +------------------------------------------------------+      |
|                        |                                                                    |
|                        |  (A) Confirmar Instalação                         (B) Ignorar      |
+------------------------+--------------------------------------------------------------------+
```

---

## 4. Fases Testáveis Incrementais

---

### FASE 1: Detecção de Hardware Steam Deck & Verificação do Driver ViGEmBus

* **Objetivo:** Implementar o motor de identificação de hardware da Valve (LCD e OLED) e o diagnóstico em tempo real do estado do driver de gamepad virtual ViGEmBus no sistema.
* **Tarefas:**
  1. **Serviço de Reconhecimento de Hardware (`Hardware/SteamDeckDetector.cs`):**
     - Checagem WMI/SMBIOS: `Manufacturer == "Valve"` e `Model` contendo `"Jupiter"` ou `"Galileo"`.
     - Checagem USB PnP: presença do dispositivo com `VendorID == 0x28DE` e `ProductID == 0x1205`.
     - Expor propriedade booleana `IsSteamDeck` e enum com a variante (`None`, `SteamDeckLcd`, `SteamDeckOled`).
  2. **Verificador de Driver (`Hardware/ViGEmBusService.cs`):**
     - Instanciação de teste do `Nefarius.ViGEm.Client.ViGEmClient`.
     - Tratamento específico de `VigemBusNotFoundException` e `VigemBusAccessDeniedException`.
     - Expor propriedade booleana `IsDriverInstalledAndActive`.
  3. **Integração Inicial no `SettingsForm`:**
     - Exibir na barra de status ou badge lateral o reconhecimento do dispositivo (ex: `"Dispositivo: Steam Deck (Jupiter)"`).
  4. **Testes Unitários da Fase 1:**
     - Testes simulando retornos de WMI e captura de exceções do driver sem crashar a aplicação.

* **🛑 Critério de Aceite & Validação (Fase 1):**
  1. Abrir o app no Steam Deck (com o driver ViGEm recém-desinstalado).
  2. Verificar nos logs ou na tela que o app identificou corretamente:
     - `IsSteamDeck = True` (`Valve Jupiter`)
     - `IsDriverInstalledAndActive = False`
  3. No PC tradicional, verificar que o app reporta `IsSteamDeck = False` e segue o fluxo comum sem nenhum impacto.

---

### FASE 2: Painel Modal de Setup do Driver na Área de Conteúdo (`SteamDeckSetupControl`)

* **Objetivo:** Apresentar a interface visual de setup na área de conteúdo quando o Steam Deck for detectado sem o driver, com botão para instalação silenciosa assistida em segundo plano (com redist embutido/local e fallback online).
* **Tarefas:**
  1. **Componente `UI/Controls/SteamDeckSetupControl.cs` (`Dock = DockStyle.Fill`):**
     - Layout estilizado conforme o wireframe com texto explicativo e status visual do driver.
     - Botão `[⚡ Instalar Driver Automaticamente]` operável tanto por clique de mouse/touch quanto por controle (após conectado) ou teclado (`Enter`).
     - Exibição de spinner/progresso animado durante o processo: *"Instalando driver ViGEmBus em segundo plano..."*.
  2. **Mecanismo de Instalação Silenciosa:**
     - Disponibilizar o instalador do ViGEmBus em pasta `redist/` ou recurso embutido (com fallback para download via GitHub Release ou `winget`).
     - Executar o instalador via processo elevado (`Verb = "runas"`) com argumentos silenciosos (`/quiet /norestart`).
     - Monitorar término do processo e revalidar imediatamente o `ViGEmBusService`.
  3. **Transição Automática:**
     - Assim que a instalação concluir com êxito, o painel muda o status para verde (*"Driver Instalado e Ativo!"*) e transiciona suavemente para a visualização normal da biblioteca após 1.5 segundo.

* **🛑 Critério de Aceite & Validação (Fase 2):**
  1. Abrir o app no Steam Deck limpo e constatar o painel de setup ocupando a área de conteúdo.
  2. Clicar em `[⚡ Instalar Driver Automaticamente]`.
  3. Aceitar o prompt do UAC do Windows (clique único em "Sim").
  4. Observar a tela informando a instalação em segundo plano sem abrir telas de instalador externo.
  5. Constatar a conclusão automática, validação do driver em tempo real e liberação da interface do Companion sem reiniciar o Windows.

---

### FASE 3: Motor de Leitura Direta HID da Valve & Gamepad Virtual X360

* **Objetivo:** Conectar diretamente à interface USB HID da Valve (`VID 0x28DE / PID 0x1205`), desativar o Lizard Mode enquanto o jogo/app estiver em foco, e alimentar o controle virtual de Xbox 360 via ViGEmBus.
* **Tarefas:**
  1. **Camada de Comunicação HID (`Hardware/SteamDeckHidNative.cs`):**
     - Localizar e abrir a interface HID do controle Neptune (`0x28DE:0x1205`).
     - Envio de Feature Reports para silenciar Lizard Mode:
       - Mouse Off: `{ 0x87, 0x03, 0x08, 0x07 }`
       - Buttons/Keyboard Off: `{ 0x81, 0x00 }`
     - Restauração automática de Lizard Mode ao fechar o app ou minimizar:
       - Mouse On: `{ 0x8e, 0x00 }`
       - Buttons/Keyboard On: `{ 0x85, 0x00 }`
  2. **Pipeline de Polling de Alta Frequência:**
     - Leitura assíncrona dos pacotes brutos de 64 bytes (analógicos, D-Pad, ABXY, bumpers, gatilhos analógicos).
  3. **Despacho para o Gamepad Virtual Xbox 360:**
     - Instanciar `IXbox360Controller` via `ViGEmClient`.
     - Submeter os estados de botões e analógicos a cada ciclo para o controle virtual.
     - O próprio `XInputNative` do Companion (e qualquer jogo de PC com suporte a controle) passa a reconhecer o controle nativamente no Jogador 1.

* **🛑 Critério de Aceite & Validação (Fase 3):**
  1. Abrir o app no Steam Deck (sem nenhum software externo rodando).
  2. Testar no Visual Debugger do Companion: constatar todos os botões e analógicos respondendo em tempo real.
  3. Abrir um jogo com suporte nativo a controle (ou testar via Gamepad Tester no navegador): constatar que o Windows reconhece um controle de Xbox 360 perfeito.

---

### FASE 4: Suporte aos Botões Traseiros (L4/L5/R4/R5), Remap de Hardware & Editor Visual

* **Objetivo:** Habilitar a personalização completa dos botões traseiros do Steam Deck tanto para remap de hardware de botões com defeito quanto para atalhos customizados do Companion.
* **Tarefas:**
  1. **Remap de Hardware (Substituição de Botões Físicos):**
     - Permitir configurar na UI regras diretas como `L4 -> L1/LB` ou `R4 -> R1/RB`.
     - Quando o usuário acionar o L4 físico, o emulador de Xbox 360 emite o botão correspondente, sanando eventuais falhas físicas de botões no aparelho.
  2. **Diagrama do Steam Deck no Editor de Perfis:**
     - Quando o app estiver em execução no Steam Deck, atualizar o diagrama visual do controle em GDI+ para incluir os cards dos botões traseiros (`L4`, `L5`, `R4`, `R5`) e Touchpads.
  3. **Comandos Customizados nos Traseiros:**
     - Permitir associar qualquer tecla, combo (`F9`, `Ctrl+Shift+S`, clique de mouse) diretamente aos botões traseiros no perfil de jogo do Companion.
  4. **Documentação & Validação Final:**
     - Atualizar o `README.md` com as instruções de uso no Steam Deck.

* **🛑 Critério de Aceite & Validação (Fase 4):**
  1. Configurar o botão `L4` para emular o `L1` (ou uma tecla customizada como `F9`).
  2. Apertar o botão traseiro físico no Steam Deck e validar o acionamento imediato no jogo e no visual debugger.
