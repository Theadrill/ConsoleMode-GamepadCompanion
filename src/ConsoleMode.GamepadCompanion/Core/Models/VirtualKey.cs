namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Teclas virtuais do Windows (valores VK_ do Windows).
    /// Suporta teclado ANSI completo, modificadores, navegação, mídia e funções de sistema.
    /// </summary>
    public enum VirtualKey : ushort
    {
        None = 0x00,

        // Teclas de controle padrão
        Backspace = 0x08,
        Tab = 0x09,
        Clear = 0x0C,
        Enter = 0x0D,
        Escape = 0x1B,
        Space = 0x20,

        // Navegação e Edição
        PageUp = 0x21,
        PageDown = 0x22,
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        PrintScreen = 0x2C,
        Insert = 0x2D,
        Delete = 0x2E,

        // Números principais (linha superior)
        D0 = 0x30,
        D1 = 0x31,
        D2 = 0x32,
        D3 = 0x33,
        D4 = 0x34,
        D5 = 0x35,
        D6 = 0x36,
        D7 = 0x37,
        D8 = 0x38,
        D9 = 0x39,

        // Letras (A-Z)
        A = 0x41,
        B = 0x42,
        C = 0x43,
        D = 0x44,
        E = 0x45,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        M = 0x4D,
        N = 0x4E,
        O = 0x4F,
        P = 0x50,
        Q = 0x51,
        R = 0x52,
        S = 0x53,
        T = 0x54,
        U = 0x55,
        V = 0x56,
        W = 0x57,
        X = 0x58,
        Y = 0x59,
        Z = 0x5A,

        // Teclas do Windows
        LeftWindows = 0x5B,
        RightWindows = 0x5C,
        Apps = 0x5D,

        // Teclas de Função (F1 - F12)
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B,

        // Locks
        CapsLock = 0x14,
        NumLock = 0x90,
        ScrollLock = 0x91,

        // Navegação de Navegador
        BrowserBack = 0xA6,
        BrowserForward = 0xA7,
        BrowserRefresh = 0xA8,

        // Modificadores específicos
        LeftShift = 0xA0,
        RightShift = 0xA1,
        LeftControl = 0xA2,
        RightControl = 0xA3,
        LeftAlt = 0xA4,
        RightAlt = 0xA5,

        // Controle de Áudio e Mídia
        VolumeMute = 0xAD,
        VolumeDown = 0xAE,
        VolumeUp = 0xAF,
        MediaNextTrack = 0xB0,
        MediaPrevTrack = 0xB1,
        MediaStop = 0xB2,
        MediaPlayPause = 0xB3,

        // Teclas OEM / Pontuação (ANSI US padrão)
        OemSemicolon = 0xBA,     // ; :
        OemPlus = 0xBB,          // = +
        OemComma = 0xBC,         // , <
        OemMinus = 0xBD,         // - _
        OemPeriod = 0xBE,        // . >
        OemQuestion = 0xBF,      // / ?
        OemTilde = 0xC0,         // ` ~
        OemOpenBrackets = 0xDB,  // [ {
        OemPipe = 0xDC,          // \ |
        OemCloseBrackets = 0xDD, // ] }
        OemQuotes = 0xDE         // ' "
    }
}
