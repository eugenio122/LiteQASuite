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
///     &lt;squad/projeto&gt;/
///         &lt;ciclo/sprint&gt;/
///             &lt;id&gt;/
///                 id.lflow
///                 id.json
/// </code>
///
/// <b>O nível de squad/projeto existe porque um QA circula entre times.</b> Cobrir
/// alguém por duas semanas, ou dividir a semana entre dois times, faria os ciclos
/// de todos eles se misturarem numa lista só — e "Sprint 12" de um time não tem
/// nada a ver com a "Sprint 12" do outro. Separar na pasta é o que mantém o
/// Workspace legível quando o trabalho não é de um time só.
/// </summary>
public interface IWorkspaceService
{
    /// <summary><c>true</c> quando já há uma raiz de Workspace válida configurada.</summary>
    bool IsConfigured { get; }

    /// <summary>Raiz do Workspace (<c>.../LiteQASuite Workspace</c>), ou <c>null</c> se não configurado.</summary>
    string? RootPath { get; }

    /// <summary>Cria a pasta "LiteQASuite Workspace" dentro de <paramref name="parentFolder"/> e persiste a escolha.</summary>
    void Configure(string parentFolder);

    /// <summary>Nomes dos squads/projetos existentes.</summary>
    IReadOnlyList<string> GetSquads();

    /// <summary>Garante a pasta do squad/projeto (cria se faltar) e devolve o caminho.</summary>
    string EnsureSquad(string squadName);

    /// <summary>Nomes dos ciclos/sprints dentro de um squad.</summary>
    IReadOnlyList<string> GetCycles(string squadName);

    /// <summary>Garante a pasta do ciclo (cria o squad também, se faltar) e devolve o caminho.</summary>
    string EnsureCycle(string squadName, string cycleName);

    /// <summary>IDs dos cenários dentro de um ciclo.</summary>
    IReadOnlyList<string> GetScenarios(string squadName, string cycleName);

    /// <summary>Garante a pasta do cenário (<c>&lt;raiz&gt;/&lt;squad&gt;/&lt;ciclo&gt;/&lt;id&gt;/</c>) e devolve o caminho.</summary>
    string EnsureScenarioFolder(string squadName, string cycleName, string scenarioId);

    /// <summary>Caminho de um arquivo do cenário, ex.: <c>.../&lt;id&gt;/&lt;id&gt;.lflow</c>.</summary>
    string GetScenarioFilePath(string squadName, string cycleName, string scenarioId, string extension);
}