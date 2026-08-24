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

    public IReadOnlyList<string> GetCycles()
    {
        EnsureConfigured();
        return ChildFolderNames(_rootPath!);
    }

    public string EnsureCycle(string cycleName)
    {
        EnsureConfigured();
        var path = Path.Combine(_rootPath!, Sanitize(cycleName));
        Directory.CreateDirectory(path);
        return path;
    }

    public IReadOnlyList<string> GetScenarios(string cycleName)
    {
        EnsureConfigured();
        var cyclePath = Path.Combine(_rootPath!, Sanitize(cycleName));
        return Directory.Exists(cyclePath) ? ChildFolderNames(cyclePath) : Array.Empty<string>();
    }

    public string EnsureScenarioFolder(string cycleName, string scenarioId)
    {
        var cyclePath = EnsureCycle(cycleName);
        var path = Path.Combine(cyclePath, Sanitize(scenarioId));
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetScenarioFilePath(string cycleName, string scenarioId, string extension)
    {
        EnsureConfigured();
        var id = Sanitize(scenarioId);
        var folder = Path.Combine(_rootPath!, Sanitize(cycleName), id);
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

    private static void PersistRoot(string root)
    {
        var json = JsonSerializer.Serialize(new { root });
        File.WriteAllText(ConfigFilePath, json);
    }
}