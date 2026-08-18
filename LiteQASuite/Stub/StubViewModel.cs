using LiteQASuite.Core;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Modules;
using LiteQASuite.Core.Mvvm;
using LiteQASuite.Stub;

namespace LiteQASuite.Stub;

/// <summary>
/// ViewModel do módulo de teste. Existe só para provar o contrato ponta a ponta
/// e servir de gabarito vivo para os módulos reais (LiteShot etc.): mostra como
/// puxar strings escopadas e reagir à troca de idioma.
/// </summary>
public sealed class StubViewModel : ViewModelBase
{
    private readonly IModuleStrings _strings;

    public StubViewModel(ModuleContext context)
    {
        _strings = context.Language.ForModule(StubModule.ModuleId);

        // Strings não são recurso dinâmico: ao trocar o idioma, re-notifica tudo.
        // (Num módulo real, cancele esta assinatura no Shutdown para não vazar.)
        context.Language.LanguageChanged += () => OnPropertyChanged(null);
    }

    public string Title => _strings.GetString("Module.Name");
    public string Greeting => _strings.GetString("Greeting");
}