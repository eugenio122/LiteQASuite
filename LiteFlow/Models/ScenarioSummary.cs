namespace LiteFlow.Models;

/// <summary>
/// O cabeçalho de um <c>.lflow</c> — o suficiente para rotular o cenário na árvore
/// do Workspace, sem abrir o arquivo inteiro.
///
/// <b>Existe por causa do formato.</b> As imagens moram dentro do <c>.lflow</c>,
/// então "ler o cenário para mostrar o nome dele" custaria centenas de megabytes
/// numa árvore com trinta cenários. Como os metadados são gravados <i>antes</i> do
/// array de passos, dá para ler só o começo do arquivo e parar — e é isso que o
/// <c>ScenarioStore.ReadSummary</c> faz.
/// </summary>
/// <param name="ScenarioId">O ID do cenário.</param>
/// <param name="TestCaseName">O caso de teste, que serve de nome legível.</param>
public sealed record ScenarioSummary(string ScenarioId, string TestCaseName);