using System;
using System.Runtime.InteropServices;

namespace LiteQASuite.Platform;

/// <summary>
/// Interop de SO do composition root. Mantém as chamadas P/Invoke isoladas aqui,
/// coerente com o princípio zero-WinForms (interop por P/Invoke, não por
/// referência ao <c>System.Windows.Forms</c>).
/// </summary>
internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    /// <summary>
    /// Melhor esforço para trazer a janela de título <paramref name="windowTitle"/>
    /// para a frente (restaurando-a se estiver minimizada). Silencioso se não a achar.
    /// </summary>
    public static void BringToFront(string windowTitle)
    {
        var handle = FindWindow(null, windowTitle);
        if (handle == IntPtr.Zero)
            return;

        ShowWindow(handle, SW_RESTORE);
        SetForegroundWindow(handle);
    }
}