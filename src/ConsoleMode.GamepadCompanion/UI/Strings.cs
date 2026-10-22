namespace ConsoleMode.GamepadCompanion.UI
{
    /// <summary>Textos da interface centralizados (regra 11: localização desacoplada).</summary>
    internal static class Strings
    {
        public const string WindowTitle = "ConsoleMode - Gamepad Companion";
        public const string ActiveController = "Controle ativo";
        public const string Automatic = "Automático (primeiro conectado)";
        public const string PlayerFormat = "Gamepad {0} ({1})";
        public const string Connected = "Conectado";
        public const string Disconnected = "Desconectado";
        public const string MappingOn = "Desativar Companion";
        public const string MappingOff = "Ativar Companion";
        public const string EnableCompanion = "Ativar Companion";
        public const string DisableCompanion = "Desativar Companion";
        public const string SelectActiveController = "Selecionar Controle Ativo";
        public const string NavHintAdjust = "[◄ / ►] Ajustar";
        public const string NavHintNavigate = "[▲ / ▼] Navegar";
        public const string NavHintConfirm = "[A] Confirmar";
        public const string NavHintCancel = "[B] Cancelar";
        public const string NavHintEdit = "[A] Editar";
        public const string MouseSensitivity = "Sensibilidade do mouse";
        public const string StickDeadzone = "Deadzone dos analógicos (%)";
        public const string TriggerThreshold = "Limiar dos gatilhos (%)";
        public const string StatusNone = "Nenhum controle conectado";
        public const string StatusFormat = "Usando Gamepad {0}";
        public const string ProfileFormat = "Perfil: {0}";
        public const string ProfileGaming = "Gaming (WoW ativo)";
        public const string ProfileDesktop = "Desktop (em espera)";
        public const string DeviceSteamDeckLcd = "Steam Deck (LCD)";
        public const string DeviceSteamDeckOled = "Steam Deck (OLED)";
        public const string DeviceStandardPc = "PC Padrão";
        public const string DeviceFormat = "Aparelho: {0}";
        public const string ViGEmActive = "ViGEm: Ativo";
        public const string ViGEmMissing = "ViGEm: Não Instalado";
        public const string OpenSettings = "Configurações";
        public const string Exit = "Sair";
        public const string ExitDialogTitle = "Sair do Companion";
        public const string ExitDialogMessage = "O que você deseja fazer?";
        public const string ExitActionMinimize = "Minimizar para a Barra de Tarefas";
        public const string ExitActionMinimizeDesc = "Continua rodando em segundo plano no System Tray";
        public const string ExitActionClose = "Fechar o Aplicativo";
        public const string ExitActionCloseDesc = "Encerra o processo completamente";
        public const string NavHintBack = "[B] Voltar";
        public const string AlreadyRunningTitle = "ConsoleMode - Gamepad Companion";
        public const string AlreadyRunningPrompt = "O ConsoleMode - Gamepad Companion já está em execução no sistema.\n\nDeseja forçar o encerramento da instância anterior e iniciar esta?";

        // Strings da Biblioteca de Jogos (Fase 2)
        public const string BtnGames = "BIBLIOTECA (Y)";
        public const string ActionLaunch = "[A] INICIAR";
        public const string ActionConfigure = "[Y] CONFIGURAR";
        public const string ActionAdd = "[A] ADICIONAR";
        public const string AddGameTitle = "Adicionar Jogo";
        public const string AddGameDesc = "Novo atalho de jogo";
        public const string GamesLibraryTitle = "BIBLIOTECA DE JOGOS";
        public const string GameConfigTitle = "Propriedades do Jogo";
        public const string GameConfigNewTitle = "Adicionar Novo Jogo";
        public const string GameConfigName = "Nome do Jogo:";
        public const string GameConfigLauncher = "Launcher / Script intermediário (opcional):";
        public const string GameConfigExecutable = "Executável Principal do Jogo (*.exe):";
        public const string GameConfigTargetPath = "Caminho de Destino (TargetPath):";
        public const string GameConfigWorkingDir = "Diretório de Trabalho (WorkingDirectory):";
        public const string GameConfigArguments = "Argumentos de Linha de Comando:";
        public const string GameConfigCoverImage = "Caminho da Capa (PNG / JPG):";
        public const string BtnBrowse = "Procurar...";
        public const string BtnSave = "[RT] Salvar";
        public const string BtnCancel = "[B] Cancelar";
        public const string BtnDelete = "Excluir Jogo";
        public const string ConfirmDeleteTitle = "Excluir Jogo";
        public const string ConfirmDeletePrompt = "Tem certeza de que deseja remover '{0}' da sua biblioteca?";
        public const string LaunchErrorTitle = "Falha ao Iniciar Jogo";
        public const string LaunchErrorPrompt = "Não foi possível iniciar o jogo:\n{0}";
        public const string ValidationErrorTitle = "Dados Incompletos";
        public const string ValidationErrorPrompt = "Por favor, preencha o Nome do Jogo e o Caminho de Destino (ou Executável).";
        public const string ValidationExeRequiredTitle = "Executável Necessário";
        public const string ValidationExeRequiredPrompt = "Não foi possível identificar o executável principal do jogo (*.exe).\n\nPor favor, informe o nome do executável (ex: OctoWoW.exe) ou selecione o arquivo em Destino.";
        public const string DefaultGameName = "Turtle WoW";
        public const string DefaultGameExecutable = "WoW.exe";
        public const string DefaultGameTargetPath = "WoW.exe";

        // Perfis de Controle & Input por Jogo (Fase 1 e 2)
        public const string GameConfigInputSectionTitle = "INPUT DO COMPANION NESTE JOGO:";
        public const string GameConfigInputEnabled = "(●) ATIVADO - Emular Teclado/Mouse";
        public const string GameConfigInputDisabled = "(○) DESATIVADO - Jogo lê Gamepad Nativo";
        public const string GameConfigConfigureControls = "🎮 Configurar Controles";
        public const string ProfileEditorTitle = "PERFIL DE CONTROLE: {0}";
        public const string PresetWowClassicName = "WoW Classic (Octowow)";
        public const string PresetRpgName = "RPG / MMO (Câmera Botão Direito)";
        public const string PresetStandardPcName = "Padrão PC (WASD + Mouse)";

        // Editor Visual de Perfil do Jogo (Fase 2)
        public const string ProfileImportPresetOrCopy = "Importar Preset / Copiar Jogo ▼";
        public const string ProfileMenuSectionPresets = "PRESETS PRONTOS";
        public const string ProfileMenuSectionCopyGames = "COPIAR DE OUTRO JOGO";
        public const string ProfileMenuCopyFromFormat = "Copiar de: {0}";
        public const string ProfileMenuCloseHint = "(▲/▼) Navegar   (A) Aplicar   (B) Fechar";
        public const string ProfileSensitivityLabel = "Sensibilidade Mouse (Página 1):";
        public const string ProfileDeadzoneLabel = "Deadzone Analógicos:";
        public const string ProfileTriggerThresholdLabel = "Limiar de Gatilhos (LT/RT):";
        public const string ProfileGlobalToggle = "Global";
        public const string ProfileCardLeftStick = "STICK ESQUERDO";
        public const string ProfileCardRightStick = "STICK DIREITO";
        public const string ProfileCardDPad = "D-PAD (SETAS)";
        public const string ProfileCardLeftTrackpad = "TRACKPAD ESQUERDO";
        public const string ProfileCardRightTrackpad = "TRACKPAD DIREITO";
        public const string ProfileTrackpadExpandedTitle = "Configuração Expandida";
        public const string ProfileTrackpadExpandedSubtitle = "(Planejada - Fase 6)";
        public const string ProfileRearButtonsTitle = "BOTÕES TRASEIROS (GRIP)";
        public const string ProfileCardFaceButtons = "BOTÕES FACIAIS";
        public const string ProfileStickModeLabel = "Modo:";
        public const string ProfileStickModeDirectional = "4 Direções";
        public const string ProfileStickModeMouse = "Mov. Mouse";
        public const string ProfileStickModeDisabled = "Desativado";
        public const string ProfileDirUp = "Cima:";
        public const string ProfileDirDown = "Baixo:";
        public const string ProfileDirLeft = "Esq:";
        public const string ProfileDirRight = "Dir:";
        public const string ProfileAdditionalAction = "Adicional:";
        public const string ProfileClickL3 = "Clique L3:";
        public const string ProfileClickR3 = "Clique R3:";
        public const string ProfileActionNone = "Nenhum";
        public const string ProfileFooterLegend = "(A) Editar / Alternar    (X) Limpar Entrada    (RT) Salvar Perfil    (B) Voltar";
        public const string ProfileBtnSave = "[RT] Salvar Perfil";
        public const string ProfileBtnBack = "[B] Voltar";

        // Tela de Escolha do Modo de Edição & Captura Física de Combos (Fase 3)
        public const string BindingHeaderTitle = "EDITAR MAPEAMENTO: {0} — Jogo: {1}";
        public const string BindingCurrentMapping = "Mapeamento Atual: [ {0} ]";
        public const string BindingOptionCaptureTitle = "🎙️  CAPTURAR INPUT FÍSICO";
        public const string BindingOptionCaptureDesc = "(Pressione tecla, combo ou botão do mouse)";
        public const string BindingOptionVisualTitle = "🖮   ESCOLHER BOTÃO / BOTÕES";
        public const string BindingOptionVisualDesc = "(Abrir seletor visual: Teclado / Mouse / Sistema)";
        public const string BindingOptionClearTitle = "🗑️  LIMPAR / DESATIVAR ENTRADA";
        public const string BindingOptionClearDesc = "(Define esta entrada como Nenhum)";
        public const string BindingCaptureTip = "* No modo Captura Física: segure múltiplas teclas (ex: Ctrl + Shift + S) e solte para gravar o combo completo automaticamente.";
        public const string BindingFooterChoice = "(A) Selecionar Opção                                                          (B) Voltar";
        public const string BindingListeningTitle = "🎙️ AGUARDANDO COMANDO FÍSICO...";
        public const string BindingListeningPrompt = "Pressione qualquer tecla, combinação de teclas (ex: Ctrl + Shift + S),\nbotão do mouse ou gire a roda de scroll agora.";
        public const string BindingListeningComboLabel = "Combo Detectado:";
        public const string BindingListeningWaiting = "Aguardando entrada...";
        public const string BindingListeningReleaseHint = "Solte as teclas para confirmar e gravar automaticamente!";
        public const string BindingFooterListening = "(B) Cancelar Escuta";

        // Seletor Visual de Input em 3 Abas (Fase 4)
        public const string VisualSelectorTabKeyboard = "1. TECLADO";
        public const string VisualSelectorTabMouse = "2. MOUSE";
        public const string VisualSelectorTabSystem = "3. SISTEMA & ESPECIAL";
        public const string VisualSelectorHeaderFormat = "Editando: {0}   |   Jogo: {1}";
        public const string VisualSelectorComboLabel = "Combo Montado: [ {0} ]";
        public const string VisualSelectorComboEmpty = "Vazio";
        public const string VisualSelectorFooterLegend = "(A) Selecionar Tecla    (X) Adicionar ao Combo    (Y) Limpar    (RT) Salvar e Voltar    (B) Cancelar";
        public const string VisualSelectorBtnSelect = "[A] Selecionar";
        public const string VisualSelectorBtnToggle = "[X] Add Combo";
        public const string VisualSelectorBtnClear = "[Y] Limpar";
        public const string VisualSelectorBtnConfirm = "[RT] Salvar";
        public const string VisualSelectorBtnCancel = "[B] Cancelar";

        // Aba 2: Mouse
        public const string VisualSelectorMouseLeft = "Clique Esquerdo (Mouse L)";
        public const string VisualSelectorMouseRight = "Clique Direito (Mouse R)";
        public const string VisualSelectorMouseMiddle = "Clique do Meio (Mouse M)";
        public const string VisualSelectorMouseWheelUp = "Roda para Cima (Scroll ▲)";
        public const string VisualSelectorMouseWheelDown = "Roda para Baixo (Scroll ▼)";
        public const string VisualSelectorMouseClear = "Limpar / Desativado";

        // Aba 3: Sistema & Especial
        public const string VisualSelectorSectionAudio = "ÁUDIO & MÍDIA";
        public const string VisualSelectorSectionWindows = "JANELAS & CAPTURA";
        public const string VisualSelectorSectionCompanion = "FUNÇÕES DO COMPANION";

        public const string VisualSelectorAudioVolUp = "🔊 Volume +";
        public const string VisualSelectorAudioVolDown = "🔉 Volume -";
        public const string VisualSelectorAudioMute = "🔇 Mutar Áudio";
        public const string VisualSelectorAudioPlayPause = "⏯️ Play / Pause Mídia";
        public const string VisualSelectorAudioNext = "⏭️ Próxima Faixa";
        public const string VisualSelectorAudioPrev = "⏮️ Faixa Anterior";

        public const string VisualSelectorWinPrintScreen = "📸 Captura PrintScreen";
        public const string VisualSelectorWinAltEnter = "🖥️ Tela Cheia (Alt+Enter)";
        public const string VisualSelectorWinAltTab = "🔄 Trocar Janela (Alt+Tab)";
        public const string VisualSelectorWinShowDesktop = "🪟 Mostrar Desktop (Win+D)";
        public const string VisualSelectorWinAltF4 = "❌ Fechar Jogo (Alt+F4)";
        public const string VisualSelectorWinKey = "🪟 Tecla Windows (Win)";

        public const string VisualSelectorCompCenterCursor = "🎯 Centralizar Cursor";
        public const string VisualSelectorCompToggleClip = "🔒 Toggle Confinar Cursor";

        // Diálogos modais Couch Gaming
        public const string DialogOk = "OK";
        public const string DialogConfirm = "Confirmar";
        public const string DialogCancel = "Cancelar";
        public const string DialogClose = "Fechar";

        // Busca e Filtragem (Fase 3)
        public const string SearchPlaceholder = "Buscar jogos... [X]";
        public const string ActionSearch = "[X] BUSCAR";
        public const string NoGamesFound = "Nenhum jogo encontrado";
        public const string PressBToClearSearch = "Pressione B para limpar a busca";
        public const string BtnClearSearch = "Limpar busca";

        // Teclado Virtual Couch Gaming (Fase 4)
        public const string VirtualKeyboardDefaultTitle = "Digitar Texto";
        public const string VirtualKeyboardSearchTitle = "Pesquisar Jogos";
        public const string KbHintType = "[A] Digitar";
        public const string KbHintSpace = "[Y] Espaço";
        public const string KbHintBackspace = "[X] Apagar";
        public const string KbHintCancel = "[B] Cancelar";
        public const string KbHintConfirm = "[RT] Concluir";
        public const string KbHintCaps = "[LB/RB] Caps";
        public const string KbHintClear = "[LT] Limpar";
        public const string KbHintCursor = "[◄/►] Cursor";

        // Integração SteamGridDB (Capas Automáticas)
        public const string BtnSteamGridDb = "SteamGridDB";
        public const string SteamGridModalTitle = "Configurar SteamGridDB";
        public const string SteamGridStep1 = "1. Clique no botão abaixo para abrir a página oficial no navegador.";
        public const string SteamGridStep2 = "2. Faça login e em 'API Preferences' gere sua chave gratuita.";
        public const string SteamGridStep3 = "3. Copie a chave e clique em [Colar] para validar.";
        public const string BtnGetApiKey = "Obter Chave no SteamGridDB";
        public const string BtnPaste = "Colar";
        public const string BtnChangeApiKey = "🔑 Trocar Chave API";
        public const string SteamGridCheckingKey = "🟡 Checando chave...";
        public const string SteamGridKeyValid = "🟢 Chave válida!";
        public const string SteamGridKeyInvalid = "🔴 Chave inválida ou expirada. Tente novamente.";
        public const string SteamGridSearchTitle = "Pesquisar no SteamGridDB";
        public const string SteamGridResultsTitle = "Jogos Encontrados para: {0}";
        public const string SteamGridCoversTitle = "Capas para: {0}";
        public const string SteamGridNoGamesFound = "Nenhum jogo encontrado com esse termo.";
        public const string SteamGridNoCoversFound = "Nenhuma capa vertical encontrada para este jogo.";
        public const string SteamGridDownloading = "Baixando capa em alta resolução...";
        public const string SteamGridLegendGames = "[A] Escolher Jogo    [X] Nova Pesquisa    [Y] Trocar Chave    [B] Voltar";
        public const string SteamGridLegendCovers = "[A] Aplicar Capa    [B] Voltar aos Jogos";

        // Modo Desktop (Windows)
        public const string BtnDesktopModeFormat = "🖥️ MODO DESKTOP [{0}]";
        public const string DesktopModeTitle = "Modo Desktop (Windows)";
        public const string DesktopModeActive = "ATIVADO";
        public const string DesktopModeDisabled = "DESATIVADO";
    }
}
