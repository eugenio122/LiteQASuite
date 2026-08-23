using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LiteShot.Platform;

/// <summary>
/// O motor de captura: tira a foto de toda a área de trabalho física, unindo
/// todos os monitores. Porta direta do <c>ScreenCapture.CaptureAllScreens</c>
/// antigo, com as dependências de WinForms trocadas por P/Invoke.
///
/// Usa <c>System.Drawing</c> para o grab — que é rápido, síncrono e já validado —
/// e não fere o princípio de zero WinForms, porque <c>System.Drawing</c> é uma
/// biblioteca separada. A conversão para o mundo WPF acontece logo em seguida, no
/// <see cref="BitmapInterop"/>.
/// </summary>
public static class ScreenGrabber
{
    /// <summary>
    /// Copia os pixels de todos os monitores para um bitmap único, respeitando o
    /// deslocamento da união (um monitor à esquerda do primário tem coordenada
    /// negativa).
    ///
    /// É a operação que precisa ser rápida: ela roda de forma síncrona no momento
    /// em que o usuário aperta o atalho, antes de qualquer coisa aparecer na tela.
    /// A codificação para PNG — essa sim cara — fica para depois, quando o overlay
    /// já saiu da frente.
    /// </summary>
    /// <param name="bounds">A união dos monitores, de <see cref="Monitors.VirtualBounds"/>.</param>
    /// <param name="drawCursor">Se o cursor do mouse deve ser embutido na imagem.</param>
    public static Bitmap Capture(
        (int Left, int Top, int Width, int Height) bounds, bool drawCursor)
    {
        var screenshot = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(screenshot))
        {
            g.CopyFromScreen(
                bounds.Left, bounds.Top,
                0, 0,
                new Size(bounds.Width, bounds.Height),
                CopyPixelOperation.SourceCopy);

            if (drawCursor)
                TryDrawCursor(g, bounds.Left, bounds.Top);
        }

        return screenshot;
    }

    /// <summary>
    /// Desenha o cursor que estava de fato na tela, no lugar certo.
    ///
    /// Duas melhorias sobre o código antigo, que usava <c>Cursors.Default.Draw</c>:
    /// o cursor desenhado é o <b>real</b> (mãozinha, I-beam, redimensionamento) em
    /// vez da setinha padrão sempre; e a posição desconta o <i>ponto quente</i> do
    /// ícone, sem o qual um cursor de centro deslocado — como o I-beam ou a cruz —
    /// aparece fora de lugar na imagem.
    ///
    /// Falha em silêncio de propósito: o Windows bloqueia o acesso ao cursor em
    /// algumas situações (telas do UAC, cursores de hardware exclusivos), e isso
    /// não é motivo para perder a captura inteira.
    /// </summary>
    private static void TryDrawCursor(Graphics g, int offsetX, int offsetY)
    {
        try
        {
            var cursorInfo = new NativeMethods.CURSORINFO
            {
                cbSize = Marshal.SizeOf<NativeMethods.CURSORINFO>()
            };

            if (!NativeMethods.GetCursorInfo(ref cursorInfo))
                return;

            if ((cursorInfo.flags & NativeMethods.CURSOR_SHOWING) == 0)
                return;

            if (!NativeMethods.GetIconInfo(cursorInfo.hCursor, out var iconInfo))
                return;

            try
            {
                var x = cursorInfo.ptScreenPos.X - offsetX - (int)iconInfo.xHotspot;
                var y = cursorInfo.ptScreenPos.Y - offsetY - (int)iconInfo.yHotspot;

                var hdc = g.GetHdc();
                try
                {
                    NativeMethods.DrawIconEx(
                        hdc, x, y, cursorInfo.hCursor,
                        0, 0, 0, IntPtr.Zero, NativeMethods.DI_NORMAL);
                }
                finally
                {
                    g.ReleaseHdc(hdc);
                }
            }
            finally
            {
                // O GetIconInfo devolve dois HBITMAP que são nossos para liberar.
                // Sem isto, vaza um par de bitmaps GDI a cada captura.
                if (iconInfo.hbmMask != IntPtr.Zero) NativeMethods.DeleteObject(iconInfo.hbmMask);
                if (iconInfo.hbmColor != IntPtr.Zero) NativeMethods.DeleteObject(iconInfo.hbmColor);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LiteShot] Não foi possível desenhar o cursor: {ex.Message}");
        }
    }
}