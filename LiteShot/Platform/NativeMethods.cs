using System.Runtime.InteropServices;

namespace LiteShot.Platform;

/// <summary>
/// Chamadas nativas ao Windows usadas pelo LiteShot, e as constantes e estruturas
/// que as acompanham. Só declarações — a lógica que as consome mora em outras
/// classes.
///
/// O projeto é livre de WinForms por decisão de princípio da suíte, então o que
/// antes vinha de <c>System.Windows.Forms</c> (Screen, Cursor, Cursors) vem daqui.
///
/// As estruturas de interop ficam aninhadas nesta classe em vez de em arquivos
/// próprios: a regra de "um tipo por arquivo" existe para classes de domínio, e
/// espalhar RECT/POINT/CURSORINFO em quatro arquivos só afastaria cada uma da
/// função que a usa.
///
/// Onde há callback ou estrutura por referência, a declaração usa
/// <c>DllImport</c> em vez de <c>LibraryImport</c> — o gerador do LibraryImport
/// fica desconfortável com esses casos e o ganho não compensa a fricção.
/// </summary>
internal static partial class NativeMethods
{
    // ==================================================================
    // ESTRUTURAS
    // ==================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public uint xHotspot;
        public uint yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    // ==================================================================
    // CONSTANTES
    // ==================================================================

    /// <summary>Mensagem que o Windows envia quando uma hotkey registrada é pressionada.</summary>
    public const int WM_HOTKEY = 0x0312;

    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    /// <summary>Largura, em pixels físicos, do monitor primário.</summary>
    public const int SM_CXSCREEN = 0;

    /// <summary>Altura, em pixels físicos, do monitor primário.</summary>
    public const int SM_CYSCREEN = 1;

    /// <summary>Sinalizador de monitor primário em <see cref="MONITORINFO.dwFlags"/>.</summary>
    public const uint MONITORINFOF_PRIMARY = 0x00000001;

    /// <summary>O cursor está visível (<see cref="CURSORINFO.flags"/>).</summary>
    public const uint CURSOR_SHOWING = 0x00000001;

    /// <summary>Desenha o ícone com máscara e cor.</summary>
    public const uint DI_NORMAL = 0x0003;

    /// <summary>Janela apenas de mensagens: invisível, sem posição, sem barra de tarefas.</summary>
    public static readonly IntPtr HWND_MESSAGE = new(-3);

    /// <summary>Coloca a janela acima de todas as não-topmost.</summary>
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    // ==================================================================
    // TECLADO E ATALHOS
    // ==================================================================

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Traduz um código virtual em scan code (uMapType = 0).</summary>
    [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    public static partial uint MapVirtualKey(uint uCode, uint uMapType);

    /// <summary>
    /// Nome legível de uma tecla, no layout de teclado ativo. Recebe o scan code
    /// já posicionado nos bits 16-23 do lParam.
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "GetKeyNameTextW")]
    public static unsafe partial int GetKeyNameText(int lParam, char* lpString, int cchSize);

    // ==================================================================
    // MONITORES
    // ==================================================================

    /// <summary>Métrica do sistema. Com PerMonitorV2, devolve pixels físicos reais.</summary>
    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int nIndex);

    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcClip, IntPtr dwData);

    /// <summary>Enumera todos os monitores. Substitui o <c>Screen.AllScreens</c> do WinForms.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // ==================================================================
    // CURSOR
    // ==================================================================

    /// <summary>Posição do cursor em coordenadas físicas. Substitui o <c>Cursor.Position</c>.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>
    /// O cursor que está de fato na tela, com o ponteiro do ícone e a posição.
    /// Substitui o <c>Cursors.Default</c> do código antigo — que desenhava sempre a
    /// setinha padrão, mesmo quando o cursor real era outro.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorInfo(ref CURSORINFO pci);

    /// <summary>
    /// Dados de um ícone/cursor, incluindo o ponto quente. Os dois HBITMAP que ela
    /// devolve precisam de <see cref="DeleteObject"/> — senão vaza a cada captura.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DrawIconEx(
        IntPtr hdc, int xLeft, int yTop, IntPtr hIcon,
        int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

    // ==================================================================
    // JANELAS E GDI
    // ==================================================================

    /// <summary>Posiciona a janela em <b>pixels físicos</b>, driblando a matemática de DIP do WPF.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    /// <summary>Quem estava em primeiro plano antes de o overlay roubar a tela.</summary>
    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>Libera objetos GDI (HBITMAP, HICON auxiliares). Sem isto, vaza.</summary>
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(IntPtr hObject);
}