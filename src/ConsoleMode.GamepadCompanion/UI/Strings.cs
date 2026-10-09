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
    }
}
