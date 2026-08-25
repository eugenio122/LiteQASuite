namespace LiteFlow.Export;

/// <summary>
/// Uma evidência do jeito que o motor de exportação precisa dela: o caminho da
/// imagem, o texto e onde o texto vai.
///
/// <b>Passa o caminho, não os bytes</b>, de propósito. Um relatório de quarenta
/// prints em 4K seriam mais de cem megabytes se a lista inteira viesse carregada;
/// com o caminho, o motor lê uma imagem, escreve no documento e a solta antes de
/// ir para a próxima. O pico de memória é uma evidência, não o relatório.
/// </summary>
/// <param name="ImagePath">PNG no cache da sessão.</param>
/// <param name="Note">Texto que acompanha a evidência. Vazio não gera parágrafo.</param>
/// <param name="TextBelowImage"><c>true</c> põe o texto depois da imagem.</param>
public sealed record ExportEvidence(string ImagePath, string Note, bool TextBelowImage);