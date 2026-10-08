# Plano de Implementação — Integração SteamGridDB (Capas Automáticas)

> **Documento de Arquitetura Técnica, UX e Roteiro de Validação Interativa**  
> Integração completa com o serviço [SteamGridDB](https://www.steamgriddb.com) para pesquisa, seleção em 2 etapas (Lista de Jogos ➔ Grade de Artes) e download de capas verticais estilo console diretamente na interface Couch Gaming do ConsoleMode-GamepadCompanion.

---

## 1. Visão Geral da Experiência do Usuário (UX) & Reuso de Código

O usuário poderá buscar e aplicar qualquer capa do SteamGridDB sem sair do sofá, utilizando exclusivamente o controle ou o mouse.

### Princípios Chave de Arquitetura & UX:
1. **Fase 100% Interativa e Visual:** Nenhuma fase fragmentada sem resultado visível. O usuário deve ser capaz de abrir o app, clicar, testar no controle e validar na tela.
2. **Regra de Ouro do Reuso:** O mesmo modal de chave de API (`SteamGridApiKeyDialog`) é reutilizado na primeira configuração e no botão *"🔑 Trocar Chave API"*.
3. **Desempenho Instantâneo:** A **Tela 1** é uma **Lista Vertical de Jogos** (resposta em <100ms em 1 única requisição leve). Evita lentidão e riscos de rate-limit.
4. **Compatibilidade GDI+ (.NET 4.8):** A API busca estritamente formatos nativos compatíveis (`mimes=image/png,image/jpeg`), garantindo estabilidade nativa no Windows Forms sem bibliotecas C++ externas para WebP.
5. **Validação Ativa de Chave:** A chave colada ou digitada é testada em tempo real com status visual (🟡 Checando ➔ 🟢 Válida / 🔴 Inválida). Se inválida, **não salva**.

### Fluxo de Navegação em 2 Etapas:
```mermaid
flowchart TD
    A["GameConfigPanel\n(Linha da Capa Redesenhada)"] -->|Clica [SteamGridDB]| B{"Possui Chave Válida\nno config.ini?"}
    B -->|Não| C["Modal Reutilizável de Chave de API\n(Validação em tempo real: 🟡 ➔ 🟢/🔴)"]
    C -->|Salva apenas se Válida| D["Modal de Termo de Busca\n(Nome do Jogo + Botão Trocar Chave)"]
    B -->|Sim| D
    D -->|Clica [Trocar Chave]| C
    D -->|Confirma Busca| E["Tela 1: Lista de Jogos Encontrados\n(Lista Vertical Instantânea <100ms)"]
    E -->|Pressiona B ou Voltar| A
    E -->|Seleciona Jogo (A ou 1 clique)| F["Tela 2: Grade de Artes do Jogo\n(Capas Verticais 600x900 PNG/JPEG)"]
    F -->|Pressiona B ou Voltar| E
    F -->|Seleciona Capa (A ou 1 clique)| G["Download em Alta Resolução\n(Salva em covers/ e aplica no formulário)"]
    G --> A
```

---

## 2. Detalhamento dos Componentes de Interface (UI)

### 2.1. Redesenho da Linha de Capa no `GameConfigPanel`
- **Campo de Texto (`_txtCoverPath`):** Largura ajustada para **230px**.
- **Botão `[Procurar...]` (`_btnBrowseCover`):** Largura de ~84px para seleção de arquivos locais no Windows.
- **Novo Botão `[SteamGridDB]` (`_btnSteamGridDb`):** Largura de ~110px com destaque visual (fundo turquesa/ciano com borda suave, ícone/texto claro e suporte a gamepad).
- **Navegação Gamepad:** A linha possui 3 colunas focáveis no `_navGrid`: `[Input Capa] ➔ [Procurar...] ➔ [SteamGridDB]`.

---

### 2.2. Modal Reutilizável de Chave de API (`SteamGridApiKeyDialog`)
Componente reutilizável utilizado tanto no primeiro acesso quanto no botão *"🔑 Trocar Chave API"*.
- **Estrutura Visual:**
  - **Título:** *"Configurar SteamGridDB"*
  - **Passo a passo resumido:**
    1. Clique em **`[Obter Chave no SteamGridDB]`** (abre a página oficial no navegador).
    2. Faça login (Steam ou Discord) e em *API Preferences* clique em *Generate API Key*.
    3. Copie a chave gerada e clique em **`[Colar]`** abaixo.
  - **Botão de Ação Externa:** `[Obter Chave no SteamGridDB]` (executa `Process.Start` para `https://www.steamgriddb.com/profile/preferences/api`).
  - **Campo de Entrada com Teclado Virtual Couch Gaming:**
    - Botão rápido de `[Colar]` da área de transferência (1 clique no mouse ou atalho no controle).
    - Se aberto via "Trocar Chave", já traz a chave atual preenchida.
- **Validação em Tempo Real (Feedback Imediato):**
  - Ao colar ou digitar e confirmar:
    - 🟡 **Checando...:** Borda amarela pulsante, executa `ValidateApiKeyAsync`.
    - 🟢 **Chave Válida!:** Borda verde neon, salva automaticamente no `config.ini` na seção `[SteamGridDB]` `ApiKey=<chave>` e avança o fluxo.
    - 🔴 **Chave Inválida!:** Borda vermelha com mensagem *"Chave incorreta ou expirada. Verifique e tente colar novamente."*. **Não salva** no arquivo de configurações e mantém o foco no botão de colar.

---

### 2.3. Modal de Termo de Busca & Atalho para Trocar Chave
- Disparado ao clicar em `[SteamGridDB]` quando a chave já está configurada.
- **Elementos:**
  - Campo de busca pré-preenchido com o nome do jogo atual (`_txtName.Text`).
  - Teclado Virtual Couch Gaming para edição rápida pelo controle ou mouse.
  - Botão de ação principal: `[Buscar]` (ou `Enter` / `RT`).
  - Botão secundário de utilidade: **`[🔑 Trocar Chave API]`** (reabre o `SteamGridApiKeyDialog` com a chave atual preenchida, permitindo substituir a qualquer momento).

---

### 2.4. Tela 1 — Lista Vertical de Jogos Encontrados
- **Vantagem de Performance:** Carregamento ultra-rápido (<100ms) sem gastar requisições para puxar imagens preliminares.
- **Cabeçalho:**
  - Título: *"Jogos Encontrados para: [Termo]"*
  - Legenda: `[A] Escolher Jogo   [X] Nova Pesquisa   [B] Voltar`
- **Conteúdo (Lista Vertical Estilo Console):**
  - Itens em lista vertical estilizada com badge de plataforma e título completo retornado por `/search/autocomplete/{termo}`.
  - Suporte completo a navegação vertical com D-Pad/Analógico (Gamepad) e clique único/hover (Mouse).
- **Ações:**
  - `B` no controle ou botão Voltar: retorna ao `GameConfigPanel`.
  - `X` no controle: reabre o modal de busca com o teclado virtual.
  - `A` ou 1 clique no mouse em um item: avança diretamente para a **Tela 2** com o ID do jogo escolhido.

---

### 2.5. Tela 2 — Galeria de Artes do Jogo (Cards de Capas 600x900)
- **Cabeçalho:**
  - Título: *"Capas para: [Nome do Jogo Oficial]"*
  - Legenda: `[A] Aplicar Capa   [B] Voltar aos Jogos`
- **Conteúdo (Grade de Capas):**
  - Consulta o endpoint `/grids/game/{gameId}?dimensions=600x900&mimes=image/png,image/jpeg&types=static`.
  - Filtro estrito `mimes=image/png,image/jpeg` garante total estabilidade com GDI+ do Windows Forms.
  - Renderiza a grade de cards com as miniaturas (`thumb`).
  - Destaque no hover do mouse e seleção com D-Pad.
- **Ações:**
  - `B` no controle ou botão Voltar: retorna para a **Tela 1** (lista de jogos).
  - `A` ou 1 clique no mouse em uma capa:
    1. Baixa a imagem em alta resolução (`url`).
    2. Salva localmente em `covers/steamgriddb_{gameId}_{assetId}.png`.
    3. Atualiza o campo `_txtCoverPath.Text` e a prévia de imagem no `GameConfigPanel`.
    4. Fecha a tela de busca e retorna ao formulário do jogo.

---

## 3. Roteiro de Validação Interativa (Como o Usuário Valida no App)

Quando a entrega for concluída, o usuário poderá validar cada detalhe diretamente na tela através dos seguintes passos:

### Teste 1: Linha de Capa & Primeiro Acesso (Chave de API)
1. Inicie o app e navegue até a **Biblioteca de Jogos**.
2. Clique com o botão direito ou no botão **`[Y] Configurar`** de qualquer jogo.
3. Observe a linha da Capa: o campo está menor, ao lado do botão `[Procurar...]` agora existe o botão **`[SteamGridDB]`**.
4. Clique no botão **`[SteamGridDB]`** (com o mouse ou com `A` no controle).
5. Como ainda não há chave salva, o modal **"Configurar SteamGridDB"** abre na tela.
6. Clique no botão **`[Obter Chave no SteamGridDB]`**: o navegador deve abrir na página da API do SteamGridDB.
7. Digite uma chave falsa ou aleatória e confirme: o sistema exibirá **🟡 Checando...** e em seguida a borda fica **🔴 Vermelha ("Chave inválida!")** sem salvar.
8. Cole a chave correta da sua conta e confirme: o sistema exibirá **🟢 Chave válida!** e avançará automaticamente.

### Teste 2: Pesquisa de Jogo & Troca de Chave
1. O modal de busca abre com o nome do jogo atual já preenchido.
2. Note o botão **`[🔑 Trocar Chave API]`**: clicando nele, o modal anterior reabre com a sua chave já preenchida para edição.
3. Pressione `Enter` ou confirme com `A`/`RT` no controle para pesquisar.

### Teste 3: Tela 1 (Lista de Jogos Encontrados)
1. Em menos de 1 segundo, a **Tela 1** exibe a lista vertical dos jogos encontrados com títulos e plataformas.
2. Navegue com o D-Pad para cima e para baixo (ou mova o mouse para destacar com hover).
3. Pressione `B` (ou clique em Voltar): volta para a configuração do jogo.
4. Pressione `[SteamGridDB]` novamente e selecione um jogo pressionando `A` (ou 1 clique com o mouse).

### Teste 4: Tela 2 (Grade de Capas) & Download
1. A **Tela 2** abre exibindo os cards das capas verticais 600x900 disponíveis para o título.
2. Navegue entre as capas com o D-Pad ou mouse.
3. Pressione `B`: retorna à Tela 1 (lista de jogos).
4. Escolha uma capa e pressione `A` (ou 1 clique com o mouse):
   - O app baixa a capa em alta resolução para a pasta local `covers/`.
   - O campo de caminho da capa no formulário é atualizado.
   - A prévia da capa é atualizada na tela do jogo.
   - Pressione Salvar e veja o card do jogo na biblioteca com a nova capa oficial!

---

## 4. Marco de Entrega Consolidado

| Marco | Conteúdo Completo | Verificação Automática + Manual |
| :--- | :--- | :--- |
| **Entrega Completa: SteamGridDB Couch Gaming** | - DTOs e `SteamGridDbService` (validação de chave, busca, consulta com filtro MIME e download stream).<br>- Redesenho da linha da capa no `GameConfigPanel`.<br>- Modal reutilizável de chave (`SteamGridApiKeyDialog`) com feedback em tempo real (🟡/🟢/🔴) e persistência em `config.ini`.<br>- Modal de pesquisa com atalho de troca de chave.<br>- Controle de navegação em 2 etapas (`SteamGridDbBrowserControl`): Lista de Jogos + Grade de Capas 600x900.<br>- Download em disco e integração final no formulário. | - `dotnet test` (97+ testes aprovados).<br>- Validação visual e interativa completa no app conforme o Roteiro da Seção 3. |

---

## 5. Regras & Boas Práticas
- **Zero Warnings / Zero Errors:** Compilação limpa com `TreatWarningsAsErrors=true`.
- **Encerramento Preventivo:** `Stop-Process -Name ConsoleMode-GamepadCompanion -ErrorAction SilentlyContinue` antes de compilar/testar.
- **Push apenas sob comando:** Commits atômicos locais; push só com comando explícito.
- **Suporte 100% Dual:** Todas as interações funcionam identicamente no Gamepad e no Mouse.
