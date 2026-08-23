using LiteShot.Platform;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LiteShot.Capture;

/// <summary>
/// A parte matemática da captura, fora de qualquer janela: recorta a área
/// escolhida, aplica o limitador de resolução e codifica no formato pedido.
///
/// No LiteShot antigo isto vivia dentro do formulário do overlay
/// (<c>GetCroppedImageProcess</c>), misturado com a interface. Aqui é uma classe
/// sem dependência de UI, que pode rodar em qualquer thread e ser testada isolada.
/// </summary>
public static class ImageComposer
{
    /// <summary>
    /// Recorta o pedaço escolhido e reduz se ele ultrapassar o limite configurado.
    ///
    /// O limitador se aplica ao <b>recorte</b>, não à tela cheia — então um recorte
    /// pequeno nunca é reduzido, por mais alta que seja a resolução do monitor.
    /// </summary>
    /// <param name="screenshot">A imagem completa da área de trabalho virtual.</param>
    /// <param name="bounds">A união dos monitores, para converter coordenada virtual em posição na imagem.</param>
    /// <param name="selection">A área escolhida, em coordenadas virtuais.</param>
    /// <param name="resolutionLimit">"Auto" ou "LARGURAxALTURA".</param>
    /// <param name="annotations">
    /// As anotações a gravar por cima. É aqui — e só aqui — que elas deixam de ser
    /// objetos e viram pixels: uma rasterização, no fim, em vez de uma por traço.
    /// </param>
    public static BitmapSource Compose(
        BitmapSource screenshot,
        (int Left, int Top, int Width, int Height) bounds,
        (int Left, int Top, int Width, int Height) selection,
        string resolutionLimit,
        AnnotationLayer? annotations = null)
    {
        var cropped = BitmapInterop.Crop(
            screenshot,
            selection.Left - bounds.Left,
            selection.Top - bounds.Top,
            selection.Width,
            selection.Height);

        if (annotations is null || annotations.Items.Count == 0)
            return ApplyLimit(cropped, resolutionLimit);

        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            context.DrawImage(cropped, new Rect(0, 0, selection.Width, selection.Height));

            // As anotações vivem em coordenadas virtuais; aqui a origem passa a ser
            // o canto da seleção. O que ficar fora some naturalmente, porque a
            // superfície tem exatamente o tamanho do recorte.
            context.PushTransform(new TranslateTransform(-selection.Left, -selection.Top));
            AnnotationRenderer.DrawAll(context, annotations);
            context.Pop();
        }

        var rendered = new RenderTargetBitmap(
            selection.Width, selection.Height, 96, 96, PixelFormats.Pbgra32);

        rendered.Render(visual);
        rendered.Freeze();

        return ApplyLimit(rendered, resolutionLimit);
    }

    /// <summary>
    /// Reduz proporcionalmente se a imagem passar do limite. Devolve a original
    /// quando o limite é "Auto", não é reconhecido, ou a imagem já cabe.
    /// </summary>
    public static BitmapSource ApplyLimit(BitmapSource image, string resolutionLimit)
    {
        if (!TryParseLimit(resolutionLimit, out var maxWidth, out var maxHeight))
            return image;

        if (image.PixelWidth <= maxWidth && image.PixelHeight <= maxHeight)
            return image;

        var ratio = Math.Min(
            (double)maxWidth / image.PixelWidth,
            (double)maxHeight / image.PixelHeight);

        var scaled = new TransformedBitmap(image, new ScaleTransform(ratio, ratio));
        scaled.Freeze();
        return scaled;
    }

    /// <summary>
    /// Codifica para bytes. É a operação cara da captura — por isso ela acontece
    /// depois de o overlay sair da frente, e nunca no caminho síncrono do disparo.
    /// </summary>
    /// <param name="format">"PNG", "JPEG" ou "BMP".</param>
    public static byte[] Encode(BitmapSource image, string format)
    {
        var encoder = CreateEncoder(format);
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>Grava direto num arquivo, sem passar por um buffer intermediário.</summary>
    public static void SaveToFile(BitmapSource image, string path, string format)
    {
        var encoder = CreateEncoder(format);
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>Deduz o formato pela extensão do arquivo escolhido no diálogo.</summary>
    public static string FormatFromExtension(string path, string fallback)
    {
        var extension = Path.GetExtension(path);

        return extension.ToLowerInvariant() switch
        {
            ".png" => "PNG",
            ".jpg" or ".jpeg" => "JPEG",
            ".bmp" => "BMP",
            _ => fallback
        };
    }

    private static BitmapEncoder CreateEncoder(string format) => format.ToUpperInvariant() switch
    {
        "JPEG" or "JPG" => new JpegBitmapEncoder { QualityLevel = 92 },
        "BMP" => new BmpBitmapEncoder(),
        _ => new PngBitmapEncoder()
    };

    private static bool TryParseLimit(string limit, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (string.IsNullOrWhiteSpace(limit) || limit.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = limit.Split('x', 'X');

        return parts.Length == 2
            && int.TryParse(parts[0], out width)
            && int.TryParse(parts[1], out height)
            && width > 0
            && height > 0;
    }
}