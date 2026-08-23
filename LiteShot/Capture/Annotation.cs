using System.Windows;
using System.Windows.Media;

namespace LiteShot.Capture;

/// <summary>
/// Uma anotação desenhada sobre a captura — um traço, uma seta, um texto.
///
/// <b>Anotações são objetos, não pixels.</b> Essa é a mudança conceitual da fatia
/// de anotação: o LiteShot antigo desenhava direto num bitmap de camada e, para
/// permitir desfazer, clonava esse bitmap <i>inteiro</i> a cada traço, empilhando
/// as cópias. Numa área de trabalho com dois monitores 4K, cada clone passava de
/// 60 MB, e a pilha não tinha limite.
///
/// Guardando objetos, desfazer é remover o último item da lista, e a imagem só é
/// rasterizada uma vez — no momento de copiar ou salvar.
///
/// <b>Coordenadas:</b> todas em pixels físicos da área de trabalho virtual, o mesmo
/// sistema da seleção. Quem desenha converte para o seu próprio sistema.
///
/// Um tipo só cobre as seis ferramentas em vez de uma hierarquia de seis classes:
/// elas compartilham quase todos os dados, e os campos que sobram
/// (<see cref="Path"/> para traço livre, <see cref="Text"/> para texto) são
/// explicitamente opcionais.
/// </summary>
public sealed class Annotation
{
    /// <summary>Qual ferramenta produziu esta anotação.</summary>
    public required AnnotationKind Kind { get; init; }

    /// <summary>Cor do traço. O marcador aplica transparência na hora de desenhar.</summary>
    public required Color Color { get; init; }

    /// <summary>Espessura do traço, em pixels físicos.</summary>
    public required double Thickness { get; init; }

    /// <summary>Ponto inicial — canto do retângulo, origem da linha, âncora do texto.</summary>
    public Point Start { get; set; }

    /// <summary>Ponto final. Vai sendo atualizado enquanto o usuário arrasta.</summary>
    public Point End { get; set; }

    /// <summary>
    /// O caminho percorrido, para caneta e marcador. Nulo nas demais ferramentas.
    /// </summary>
    public List<Point>? Path { get; init; }

    /// <summary>O conteúdo digitado, para a ferramenta de texto. Nulo nas demais.</summary>
    public string? Text { get; set; }

    /// <summary>Ferramentas que se desenham arrastando de um ponto a outro.</summary>
    public bool IsDrag => Kind is AnnotationKind.Line or AnnotationKind.Arrow or AnnotationKind.Shape;

    /// <summary>Ferramentas que acompanham o movimento do mouse ponto a ponto.</summary>
    public bool IsFreehand => Kind is AnnotationKind.Pen or AnnotationKind.Highlighter;
}