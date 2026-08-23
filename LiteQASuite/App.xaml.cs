using LiteQASuite.Core;
using LiteQASuite.Core.Events;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Modules;
using LiteQASuite.Core.Session;
using LiteQASuite.Shell;
using LiteQASuite.Shell.ViewModels;
using LiteQASuite.Stub;
using LiteShot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace LiteQASuite;

/// <summary>
/// Ponto de entrada da aplicação e composition root: o único lugar que conhece
/// todos os projetos. Cria os serviços do Core, instancia os módulos, injeta-os
/// no <see cref="ShellViewModel"/> e sobe a <see cref="ShellWindow"/>.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // --- Serviços do Core ---
        var events = new EventBus();
        var session = new SessionContext();

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
        var context = new ModuleContext(events, session, language);
        var modules = new List<IModule>
        {
            new LiteShotModule(context)
            // depois: new LiteShotModule(context), new LiteFlowModule(context), ...
        };

        // --- Casca ---
        var shell = new ShellWindow { DataContext = new ShellViewModel(modules) };
        shell.Show();
    }
}