using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace LiteShot.Capture;

/// <summary>
/// Desenha anotações. Um lugar só, usado por dois consumidores muito diferentes:
/// o overlay, que redesenha a cada movimento do mouse, e a composição final, que
/// rasteriza uma única vez ao copiar ou salvar.
///
/// Ter os dois pelo mesmo código é o que garante que o resultado gravado seja
/// exatamente o que o usuário viu — no LiteShot antigo isso era garantido por
/// outro caminho (desenhar direto no bitmap), ao custo de não poder desfazer sem
/// clonar a tela inteira.
///
/// Trabalha sempre em coordenadas virtuais; quem chama aplica a transformação.
/// </summary>
public static class AnnotationRenderer
{
    /// <summary>Fonte do texto anotado. A mesma da interface do LiteQASuite.</summary>
    private static readonly Typeface TextTypeface = new(
        new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    /// <summary>Multiplicador da espessura no marcador, para o traço largo de marca-texto.</summary>
    private const double HighlighterWidthFactor = 4.0;

    /// <summary>Opacidade do marcador. O mesmo 80/255 do código antigo.</summary>
    private const byte HighlighterAlpha = 80;

    /// <summary>Tamanho da ponta da seta, em múltiplos da espessura.</summary>
    private const double ArrowHeadFactor = 4.0;

    /// <summary>Desenha todas as anotações de uma camada, na ordem de criação.</summary>
    public static void DrawAll(DrawingContext context, AnnotationLayer layer)
    {
        foreach (var annotation in layer.Items)
            Draw(context, annotation);

        if (layer.InProgress is { } current)
            Draw(context, current);
    }

    public static void Draw(DrawingContext context, Annotation annotation)
    {
        switch (annotation.Kind)
        {
            case AnnotationKind.Pen:
            case AnnotationKind.Highlighter:
                DrawFreehand(context, annotation);
                break;

            case AnnotationKind.Line:
                context.DrawLine(CreatePen(annotation), annotation.Start, annotation.End);
                break;

            case AnnotationKind.Arrow:
                DrawArrow(context, annotation);
                break;

            case AnnotationKind.Shape:
                context.DrawRectangle(null, CreatePen(annotation), ToRect(annotation));
                break;

            case AnnotationKind.Text:
                DrawText(context, annotation);
                break;
        }
    }

    private static void DrawFreehand(DrawingContext context, Annotation annotation)
    {
        var points = annotation.Path;
        if (points is null || points.Count == 0)
            return;

        // Um ponto só é um clique: vira um pingo, senão o traço some.
        if (points.Count == 1)
        {
            var radius = EffectiveThickness(annotation) / 2;
            context.DrawEllipse(new SolidColorBrush(EffectiveColor(annotation)), null, points[0], radius, radius);
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            ctx.PolyLineTo(points.Skip(1).ToList(), true, true);
        }

        geometry.Freeze();
        context.DrawGeometry(null, CreatePen(annotation), geometry);
    }

    private static void DrawArrow(DrawingContext context, Annotation annotation)
    {
        var pen = CreatePen(annotation);
        context.DrawLine(pen, annotation.Start, annotation.End);

        var dx = annotation.End.X - annotation.Start.X;
        var dy = annotation.End.Y - annotation.Start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 1)
            return;

        // Ponta como triângulo cheio, e não como duas linhas: fica sólida em
        // qualquer espessura e não abre quando a seta é curta.
        var head = annotation.Thickness * ArrowHeadFactor;
        var ux = dx / length;
        var uy = dy / length;

        var baseX = annotation.End.X - ux * head;
        var baseY = annotation.End.Y - uy * head;
        var halfWidth = head * 0.45;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(annotation.End, true, true);
            ctx.LineTo(new Point(baseX - uy * halfWidth, baseY + ux * halfWidth), true, false);
            ctx.LineTo(new Point(baseX + uy * halfWidth, baseY - ux * halfWidth), true, false);
        }

        geometry.Freeze();
        context.DrawGeometry(new SolidColorBrush(annotation.Color), null, geometry);
    }

    private static void DrawText(DrawingContext context, Annotation annotation)
    {
        if (string.IsNullOrEmpty(annotation.Text))
            return;

        var formatted = new FormattedText(
            annotation.Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            TextTypeface,
            FontSize(annotation.Thickness),
            new SolidColorBrush(annotation.Color),
            96);

        context.DrawText(formatted, annotation.Start);
    }

    /// <summary>
    /// Tamanho da fonte derivado da espessura, para que Ctrl+(+) e Ctrl+(−)
    /// controlem também o texto — como no LiteShot antigo.
    /// </summary>
    public static double FontSize(double thickness) => 10 + thickness * 3;

    private static Pen CreatePen(Annotation annotation)
    {
        var pen = new Pen(new SolidColorBrush(EffectiveColor(annotation)), EffectiveThickness(annotation));

        if (annotation.Kind == AnnotationKind.Highlighter)
        {
            // Pontas quadradas dão ao marcador o aspecto de caneta chanfrada.
            pen.StartLineCap = PenLineCap.Square;
            pen.EndLineCap = PenLineCap.Square;
        }
        else
        {
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            pen.LineJoin = PenLineJoin.Round;
        }

        pen.Freeze();
        return pen;
    }

    private static Color EffectiveColor(Annotation annotation) =>
        annotation.Kind == AnnotationKind.Highlighter
            ? Color.FromArgb(HighlighterAlpha, annotation.Color.R, annotation.Color.G, annotation.Color.B)
            : annotation.Color;

    private static double EffectiveThickness(Annotation annotation) =>
        annotation.Kind == AnnotationKind.Highlighter
            ? annotation.Thickness * HighlighterWidthFactor
            : annotation.Thickness;

    private static Rect ToRect(Annotation annotation) => new(
        Math.Min(annotation.Start.X, annotation.End.X),
        Math.Min(annotation.Start.Y, annotation.End.Y),
        Math.Abs(annotation.End.X - annotation.Start.X),
        Math.Abs(annotation.End.Y - annotation.Start.Y));
}