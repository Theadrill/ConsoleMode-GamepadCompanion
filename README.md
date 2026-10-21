# ConsoleMode - Gamepad Companion

App companion para substituir o Steam Input e usar o gamepad com o addon ConsoleMode-Vanilla.
Desenvolvido em C# (.NET Framework 4.8) para Windows, leve, 100% portátil e com inicialização direta na bandeja do sistema (system tray).

## ✨ Funcionalidades Principais
- **Mapeamento Couch Gaming de Baixa Latência:** Poll de entrada a ~60 FPS compatível com XInput e controllers Xbox/PlayStation.
- **Smart Mouse Look:** Integração automática de controle de câmera via analógico direito com alternância F9.
- **Biblioteca de Jogos Integrada:**
  - Navegação 100% nativa por controle (D-Pad, analógicos, atalhos A/B/X/Y e gatilhos).
  - Pesquisa rápida de títulos em tempo real.
  - Integração com a API do **SteamGridDB** para download instantâneo de capas em alta resolução.
  - Geração automática de **Hero Backgrounds Widescreen** com desfoque atmosférico e vinheta suave.
  - Teclado Virtual integrado com prevenção de missclicks, suporte a controle e digitação direta por teclado físico.
  - Persistência portátil de configurações e jogos via `config.ini` e `games.ini`.
- **Visual Debugger:** Visualização gráfica em tempo real dos botões, analógicos com zonas mortas calibráveis e sensibilidade de gatilhos.

## 🗺️ Roadmap & Próximos Passos (TODO)
- [x] Mapeamento Gaming 100% idêntico ao layout `Octowow` do Steam Input (com Smart Mouse Look F9 e cliques L3/R3)
- [x] Visual Gamepad Debugger em tempo real com sliders de Deadzone e Gatilho
- [x] Persistência local em arquivos `.ini` e ciclo de vida em segundo plano na bandeja (System Tray)
- [x] Biblioteca de Games (Launcher com navegação 100% por gamepad, configuração de executáveis, SteamGridDB, hero backgrounds e teclado virtual)
- [x] Fase 1 dos Perfis: Motor de Perfis por Jogo, Limpeza da Barra Lateral e Toggle de Input do Companion por Jogo
- [ ] Fase 2 dos Perfis: Tela do Editor Visual de Perfil (`GameProfileEditorControl`) com diagrama do controle e slots
- [ ] Fase 3 dos Perfis: Tela de Escolha de Edição & Captura Física de Combos
- [ ] Fase 4 dos Perfis: Seletor Visual em 3 Abas (`Teclado` | `Mouse` | `Sistema & Especial`)
- [ ] **Toggle de Múltiplas Instâncias por Jogo (`Permitir Múltiplas Instâncias`):**
  - Adicionar no `GameConfigPanel` um botão toggle idêntico ao do Input (`[ (●) PERMITIR MÚLTIPLAS INSTÂNCIAS / (○) INSTÂNCIA ÚNICA (Padrão) ]`).
  - Se estiver em **Instância Única (Desativado - Padrão)**: ao acionar `[A] INICIAR`, caso o executável principal já esteja em execução, em vez de abrir outro processo, o app traz a janela do jogo já existente para primeiro plano e oculta a interface do Companion.
  - Se estiver ativado: permite abrir múltiplas instâncias em paralelo.
- [ ] Overlay estilo Steam acionado pelo botão central do controle (Guide/Home)
- [ ] Perfil Desktop para usar o gamepad no Windows normalmente
- [ ] Sistema de Páginas / Camadas por Modificador (alternância de páginas de mapeamento dentro do jogo com sensibilidade própria de mouse)
