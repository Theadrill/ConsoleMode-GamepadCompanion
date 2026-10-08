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
3. **Bug Pendente da Barra de Pesquisa em Fullscreen:**
   - Em tela cheia, a barra de pesquisa apresenta artefato visual de caixa menor interna desalinhada com a barra externa. Resolver assim que o fundo dinâmico for concluído.

---

## Questões Futuras do Grill Me (Pausadas temporariamente)
- Tipo e duração da animação de transição (Crossfade suave de opacidade).
- Algoritmo exato de recorte e opacidade da vinheta para legibilidade dos cards.

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
