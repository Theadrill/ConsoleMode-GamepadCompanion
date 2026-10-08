# Plano de Implementação — Integração SteamGridDB (Capas Automáticas)

> **Documento de Arquitetura Técnica & Fases de Implementação (Modo Plano)**  
> Integração completa com o serviço [SteamGridDB](https://www.steamgriddb.com) para pesquisa, seleção em 2 etapas (Lista de Jogos ➔ Grade de Artes) e download de capas verticais estilo console diretamente na interface Couch Gaming do ConsoleMode-GamepadCompanion.

---

## 1. Visão Geral da Experiência do Usuário (UX) & Reuso de Código

O usuário poderá buscar e aplicar qualquer capa do SteamGridDB sem sair do aplicativo, utilizando apenas o controle ou o mouse.

### Princípios Chave de Arquitetura:
1. **Regra de Ouro do Reuso:** Qualquer componente ou rotina que possa ser reutilizada **SERÁ** reutilizada (ex: o mesmo modal de chave de API é reutilizado na primeira configuração e no botão "Trocar Chave API").
2. **Desempenho Instantâneo:** A **Tela 1** é uma **Lista Vertical de Jogos** (resposta em <100ms em 1 única requisição leve). Evita 10 requisições simultâneas e riscos de rate-limit.
3. **Compatibilidade GDI+ (.NET 4.8):** A API busca estritamente formatos nativos compatíveis (`mimes=image/png,image/jpeg`), dispensando DLLs externas de terceiros como WebP nativo.
4. **Validação Ativa de Chave:** A chave colada/digitada é testada em tempo real com status visual (🟡 Checando ➔ 🟢 Válida / 🔴 Inválida). Se inválida, **não salva**.

### Fluxo de Navegação em 2 Etapas:
```mermaid
flowchart TD
    A["GameConfigPanel\n(Linha da Capa Redesenhada)"] -->|Clica [SteamGridDB]| B{"Possui Chave Válida\nno settings.ini?"}
    B -->|Não| C["Modal Reutilizável de Chave de API\n(Validação em tempo real: 🟡 ➔ 🟢/🔴)"]
    C -->|Salva apenas se Válida| D["Modal de Termo de Busca\n(Nome do Jogo + Botão Trocar Chave)"]
    B -->|Sim| D
    D -->|Clica [Trocar Chave]| C
    D -->|Confirma Busca| E["Tela 1: Lista de Jogos Encontrados\n(Lista Vertical Instantânea <100ms)"]
    E -->|Pressiona B ou Voltar| A
    E -->|Seleciona Jogo (A ou 1 clique)| F["Tela 2: Grade de Artes do Jogo\n(Capas Verticais 600x900 PNG/JPG)"]
    F -->|Pressiona B ou Voltar| E
    F -->|Seleciona Capa (A ou 1 clique)| G["Download em Alta Resolução\n(Salva em covers/ e aplica no formulário)"]
    G --> A
```

---

## 2. Detalhamento das Telas e Interações

### 2.1. Redesenho da Linha de Capa no `GameConfigPanel`
- **Campo de Texto (`_txtCoverPath`):** Largura ajustada para **240px**.
- **Botão `[Procurar...]` (`_btnBrowseCover`):** Largura de ~80px para seleção de arquivos locais no Windows.
- **Novo Botão `[SteamGridDB]` (`_btnSteamGridDb`):** Largura de ~110px com destaque visual (azul ciano com borda brilhante e suporte a gamepad).
- **Navegação Gamepad:** A linha possui 3 colunas focáveis no `_navGrid`: `[Input Capa] ➔ [Procurar...] ➔ [SteamGridDB]`.

---

### 2.2. Modal Reutilizável de Chave de API (`SteamGridApiKeyDialog`)
Componente **100% reutilizável** utilizado tanto no primeiro acesso quanto no botão *"Trocar Chave API"*.
- **Estrutura Visual:**
  - **Título:** *"Configurar SteamGridDB"*
  - **Passo a passo resumido:**
    1. Clique em **`[Obter Chave no SteamGridDB]`** (abre o navegador).
    2. Faça login (Steam/Discord) e em *API Preferences* clique em *Generate API Key*.
    3. Copie a chave gerada e clique em **`[Colar]`** abaixo.
  - **Botão de Ação Externa:** `[Obter Chave no SteamGridDB]` (executa `Process.Start` para `https://www.steamgriddb.com/profile/preferences/api`).
  - **Campo de Entrada com Teclado Virtual Couch Gaming:**
    - Botão rápido de `[Colar]` da área de transferência.
    - Se o modal for aberto para "Trocar Chave", o campo já vem preenchido com a chave atual mascarada/visível.
- **Validação em Tempo Real (Feedback Imediato):**
  - Ao colar ou digitar e confirmar:
    - 🟡 **Checando...:** Borda amarela pulsante, faz requisição de teste assíncrona (`GET /api/v2/games/id/1` com header `Bearer <chave>`).
    - 🟢 **Chave Válida!:** Borda verde neon, salva automaticamente no `settings.ini` na seção `[SteamGridDB]` `ApiKey=<chave>` e fecha o modal avançando o fluxo.
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
- **Vantagem de Performance:** Carregamento ultra-rápido (<100ms) sem gastar 10 requisições simultâneas para puxar imagens preliminares.
- **Cabeçalho:**
  - Título: *"Jogos Encontrados para: [Termo]"*
  - Legenda: `[A] Escolher Jogo   [X] Nova Pesquisa   [B] Voltar`
- **Conteúdo (Lista Vertical Estilo Console):**
  - Itens em lista vertical estilizada com badge de plataforma e título completo retornado por `/search/autocomplete/{termo}`.
  - Exemplo:
    - `[1] World of Warcraft`
    - `[2] World of Warcraft: Classic`
    - `[3] World of Warcraft: Wrath of the Lich King`
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

## 3. Arquitetura Técnica & Componentes

### 3.1. Cliente HTTP & Serviço (`SteamGridDbService`)
Local: `src/ConsoleMode.GamepadCompanion/Engine/Services/SteamGridDbService.cs`
- Utiliza `HttpClient` singleton resiliente.
- **Métodos:**
  - `ValidateApiKeyAsync(string apiKey)`: testa a chave contra a API e retorna `bool` (usado no feedback 🟡/🟢/🔴).
  - `SearchGamesAsync(string term, string apiKey)`: consome `/api/v2/search/autocomplete/{term}`.
  - `GetGameGridsAsync(int gameId, string apiKey)`: consome `/api/v2/grids/game/{gameId}?dimensions=600x900&mimes=image/png,image/jpeg&types=static`.
  - `DownloadCoverAsync(string imageUrl, string destinationPath)`: faz download em stream e valida que o arquivo foi gravado corretamente.

### 3.2. Modelos de Dados (`SteamGridModels.cs`)
Local: `src/ConsoleMode.GamepadCompanion/Core/Models/SteamGridModels.cs`
- DTOs serializáveis leves para o JSON da v2: `SteamGridSearchItem`, `SteamGridAssetItem`, `SteamGridResponse<T>`.

### 3.3. Telas e Controles UI
1. `SteamGridApiKeyDialog.cs`: Diálogo couch com teclado virtual, botão de link externo, botão de colar e verificação em tempo real (Reutilizável para configuração e troca).
2. `SteamGridDbBrowserControl.cs`: Controle de navegação em duas etapas:
   - Estado `GameList`: lista vertical dos títulos encontrados.
   - Estado `CoverGrid`: grade de cards das capas verticais 600x900.

---

## 4. Fases Agrupadas & Testáveis de Implementação

Para manter o desenvolvimento coeso, ágil e livre de micro-passos fragmentados, o projeto é estruturado em **2 grandes marcos testáveis**:

| Marco / Fase | Escopo Completo | Critério de Aceite & Verificação |
| :--- | :--- | :--- |
| **Fase 1: Engine, Serviço HTTP & Testes de Integração** | - Criação dos modelos de dados DTO (`SteamGridModels.cs`).<br>- Implementação completa do serviço `SteamGridDbService` (`HttpClient`, autenticação Bearer, validação de chave em tempo real `ValidateApiKeyAsync`, busca de jogos `SearchGamesAsync`, consulta de capas verticais com filtro PNG/JPEG `GetGameGridsAsync`, e download em stream para disco `DownloadCoverAsync`).<br>- Suíte de testes unitários automatizados cobrindo todos os cenários (HTTP 200, 401 chave inválida, lista vazia, deserialização JSON e download). | `dotnet test` executando com **100% de sucesso** em todos os cenários de rede/API sem warnings nem erros. |
| **Fase 2: Interface Couch Gaming Completa (UI, Navegação Dual & Integração)** | - Redesenho da linha de capa no `GameConfigPanel` (`_txtCoverPath` 240px, `_btnBrowseCover` e `_btnSteamGridDb`) com navegação de controle integrada ao `_navGrid`.<br>- Modal reutilizável de Chave de API (`SteamGridApiKeyDialog`): botão para abrir navegador oficial, colar com 1 clique, validação visual ativa (🟡 Checando ➔ 🟢 Válida / 🔴 Inválida) e persistência no `settings.ini`.<br>- Diálogo de busca de jogo com atalho `[Trocar Chave API]`.<br>- Navegação visual em 2 etapas (`SteamGridDbBrowserControl`):<br>&nbsp;&nbsp;• **Tela 1:** Lista vertical instantânea dos jogos encontrados.<br>&nbsp;&nbsp;• **Tela 2:** Grade de capas 600x900 em alta resolução.<br>- Download automático para a pasta `covers/` e aplicação instantânea no formulário. | Teste visual ponta a ponta no app: fluxo completo funcionando tanto via Gamepad quanto via Mouse com feedback em tempo real. |

---

## 5. Regras & Boas Práticas
- **Zero Warnings / Zero Errors:** Compilação limpa com `TreatWarningsAsErrors=true`.
- **Encerramento Preventivo:** `Stop-Process -Name ConsoleMode-GamepadCompanion -ErrorAction SilentlyContinue` antes de qualquer compilação/teste.
- **Push apenas sob comando:** Commits atômicos locais; push só com comando explícito.
- **Suporte 100% Dual:** Todas as interações funcionam perfeitamente no Gamepad e no Mouse.
