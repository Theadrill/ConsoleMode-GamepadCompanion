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

## 🗺️ Roadmap
- [x] Mapeamento Gaming 100% idêntico ao layout `Octowow` do Steam Input (com Smart Mouse Look F9 e cliques L3/R3)
- [x] Visual Gamepad Debugger em tempo real com sliders de Deadzone e Gatilho
- [x] Persistência local em arquivos `.ini` e ciclo de vida em segundo plano na bandeja (System Tray)
- [x] Biblioteca de Games (Launcher com navegação 100% por gamepad, configuração de executáveis, SteamGridDB, hero backgrounds e teclado virtual)
- [ ] Overlay estilo Steam acionado pelo botão central do controle (Guide/Home)
- [ ] Perfil Desktop para usar o gamepad no Windows normalmente
