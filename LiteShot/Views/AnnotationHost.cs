using System.Windows;
using System.Windows.Media;
using LiteShot.Capture;

namespace LiteShot.Views;

/// <summary>
/// A camada que desenha as anotações sobre o overlay.
///
/// É um <see cref="FrameworkElement"/> cru, com <c>OnRender</c> próprio, em vez de
/// uma pilha de formas no Canvas: são dezenas de traços redesenhados a cada
/// movimento do mouse, e criar objetos visuais para cada um deles faria o WPF
/// manter uma árvore que só cresce durante a captura.
///
/// Não recebe cliques: quem trata o mouse é a janela, que já converte coordenadas.
/// </summary>
public sealed class AnnotationHost : FrameworkElement
{
    private readonly CaptureSession _session;
    private Matrix _transform = Matrix.Identity;

    public AnnotationHost(CaptureSession session)
    {
        _session = session;
        IsHitTestVisible = false;
    }

    /// <summary>
    /// Define a conversão de coordenada virtual para coordenada local desta janela.
    /// Como cada monitor tem seu próprio fator de escala, cada camada recebe a sua.
    /// </summary>
    public void SetTransform(double scale, int monitorLeft, int monitorTop)
    {
        _transform = new Matrix(scale, 0, 0, scale, -monitorLeft * scale, -monitorTop * scale);
        InvalidateVisual();
    }

    /// <summary>Pede um redesenho — chamado quando a sessão avisa que algo mudou.</summary>
    public void Refresh() => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (!_session.HasSelection)
            return;

        var selection = _session.Selection;

        drawingContext.PushTransform(new MatrixTransform(_transform));

        // As anotações são recortadas pela seleção: um traço que escapa para fora
        // não apareceria na imagem final, então não deve aparecer na lente também.
        drawingContext.PushClip(new RectangleGeometry(
            new Rect(selection.Left, selection.Top, selection.Width, selection.Height)));

        AnnotationRenderer.DrawAll(drawingContext, _session.Annotations);

        drawingContext.Pop();
        drawingContext.Pop();
    }
}