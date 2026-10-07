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
    }
}
