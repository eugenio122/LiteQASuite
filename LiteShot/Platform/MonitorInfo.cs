namespace LiteShot.Platform;

/// <summary>
/// Um monitor físico, em pixels reais da área de trabalho virtual.
///
/// As coordenadas podem ser negativas: um monitor posicionado à esquerda do
/// primário tem <see cref="Left"/> negativo. Essa é a razão de toda a matemática
/// do LiteShot trabalhar com a união dos monitores e um deslocamento, em vez de
/// assumir que a tela começa em (0,0).
/// </summary>
/// <param name="Left">Borda esquerda em coordenadas virtuais.</param>
/// <param name="Top">Borda superior em coordenadas virtuais.</param>
/// <param name="Width">Largura em pixels físicos.</param>
/// <param name="Height">Altura em pixels físicos.</param>
/// <param name="IsPrimary">Se é o monitor primário do sistema.</param>
public sealed record MonitorInfo(int Left, int Top, int Width, int Height, bool IsPrimary)
{
    /// <summary>Borda direita (exclusiva).</summary>
    public int Right => Left + Width;

    /// <summary>Borda inferior (exclusiva).</summary>
    public int Bottom => Top + Height;

    /// <summary>Se o ponto virtual informado cai dentro deste monitor.</summary>
    public bool Contains(int x, int y) =>
        x >= Left && x < Right && y >= Top && y < Bottom;
}