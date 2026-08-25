using System.Collections.Generic;

namespace LiteFlow.Models;

/// <summary>
/// Uma anotação desenhada sobre uma evidência. É <b>objeto, não pixel</b>: fica
/// gravada no <c>.lflow</c> ao lado do print original e só vira imagem na
/// exportação. É isso que permite reabrir um cenário de ontem e arrastar a seta
/// que ficou no campo errado — em vez de refazer o print.
///
/// <b>Por que os pontos são uma lista plana de <c>double</c>.</b> Um tipo
/// <c>Point</c> do WPF serializaria como <c>{"X":..,"Y":..}</c> e amarraria o
/// arquivo a uma biblioteca gráfica — a mesma razão pela qual o LiteShot guarda a
/// geometria dos perfis como <c>int</c> soltos. Aqui os valores vão em pares
/// (x0, y0, x1, y1, …), o que serve às seis ferramentas com uma estrutura só:
/// a caneta usa a sequência inteira, linha/seta/forma usam dois pares, e o texto
/// usa um.
///
/// <b>As coordenadas são em pixels da imagem</b>, nunca em pixels da tela. O canvas
/// escala a imagem para caber, e essa escala muda quando a janela muda de tamanho —
/// guardar coordenada de tela faria a anotação andar sozinha ao reabrir o cenário
/// noutro monitor.
/// </summary>
public sealed class Annotation
{
    /// <summary>Qual ferramenta desenhou isto.</summary>
    public AnnotationKind Kind { get; set; }

    /// <summary>
    /// Cor no formato <c>#AARRGGBB</c>. String, e não um <c>Color</c>, pelo mesmo
    /// motivo dos pontos: o arquivo não conhece WPF. O alfa importa — o marcador
    /// é translúcido.
    /// </summary>
    public string Color { get; set; } = "#FFFF0000";

    /// <summary>Espessura do traço, em pixels da imagem.</summary>
    public double Thickness { get; set; } = 4;

    /// <summary>Pares (x, y) em pixels da imagem. Ver observação na classe.</summary>
    public List<double> Points { get; set; } = new();

    /// <summary>O conteúdo, quando <see cref="Kind"/> é <see cref="AnnotationKind.Text"/>.</summary>
    public string Text { get; set; } = "";

    /// <summary>Fonte do texto. Ignorado pelas demais ferramentas.</summary>
    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>Corpo da fonte, <b>em pixels da imagem</b> — não em DIP da tela.</summary>
    public double FontSize { get; set; } = 14;

    /// <summary>Cópia independente, para o autosave em segundo plano não ler o que a UI está editando.</summary>
    public Annotation Clone() => new()
    {
        Kind = Kind,
        Color = Color,
        Thickness = Thickness,
        Points = new List<double>(Points),
        Text = Text,
        FontFamily = FontFamily,
        FontSize = FontSize
    };
}