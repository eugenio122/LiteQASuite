using System.Collections.Generic;

namespace LiteQASuite.Core.Workspace;

/// <summary>
/// Dono da <b>estrutura de pastas</b> do Workspace do LiteQASuite — o chão
/// compartilhado onde LiteFlow, LiteJson e LiteAutomation gravam e leem. Conhece
/// só pastas e IDs; nunca o conteúdo dos arquivos (o <c>.lflow</c> é do LiteFlow,
/// o <c>.json</c> é do LiteJson).
///
/// Layout:
/// <code>
/// &lt;pasta escolhida&gt;/LiteQASuite Workspace/
///     &lt;ciclo&gt;/
///         &lt;id&gt;/
///             id.lflow
///             id.json
/// </code>
/// </summary>
public interface IWorkspaceService
{
    /// <summary><c>true</c> quando já há uma raiz de Workspace válida configurada.</summary>
    bool IsConfigured { get; }

    /// <summary>Raiz do Workspace (<c>.../LiteQASuite Workspace</c>), ou <c>null</c> se não configurado.</summary>
    string? RootPath { get; }

    /// <summary>Cria a pasta "LiteQASuite Workspace" dentro de <paramref name="parentFolder"/> e persiste a escolha.</summary>
    void Configure(string parentFolder);

    /// <summary>Nomes dos ciclos/sprints existentes.</summary>
    IReadOnlyList<string> GetCycles();

    /// <summary>Garante a pasta do ciclo (cria se faltar) e devolve o caminho.</summary>
    string EnsureCycle(string cycleName);

    /// <summary>IDs dos cenários dentro de um ciclo.</summary>
    IReadOnlyList<string> GetScenarios(string cycleName);

    /// <summary>Garante a pasta do cenário (<c>&lt;raiz&gt;/&lt;ciclo&gt;/&lt;id&gt;/</c>) e devolve o caminho.</summary>
    string EnsureScenarioFolder(string cycleName, string scenarioId);

    /// <summary>Caminho de um arquivo do cenário, ex.: <c>.../&lt;id&gt;/&lt;id&gt;.lflow</c>.</summary>
    string GetScenarioFilePath(string cycleName, string scenarioId, string extension);
}