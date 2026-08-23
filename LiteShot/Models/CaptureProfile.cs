using System.Text.Json.Serialization;

namespace LiteShot.Models;

/// <summary>
/// Um perfil de espaço de trabalho da captura. Existem exatamente dois (P1 e P2),
/// pensados para quem alterna entre contextos diferentes — testar mobile e web
/// desktop na mesma sessão, por exemplo — sem ter que reajustar a área de seleção
/// a cada troca.
///
/// <b>O perfil tem duas naturezas, e a distinção importa:</b>
///
/// <list type="bullet">
///   <item>
///     <b>Configuração</b> — <see cref="NavbarVertical"/>, <see cref="KeepSelection"/> e
///     <see cref="KeepNavbarPosition"/>. São escolhas deliberadas do usuário, feitas na
///     tela de configurações e gravadas pelo botão Salvar. O overlay nunca as altera.
///   </item>
///   <item>
///     <b>Estado</b> — a geometria (seleção e barra) e as cores. Isto o overlay
///     escreve sozinho, de forma implícita, conforme o usuário trabalha: mover a
///     seleção, arrastar a barra, trocar a cor. Ninguém edita isso por formulário.
///   </item>
/// </list>
///
/// A geometria é guardada como <see cref="int"/> soltos, e não como Rectangle/Point,
/// de propósito: o JSON fica limpo (Rectangle serializa Left/Top/Right/Bottom
/// calculados junto) e o arquivo de configuração não fica amarrado a um tipo de
/// biblioteca gráfica.
/// </summary>
public sealed class CaptureProfile
{
    /// <summary>Identificador do perfil: 1 ou 2. Não é índice de lista.</summary>
    public int Id { get; set; }

    // ------------------------------------------------------- Configuração

    /// <summary>Se a próxima captura deve restaurar a área de seleção guardada aqui.</summary>
    public bool KeepSelection { get; set; }

    /// <summary>Se a próxima captura deve restaurar a posição da barra guardada aqui.</summary>
    public bool KeepNavbarPosition { get; set; }

    /// <summary>Orientação da barra de ferramentas flutuante neste perfil.</summary>
    public bool NavbarVertical { get; set; }

    // ------------------------------------------------------------- Estado

    /// <summary>Canto esquerdo da última área selecionada, em pixels físicos.</summary>
    public int SelectionX { get; set; }

    /// <summary>Topo da última área selecionada, em pixels físicos.</summary>
    public int SelectionY { get; set; }

    /// <summary>Largura da última área selecionada. Zero significa "nunca definida".</summary>
    public int SelectionWidth { get; set; }

    /// <summary>Altura da última área selecionada. Zero significa "nunca definida".</summary>
    public int SelectionHeight { get; set; }

    /// <summary>Posição horizontal da barra de ferramentas, em pixels físicos.</summary>
    public int NavbarX { get; set; }

    /// <summary>Posição vertical da barra de ferramentas, em pixels físicos.</summary>
    public int NavbarY { get; set; }

    /// <summary>Cor hexadecimal das ferramentas de desenho (caneta, linha, seta, forma).</summary>
    public string LastColor { get; set; } = DefaultColor;

    /// <summary>Cor hexadecimal da ferramenta marcador (aplicada com transparência).</summary>
    public string LastHighlightColor { get; set; } = DefaultHighlightColor;

    // ---------------------------------------------------------- Constantes

    /// <summary>Vermelho — cor inicial das ferramentas de desenho.</summary>
    public const string DefaultColor = "#FF0000";

    /// <summary>Amarelo — cor inicial do marcador.</summary>
    public const string DefaultHighlightColor = "#FFFF00";

    // ------------------------------------------------------------- Apoio

    /// <summary>
    /// Verdadeiro quando há uma área guardada com tamanho útil. Substitui o teste
    /// de <c>Rectangle.Empty</c> do código antigo.
    /// </summary>
    [JsonIgnore]
    public bool HasSelection => SelectionWidth > 0 && SelectionHeight > 0;

    /// <summary>Verdadeiro quando a barra já foi arrastada para uma posição própria.</summary>
    [JsonIgnore]
    public bool HasNavbarPosition => NavbarX != 0 || NavbarY != 0;

    /// <summary>Cria um perfil zerado com o id informado.</summary>
    public static CaptureProfile CreateDefault(int id) => new() { Id = id };

    /// <summary>
    /// Cópia só da parte de <b>configuração</b>, para a tela editar sem tocar no
    /// perfil real. A geometria e as cores ficam de fora de propósito: são estado
    /// do overlay, e a tela não tem nada que mexer neles.
    /// </summary>
    public CaptureProfile CloneConfig() => new()
    {
        Id = Id,
        KeepSelection = KeepSelection,
        KeepNavbarPosition = KeepNavbarPosition,
        NavbarVertical = NavbarVertical
    };

    /// <summary>
    /// Traz de volta a configuração editada na tela, no momento do Salvar. Só os
    /// três campos de configuração são copiados — a geometria e as cores deste
    /// perfil continuam como o overlay as deixou, mesmo que a tela esteja aberta
    /// desde antes da última captura.
    /// </summary>
    public void ApplyConfigFrom(CaptureProfile source)
    {
        KeepSelection = source.KeepSelection;
        KeepNavbarPosition = source.KeepNavbarPosition;
        NavbarVertical = source.NavbarVertical;
    }
}