using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace LiteFlow.Scenario;

/// <summary>
/// Onde os PNGs das evidências ficam <b>enquanto o cenário está aberto</b>: uma
/// pasta temporária por sessão, apagada no encerramento.
///
/// <b>Isto é cache, não formato.</b> O artefato continua sendo o <c>.lflow</c>, com
/// as imagens embutidas. A pasta existe porque o caminho contrário — manter os
/// PNGs em memória — é o que fazia o 1.x carregar e descarregar bitmaps na mão
/// (<c>ManageMemoryFocus</c>) e chamar <c>GC.Collect()</c> em oito lugares. Com os
/// bytes em disco, quem decide o que está em memória é o decodificador, não nós.
///
/// <b>As miniaturas são decodificadas pequenas, não redimensionadas depois.</b>
/// O <c>DecodePixelWidth</c> faz o decodificador de PNG produzir direto uma imagem
/// de 160 pixels de largura: um print 4K nunca chega a existir em tamanho real na
/// memória para virar miniatura. O 1.x abria o bitmap inteiro (33 MB) e desenhava
/// num de 110 pixels — quarenta vezes por cenário aberto.
///
/// Tudo que sai daqui está <c>Freeze</c>ado: pode atravessar threads e o WPF para
/// de manter referência viva do decodificador.
/// </summary>
public sealed class EvidenceCache : IDisposable
{
    /// <summary>Largura em que as miniaturas do histórico são decodificadas.</summary>
    public const int ThumbnailWidth = 160;

    private readonly string _folder;
    private bool _isDisposed;

    public EvidenceCache()
    {
        _folder = Path.Combine(Path.GetTempPath(), "LiteFlowSession_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    /// <summary>Grava o PNG e devolve o caminho no cache.</summary>
    public string Store(string stepId, byte[] png)
    {
        var path = Path.Combine(_folder, SafeName(stepId) + ".png");
        File.WriteAllBytes(path, png);
        return path;
    }

    /// <summary>
    /// O <c>StepId</c> vem do LiteShot como Guid e é seguro — mas um <c>.lflow</c>
    /// antigo pode trazer qualquer coisa, e aqui ele vira nome de arquivo.
    /// </summary>
    private static string SafeName(string stepId)
    {
        if (string.IsNullOrWhiteSpace(stepId)) return Guid.NewGuid().ToString("N");

        var invalid = Path.GetInvalidFileNameChars();
        var chars = stepId.ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
        }

        return new string(chars);
    }

    /// <summary>
    /// Esvazia o cache — chamado ao trocar de cenário, para os prints do cenário
    /// anterior não ficarem ocupando disco a sessão inteira.
    /// </summary>
    public void Clear()
    {
        if (_isDisposed) return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(_folder, "*.png"))
            {
                try { File.Delete(file); } catch (IOException) { /* em uso; vai embora no Dispose */ }
            }
        }
        catch (DirectoryNotFoundException)
        {
            Directory.CreateDirectory(_folder);
        }
    }

    /// <summary>
    /// Miniatura para o histórico, decodificada já no tamanho pequeno.
    /// <c>null</c> quando o arquivo sumiu ou não é uma imagem válida — a lista
    /// mostra o passo sem miniatura em vez de a tela inteira quebrar.
    /// </summary>
    public static BitmapSource? LoadThumbnail(string path, int pixelWidth = ThumbnailWidth) =>
        Decode(path, pixelWidth);

    /// <summary>Imagem em tamanho real, para o canvas central.</summary>
    public static BitmapSource? LoadFull(string path) => Decode(path, 0);

    private static BitmapSource? Decode(string path, int pixelWidth)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();

            // OnLoad lê o arquivo inteiro agora e o solta em seguida: sem isto o
            // WPF mantém o arquivo aberto e a próxima gravação no mesmo caminho
            // falha com "em uso por outro processo".
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (pixelWidth > 0) image.DecodePixelWidth = pixelWidth;
            image.UriSource = new Uri(path, UriKind.Absolute);

            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }
        catch (Exception)
        {
            // Encerramento best-effort: o Windows limpa a pasta temporária depois.
        }
    }
}