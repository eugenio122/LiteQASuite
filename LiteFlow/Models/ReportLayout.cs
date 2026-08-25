namespace LiteFlow.Models;

/// <summary>
/// Como as evidências se organizam no relatório exportado.
///
/// O modo <c>Compacto</c> do LiteFlow 1.x foi removido — era duas colunas com
/// altura fixa, redundante com o Mobile de duas colunas. Os <c>.lflow</c> antigos
/// que gravaram o valor <c>2</c> são lidos como <see cref="Padrao"/>; por isso os
/// dois valores que sobraram mantêm os números originais (0 e 1) em vez de serem
/// renumerados.
/// </summary>
public enum ReportLayout
{
    /// <summary>Uma evidência por bloco, ocupando a largura da página.</summary>
    Padrao = 0,

    /// <summary>Evidências lado a lado em N colunas — prints de celular são estreitos.</summary>
    Mobile = 1
}