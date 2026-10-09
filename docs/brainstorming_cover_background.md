# Brainstorming: Fundo Dinâmico com Blur & Vignette na Biblioteca

## Visão Geral
Transformar o visual da biblioteca de jogos (`GamesGridControl`) em uma experiência cinematográfica estilo PlayStation 5 / Steam Big Picture. Ao navegar pelos jogos com o controle ou mouse, o plano de fundo da biblioteca transita suavemente para uma arte widescreen do jogo focado com efeito de desfoque (blur), zoom (center-crop) e vinheta escura.

---

## Decisões Definidas (Grill Me)

1. **Área de Cobertura do Fundo:**
   - O fundo desfocado abrange toda a área direita da biblioteca (`GamesGridControl`), cobrindo desde o topo (título "BIBLIOTECA DE JOGOS" e barra de pesquisa) até embaixo na grade de cards.
   - O painel lateral esquerdo (`_sidePanel`, com sliders de deadzone, botões e status) permanece sólido e intocado.
2. **Comportamento em Jogos Sem Capa e Botão "[+] Adicionar Jogo":**
   - Retorna suavemente ao fundo neutro escuro sólido padrão (24, 26, 32). Sem fundos aleatórios ou mantidos incorretamente.
3. **Transição Visual e Navegação Rápida:**
   - **Crossfade Suave com Debounce (~150ms a 200ms):** Ao repousar o foco em um card por ~150ms, dispara o fade suave entre os fundos. Durante scrolls contínuos ou navegação veloz pelo direcional, não repinta nem troca a cada micro-passo, evitando flickering e mantendo taxa de quadros a 60 FPS.
4. **Intensidade do Efeito Visual (Desfoque & Vinheta Escura):**
   - **Atmosférico / Ambient Glow (Blur Acentuado + 70% Escurecimento / Vinheta):** Estilo PS5 / Steam Big Picture. Preserva a paleta de cores, brilho e atmosfera da arte do jogo sem gerar conflito visual ou tirar legibilidade dos cards, títulos e contornos.
5. **Controle de Configuração (Ativação e Persistência):**
   - **Ativo por Padrão com Chave no INI (`DynamicHeroBackground: true`):** A experiência rica vem habilitada por padrão, com propriedade em `AppSettings` e persistência no arquivo INI, permitindo ligar/desligar caso o usuário queira um visual escuro neutro.
6. **Bug Pendente da Barra de Pesquisa em Fullscreen:**
   - Em tela cheia, a barra de pesquisa apresenta artefato visual de caixa menor interna desalinhada com a barra externa. Resolver assim que o fundo dinâmico for concluído.

---

## Status do Grill Me: CONCLUÍDO
Todas as decisões de design, comportamento, arquitetura e performance foram alinhadas.

---

## Arquitetura & Fluxo Técnico

### 1. Pré-processamento Antecipado (Zero Custo em Runtime)
Em vez de calcular o algoritmo de blur e recorte na CPU em 60 FPS durante a navegação, cada imagem de fundo é pré-processada uma única vez e salva em disco como par da capa original:
- Capa original: `covers/steamgrid_{gameId}_{coverId}.jpg` (600x900 vertical)
- Fundo pré-processado: `covers/steamgrid_{gameId}_{coverId}_hero_blur.jpg` (resolução otimizada ~960x540 widescreen, ~40 KB)

### 2. Tratamento da Imagem no Pré-processamento
1. **Aspect Ratio & Center Crop:** Extrai a porção central da capa vertical em proporção widescreen 16:9.
2. **Downsample & Box/Stack Blur:** Reduz para uma escala menor (ex: 120x68), aplica blur homogêneo de alta qualidade.
3. **Dark Vignette & Overlay:** Aplica um degradê preto com opacidade de 60% a 70% sobreposto, garantindo alto contraste e leitura cristalina dos cards e textos da biblioteca.
4. **Salva em JPEG comprimido:** Qualidade 80, gerando arquivos de apenas ~30 KB a 50 KB cada.

### 3. Auto-cura na Inicialização (Background Scanner)
Ao iniciar o aplicativo, uma tarefa assíncrona (`Task.Run`) inspeciona a pasta `covers/`:
- Identifica quaisquer capas que ainda não possuam seu respectivo arquivo `_hero_blur.jpg` (capas antigas ou adicionadas manualmente).
- Converte e salva em segundo plano sem impactar a abertura ou responsividade da UI.

### 4. Integração no Download do SteamGridDB
Quando o usuário seleciona e baixa uma nova capa no navegador do SteamGridDB:
- Baixa a capa original 600x900.
- Imediatamente gera o arquivo `_hero_blur.jpg` e salva na mesma pasta.

### 5. Renderização na Biblioteca
- Ao trocar o foco do card na biblioteca, carrega a imagem do fundo pré-processado.
- Aplica um crossfade suave de opacidade (150ms a 200ms) entre o fundo do jogo anterior e o novo fundo.
- Configuração de liga/desliga nas opções do app caso o usuário prefira o fundo escuro neutro.
