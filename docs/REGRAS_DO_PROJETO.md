# Regras do Projeto — ConsoleMode-GamepadCompanion

> [!IMPORTANT]
> ## LEITURA OBRIGATÓRIA ANTES DE QUALQUER AÇÃO
> Todo agente que ler este documento **DEVE OBRIGATORIAMENTE LER TAMBÉM**:
> 1. [`docs/BRAINSTORMING.md`](docs/BRAINSTORMING.md) — Contém todo o mapeamento do layout Octowow, a arquitetura modular, a mecânica do Smart Mouse Look (`F9`), o ecossistema de skills e o roadmap do projeto.
> 2. [`docs/PLANO_DE_IMPLEMENTACAO.md`](docs/PLANO_DE_IMPLEMENTACAO.md) — Contém as 5 fases atômicas de implementação, critérios de aceite e procedimentos de teste com o controle.
> Não inicie nenhuma tarefa sem carregar estes documentos no contexto.


Este documento estabelece as **regras obrigatórias e inegociáveis** de governança, engenharia de software e operação dos agentes de IA para o repositório **ConsoleMode-GamepadCompanion**.

---


## 1. Regras Principais de Operação & Governança

### Regra 1: Push sob Demanda Estrita (A Mais Importante)
* **NUNCA** fazer push para o repositório remoto por conta própria ou de forma automática.
* Fazer push **EXATAMENTE UMA VEZ** apenas quando o usuário solicitar explicitamente ("faça o push", "pode subir", etc.).
* Após realizar o push solicitado, aguardar o próximo pedido explícito do usuário para qualquer novo push.

### Regra 2: Parada Obrigatória para Validação do Usuário
* Ao final de cada fase, funcionalidade ou correção, a IA deve **obrigatoriamente parar** e apresentar um resumo claro do que foi feito e como o usuário deve testar.
* Nenhum commit ou avanço para a próxima fase pode ser feito antes da aprovação explícita do usuário.

### Regra 3: Modo de Operação Tech Leader / Agentes
* A IA atua como **Tech Leader**: não realiza alterações caóticas simultâneas.
* Planeja em fases atômicas, delega/executa com precisão modular, valida sintaxe/build antes de entregar e orienta o que o usuário deve validar no controle.

### Regra 4: Sem Commits Prematuros
* Commits devem ser atômicos e realizados apenas após a funcionalidade estar concluída, sem erros de compilação e homologada pelo usuário.

---

## 2. Autonomia do Agente & Execução de Tarefas

### Regra 5: Autonomia de Processo e Build (Encerrar Processo Antes de Compilar)
* **PROIBIDO pedir para o usuário fechar o aplicativo para você compilar.**
* O agente tem acesso total ao terminal do Windows. Antes de qualquer comando de compilação (`dotnet build` / `msbuild`), o agente deve verificar se o processo do aplicativo (`ConsoleMode-GamepadCompanion` ou processo irmão) está rodando e **encerrá-lo de forma automática e proativa** via PowerShell (`Stop-Process -Name ... -ErrorAction SilentlyContinue` ou `taskkill /F /IM ...`).
* Somente após garantir que o arquivo binário está destravado, executar a compilação.

### Regra 6: Autonomia Total de Logs e Diagnóstico
* **PROIBIDO pedir para o usuário copiar, colar ou enviar logs.**
* O agente tem acesso total ao sistema de arquivos, ferramentas de visualização e terminal de comando.
* Qualquer erro, crash, falha de P/Invoke, leitura de XInput ou depuração deve ser investigada pelo próprio agente lendo os arquivos de log no disco, consultando o Event Viewer do Windows ou disparando comandos de verificação diretamente.

### Regra 7: Compilação Limpa Obrigatória (Zero Erros e Zero Warnings)
* O projeto deve compilar com **0 erros e 0 warnings** (`dotnet build -warnaserror` ou compilação limpa).
* Nenhum código quebrado ou com pendência de build pode ser entregue para o usuário testar.

---

## 3. Qualidade de Código, Skills e Arquitetura

### Regra 8: Consulta Obrigatória às Skills do Projeto
* Antes de implementar ou refatorar qualquer módulo, o agente deve consultar e seguir os padrões das **skills locais** armazenadas em `.agent/skills/`:
  * `dotnet-pinvoke`: Para chamadas seguras da Win32 API (`SendInput`, `user32.dll`) e XInput (`xinput1_4.dll`).
  * `csharp-pro` e `clean-code`: Padrões modernos de C#, tipagem forte, nomenclatura e separação de responsabilidades.
  * `csharp-refactoring`: Diretrizes de refatoração contínua.
  * `analyzing-dotnet-performance` e `microbenchmarking`: Garantia de alta frequência (polling 120Hz-250Hz) sem alocações desnecessárias ou spikes de GC.
  * `coding-guidelines`: Padrões de código corporativo e robustez.
  * `ui-ux-pro-max`: Diretrizes visuais para a janela de configurações e overlay.
  * `run-tests`: Práticas de validação e testes automatizados de matemática e regras de negócio.

### Regra 9: Arquitetura Modular e Clean Code (Zero Monólito)
* **PROIBIDO criar classes ou formulários monolíticos** com milhares de linhas acumulando responsabilidades (ex: WinForms com P/Invoke + matemática + UI misturados).
* O projeto deve seguir estritamente o princípio da responsabilidade única (SRP) e separação de camadas:
  1. `Core/`: Interfaces, DTOs e modelos desacoplados.
  2. `Hardware/`: Acesso a hardware e SO via P/Invoke (XInput e SendInput isolados).
  3. `Engine/`: Matemática pura (deadzone circular, aceleração de mouse, detecção de F9) sem dependência de UI.
  4. `Profiles/`: Implementação de perfis (Gaming vs Desktop).
  5. `UI/`: Apresentação visual leve (System Tray e janelas desacopladas).

### Regra 10: Mecânicas de Mapeamento Intocáveis
* Não regredir os comportamentos de gameplay consolidados:
  * Smart Mouse Look Companion (`F9` sincronizado com WASD e Espaço/A).
  * Emulação de mouse suave com curva exponencial e deadzone circular no analógico direito.
  * Gatilhos analógicos LT e RT agindo como botões digitais instantâneos.
  * Botão central (Guide / Home / 8BitDo) como atalho do overlay de configurações.

### Regra 11: Independência de Localização
* Todas as strings de interface e mensagens de usuário devem ficar centralizadas em constantes/dicionários de localização, desacopladas da lógica de negócio.
