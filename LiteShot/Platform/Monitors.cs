namespace LiteShot.Platform;

/// <summary>
/// A disposição física dos monitores. Porta do <c>ScreenCapture.GetPhysicalBounds</c>
/// antigo, que usava <c>Screen.AllScreens</c> do WinForms.
///
/// É a peça mais importante do motor: a união exata dos monitores é o que permite
/// capturar setups com resoluções e escalas diferentes sem corte nem distorção.
/// Depende do processo estar declarado <c>PerMonitorV2</c> — sem isso o Windows
/// virtualiza as coordenadas e tudo aqui devolve números menores que os reais.
/// </summary>
public static class Monitors
{
    /// <summary>
    /// Todos os monitores ligados, na ordem em que o Windows os enumera.
    /// Consultado a cada captura, e não cacheado: o usuário pode conectar ou
    /// desconectar uma tela com o aplicativo aberto.
    /// </summary>
    public static IReadOnlyList<MonitorInfo> All()
    {
        var found = new List<MonitorInfo>();

        bool Collect(IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT clip, IntPtr data)
        {
            var info = new NativeMethods.MONITORINFO
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };

            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                var r = info.rcMonitor;
                found.Add(new MonitorInfo(
                    r.Left, r.Top, r.Width, r.Height,
                    (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0));
            }

            return true; // continua enumerando
        }

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Collect, IntPtr.Zero);

        // Rede de segurança: se a enumeração falhar por algum motivo, cai para o
        // monitor primário em vez de devolver lista vazia e quebrar a captura.
        if (found.Count == 0)
        {
            var w = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            var h = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
            found.Add(new MonitorInfo(0, 0, Math.Max(w, 1), Math.Max(h, 1), true));
        }

        return found;
    }

    /// <summary>
    /// O retângulo que engloba fisicamente todos os monitores — a "área de trabalho
    /// virtual". É o tamanho exato do screenshot que o LiteShot tira.
    /// </summary>
    public static (int Left, int Top, int Width, int Height) VirtualBounds(
        IReadOnlyList<MonitorInfo> monitors)
    {
        var left = monitors.Min(m => m.Left);
        var top = monitors.Min(m => m.Top);
        var right = monitors.Max(m => m.Right);
        var bottom = monitors.Max(m => m.Bottom);

        return (left, top, right - left, bottom - top);
    }

    /// <summary>
    /// O monitor onde o mouse está agora. É o que o Ctrl+A usa para selecionar "esta
    /// tela" — comportamento que o LiteShot antigo já tinha, via
    /// <c>Screen.FromPoint(Cursor.Position)</c>.
    /// </summary>
    /// <returns>O monitor sob o cursor, ou o primário se não der para determinar.</returns>
    public static MonitorInfo UnderCursor(IReadOnlyList<MonitorInfo> monitors)
    {
        if (NativeMethods.GetCursorPos(out var point))
        {
            var hit = monitors.FirstOrDefault(m => m.Contains(point.X, point.Y));
            if (hit is not null)
                return hit;
        }

        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }
}