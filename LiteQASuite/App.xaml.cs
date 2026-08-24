using LiteQASuite.Core.Events;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Modules;
using LiteQASuite.Core.Session;
using LiteQASuite.Core.Workspace;
using LiteQASuite.Platform;
using LiteQASuite.Shell.Notifications;
using LiteQASuite.Shell.ViewModels;
using LiteQASuite.Shell.Views;
using LiteShot;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;

namespace LiteQASuite;

/// <summary>
/// Ponto de entrada da aplicação e composition root: o único lugar que conhece
/// todos os projetos. Cria os serviços do Core e da casca, instancia os módulos,
/// injeta-os no <see cref="ShellViewModel"/> e sobe a <see cref="ShellWindow"/>.
/// Também garante instância única, faz o first-run do Workspace, e encerra os
/// módulos de forma limpa na saída.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = "LiteQASuite-Single-Instance-8F3A2C10-4D5E-4B6A-9C7D-1E2F3A4B5C6D";

    private Mutex? _singleInstanceMutex;
    private List<IModule> _modules = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        // --- Instância única ---
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool isNew);
        if (!isNew)
        {
            NativeMethods.BringToFront("LiteQASuite");
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // --- Serviços do Core e da casca ---
        var events = new EventBus();
        var session = new SessionContext();
        var notifications = new NotificationService();
        var workspace = new WorkspaceService();

        // --- First-run do Workspace ---
        if (!workspace.IsConfigured && !ConfigureWorkspace(workspace))
        {
            Shutdown();
            return;
        }

        ILanguageManager language;
        try
        {
            var langFolder = Path.Combine(AppContext.BaseDirectory, "Lang");
            language = new LanguageManager(langFolder, "pt-BR");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Falha ao carregar os idiomas:\n\n{ex.Message}\n\n" +
                "Confirme que a pasta 'Lang' e o 'pt-BR.json' estão sendo copiados para a saída.",
                "LiteQASuite", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // --- Contexto compartilhado e módulos ---
        var context = new ModuleContext(events, session, language, notifications, workspace);
        _modules = new List<IModule>
        {
            new LiteShotModule(context)
            // depois: new LiteFlowModule(context), new LiteJsonModule(context), new LiteAutomationModule(context)
        };

        // --- Casca ---
        var shell = new ShellWindow { DataContext = new ShellViewModel(_modules, language, workspace) };
        shell.Show();
    }

    private static bool ConfigureWorkspace(IWorkspaceService workspace)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Escolha onde criar a pasta do Workspace do LiteQASuite"
        };

        if (dialog.ShowDialog() != true)
        {
            MessageBox.Show(
                "O LiteQASuite precisa de uma pasta de Workspace para salvar as evidências.",
                "LiteQASuite", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        workspace.Configure(dialog.FolderName);
        return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        foreach (var module in _modules)
        {
            try { module.Shutdown(); }
            catch { /* encerramento best-effort */ }
        }

        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}