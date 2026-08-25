using System.Windows.Media;
using LiteFlow.ViewModels;

namespace LiteFlow.Icons;

/// <summary>
/// Ícones vetoriais da árvore do Workspace, um por tipo de nó. São
/// <see cref="Geometry"/> congeladas e sem cor própria — quem desenha pinta com o
/// <c>Foreground</c> do item, então elas acompanham o tema claro/escuro sozinhas.
///
/// Mesmo padrão do <c>Icons</c> da casca: placeholders razoáveis agora, trocáveis
/// por ícones definitivos depois sem tocar em mais nada.
/// </summary>
public static class WorkspaceIcons
{
    /// <summary>Maleta — squad/projeto.</summary>
    public static readonly Geometry Squad = Parse(
        "M10,4h4c1.1,0,2,0.9,2,2v1h3c1.1,0,2,0.9,2,2v9c0,1.1-0.9,2-2,2H5c-1.1,0-2-0.9-2-2V9c0-1.1,0.9-2,2-2h3V6" +
        "C8,4.9,8.9,4,10,4z M10,7h4V6h-4V7z");

    /// <summary>Pasta — ciclo/sprint.</summary>
    public static readonly Geometry Cycle = Parse(
        "M10,4H4C2.9,4,2,4.9,2,6v12c0,1.1,0.9,2,2,2h16c1.1,0,2-0.9,2-2V8c0-1.1-0.9-2-2-2h-8L10,4z");

    /// <summary>Prancheta — cenário. A mesma do módulo na barra lateral, de propósito.</summary>
    public static readonly Geometry Scenario = Parse(
        "M19,3h-4.18C14.4,1.84,13.3,1,12,1S9.6,1.84,9.18,3H5C3.9,3,3,3.9,3,5v14c0,1.1,0.9,2,2,2h14c1.1,0,2-0.9,2-2V5" +
        "C21,3.9,20.1,3,19,3z M12,3c0.55,0,1,0.45,1,1s-0.45,1-1,1s-1-0.45-1-1S11.45,3,12,3z M7,7h10v2H7V7z M7,11h10v2H7V11z" +
        "M7,15h7v2H7V15z");

    /// <summary>Documento com linhas — relatório .docx.</summary>
    public static readonly Geometry WordReport = Parse(
        "M14,2H6C4.9,2,4,2.9,4,4v16c0,1.1,0.9,2,2,2h12c1.1,0,2-0.9,2-2V8L14,2z M13,9V3.5L18.5,9H13z" +
        "M8,13h8v1.5H8V13z M8,16h8v1.5H8V16z");

    /// <summary>Documento com bloco sólido — relatório .pdf. A silhueta diferente é o que separa os dois de relance.</summary>
    public static readonly Geometry PdfReport = Parse(
        "M14,2H6C4.9,2,4,2.9,4,4v16c0,1.1,0.9,2,2,2h12c1.1,0,2-0.9,2-2V8L14,2z M13,9V3.5L18.5,9H13z" +
        "M7,14h10v4H7V14z");

    /// <summary>O ícone de um tipo de nó.</summary>
    public static Geometry For(WorkspaceNodeKind kind) => kind switch
    {
        WorkspaceNodeKind.Squad => Squad,
        WorkspaceNodeKind.Cycle => Cycle,
        WorkspaceNodeKind.Scenario => Scenario,
        WorkspaceNodeKind.WordReport => WordReport,
        WorkspaceNodeKind.PdfReport => PdfReport,
        _ => Cycle
    };

    private static Geometry Parse(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }
}