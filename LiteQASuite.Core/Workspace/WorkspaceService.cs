using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LiteQASuite.Core.Storage;

namespace LiteQASuite.Core.Workspace;

/// <summary>
/// Implementação de <see cref="IWorkspaceService"/>. IO puro (sem WPF), por isso
/// vive no Core como o EventBus. Persiste a raiz escolhida num pequeno arquivo em
/// <see cref="AppPaths.UserData"/>, e nunca toca no conteúdo dos artefatos dos módulos.
///
/// A hierarquia é sempre <c>squad → ciclo → cenário</c>, e cada nível garante o
/// anterior: pedir a pasta de um cenário cria o squad e o ciclo se faltarem. É o
/// que evita que um erro de ordem de chamada deixe meia estrutura no disco.
/// </summary>
public sealed class WorkspaceService : IWorkspaceService
{
    private const string WorkspaceFolderName = "LiteQASuite Workspace";
    private const string ConfigFileName = "workspace.json";

    private string? _rootPath;

    public WorkspaceService()
    {
        _rootPath = LoadPersistedRoot();
    }

    public bool IsConfigured => _rootPath is not null && Directory.Exists(_rootPath);

    public string? RootPath => _rootPath;

    public void Configure(string parentFolder)
    {
        var root = Path.Combine(parentFolder, WorkspaceFolderName);
        Directory.CreateDirectory(root);
        _rootPath = root;
        PersistRoot(root);
    }

    public IReadOnlyList<string> GetSquads()
    {
        EnsureConfigured();
        return ChildFolderNames(_rootPath!);
    }

    public string EnsureSquad(string squadName)
    {
        EnsureConfigured();
        var path = Path.Combine(_rootPath!, Sanitize(squadName));
        Directory.CreateDirectory(path);
        return path;
    }

    public IReadOnlyList<string> GetCycles(string squadName)
    {
        EnsureConfigured();
        var squadPath = Path.Combine(_rootPath!, Sanitize(squadName));
        return Directory.Exists(squadPath) ? ChildFolderNames(squadPath) : Array.Empty<string>();
    }

    public string EnsureCycle(string squadName, string cycleName)
    {
        var squadPath = EnsureSquad(squadName);
        var path = Path.Combine(squadPath, Sanitize(cycleName));
        Directory.CreateDirectory(path);
        return path;
    }

    public IReadOnlyList<string> GetScenarios(string squadName, string cycleName)
    {
        EnsureConfigured();
        var cyclePath = Path.Combine(_rootPath!, Sanitize(squadName), Sanitize(cycleName));
        return Directory.Exists(cyclePath) ? ChildFolderNames(cyclePath) : Array.Empty<string>();
    }

    public string EnsureScenarioFolder(string squadName, string cycleName, string scenarioId)
    {
        var cyclePath = EnsureCycle(squadName, cycleName);
        var path = Path.Combine(cyclePath, Sanitize(scenarioId));
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetScenarioFilePath(string squadName, string cycleName, string scenarioId, string extension)
    {
        EnsureConfigured();
        var id = Sanitize(scenarioId);
        var folder = Path.Combine(_rootPath!, Sanitize(squadName), Sanitize(cycleName), id);
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return Path.Combine(folder, id + ext);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("O Workspace ainda não foi configurado.");
    }

    private static IReadOnlyList<string> ChildFolderNames(string parent) =>
        Directory.EnumerateDirectories(parent)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.CurrentCulture)
            .ToList();

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(cleaned) ? "_" : cleaned;
    }

    private static string ConfigFilePath => Path.Combine(AppPaths.UserData, ConfigFileName);

    private static string? LoadPersistedRoot()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
                return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(ConfigFilePath));
            return doc.RootElement.TryGetProperty("root", out var r) ? r.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Grava a raiz escolhida. Não lança: com o aplicativo portátil, a pasta
    /// <c>Data</c> pode estar num lugar sem permissão de escrita — e nesse caso o
    /// Workspace continua funcionando nesta sessão, só não é lembrado na próxima.
    /// Derrubar o first-run por causa disso seria pior.
    /// </summary>
    private static void PersistRoot(string root)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { root });
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception)
        {
            // Sem persistência: o first-run volta a perguntar na próxima abertura.
        }
    }
}