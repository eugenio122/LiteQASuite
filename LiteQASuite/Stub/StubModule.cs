using LiteQASuite.Core;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Modules;
using LiteQASuite.Stub;
using System.Windows;

namespace LiteQASuite.Stub;

/// <summary>
/// Módulo de teste, dono do chat do Shell. Cumpre o contrato <see cref="IModule"/>
/// inteiro (recebe o <see cref="ModuleContext"/>, escopa strings, cria a View sob
/// demanda) sem tocar em nenhum módulo real. Quando o LiteShot ficar pronto, o
/// composition root troca <c>new StubModule(...)</c> por <c>new LiteShotModule(...)</c>
/// e este arquivo pode ser apagado.
/// </summary>
public sealed class StubModule : IModule
{
    public const string ModuleId = "Stub";

    private readonly ModuleContext _context;
    private readonly IModuleStrings _strings;
    private StubView? _view;

    public StubModule(ModuleContext context)
    {
        _context = context;
        _strings = context.Language.ForModule(ModuleId);
    }

    public string Id => ModuleId;

    public string DisplayName => _strings.GetString("Module.Name");

    public FrameworkElement View => _view ??= new StubView { DataContext = new StubViewModel(_context) };

    public void Shutdown() { }
}