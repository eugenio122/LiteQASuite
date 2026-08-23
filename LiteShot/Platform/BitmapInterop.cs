using System.Windows;
using System.Windows.Media.Imaging;
using GdiBitmap = System.Drawing.Bitmap;

// A classe estática do WPF que converte um HBITMAP em BitmapSource chama-se
// "Imaging", e é referenciada abaixo pelo nome completo de propósito: o nome curto
// colidiria com o namespace System.Windows.Media.Imaging, e a leitura ficaria
// ambígua para quem passar por aqui depois.

namespace LiteShot.Platform;

/// <summary>
/// A fronteira entre o mundo GDI+ (onde a captura acontece) e o mundo WPF (onde
/// tudo o mais acontece). Depois desta conversão, o LiteShot não toca mais em
/// <c>System.Drawing</c>: recorte, redimensionamento e codificação são todos WPF.
/// </summary>
public static class BitmapInterop
{
    /// <summary>
    /// Converte o bitmap da captura num <see cref="BitmapSource"/> congelado.
    ///
    /// Usa o caminho por HBITMAP, que não recodifica os pixels — importante,
    /// porque a imagem pode ter dezenas de milhões de pixels num setup com dois
    /// monitores 4K, e passar por um encode aqui custaria segundos no exato
    /// momento em que o usuário espera a tela congelar.
    ///
    /// O <c>Freeze</c> é o que permite a imagem ser usada em outra thread depois —
    /// a codificação para PNG acontece em segundo plano.
    /// </summary>
    public static BitmapSource ToBitmapSource(GdiBitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();

        try
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();
            return source;
        }
        finally
        {
            // O HBITMAP devolvido por GetHbitmap é nosso para liberar. Sem isto,
            // vaza um bitmap do tamanho da tela inteira a cada captura.
            NativeMethods.DeleteObject(handle);
        }
    }

    /// <summary>
    /// Recorta um pedaço sem copiar pixels — o <see cref="CroppedBitmap"/> é uma
    /// vista sobre a origem. É como cada janela de overlay mostra só a parte do
    /// screenshot que corresponde ao seu monitor.
    /// </summary>
    /// <param name="source">A imagem completa da área de trabalho virtual.</param>
    /// <param name="x">Posição do recorte, relativa à imagem (não à tela).</param>
    public static BitmapSource Crop(BitmapSource source, int x, int y, int width, int height)
    {
        var safeX = Math.Clamp(x, 0, Math.Max(0, source.PixelWidth - 1));
        var safeY = Math.Clamp(y, 0, Math.Max(0, source.PixelHeight - 1));
        var safeWidth = Math.Clamp(width, 1, source.PixelWidth - safeX);
        var safeHeight = Math.Clamp(height, 1, source.PixelHeight - safeY);

        var cropped = new CroppedBitmap(source, new Int32Rect(safeX, safeY, safeWidth, safeHeight));
        cropped.Freeze();
        return cropped;
    }
}