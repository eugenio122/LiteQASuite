using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using LiteFlow.Models;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Mvvm;
using LiteQASuite.Core.Workspace;

namespace LiteFlow.ViewModels;

/// <summary>
/// O diálogo de iniciar cenário. Coleta os metadados do relatório e valida o que
/// precisa ser válido <b>antes</b> de qualquer pasta ser criada — um cenário
/// nascido pela metade deixaria lixo no Workspace que ninguém sabe se pode apagar.
///
/// <b>Squad e ciclo são combos editáveis</b>, e não listas com um botão "novo" ao
/// lado: escolher um existente e criar um novo são a mesma ação — digitar um nome.
/// Trocar de squad recarrega a lista de ciclos, porque "Sprint 12" de um time não
/// é a "Sprint 12" do outro.
///
/// <b>Não há "Pasta de Saída".</b> Quem resolve onde o cenário mora é o
/// <c>IWorkspaceService</c>; o que o usuário escolhe aqui é o lugar na hierarquia.
/// </summary>
public sealed class StartScenarioViewModel : ViewModelBase
{
    private readonly IModuleStrings _strings;
    private readonly IWorkspaceService _workspace;

    private string _squad;
    private string _cycle;
    private string _prefix;
    private string _scenarioId = "";
    private string _testCase = "";
    private string _executor;
    private string _date = DateTime.Now.ToString("dd/MM/yyyy");
    private string _comments = "";
    private int _layoutIndex;
    private int _mobileColumns;
    private string _errorMessage = "";

    public StartScenarioViewModel(IModuleStrings strings, IWorkspaceService workspace, LiteFlowSettings settings)
    {
        _strings = strings;
        _workspace = workspace;

        Squads = workspace.GetSquads().ToList();

        _squad = !string.IsNullOrWhiteSpace(settings.LastSquad) ? settings.LastSquad
               : Squads.Count > 0 ? Squads[^1]
               : "";

        ReloadCycles();

        _cycle = !string.IsNullOrWhiteSpace(settings.LastCycle) && Cycles.Contains(settings.LastCycle)
               ? settings.LastCycle
               : Cycles.Count > 0 ? Cycles[^1]
               : "";

        _prefix = settings.DefaultPrefix;
        _executor = settings.DefaultExecutor;
        _layoutIndex = (int)settings.DefaultLayout;
        _mobileColumns = settings.DefaultMobileColumns;

        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(false));
    }

    /// <summary>Pedido de fechamento: <c>true</c> confirmou, <c>false</c> desistiu.</summary>
    public event Action<bool>? RequestClose;

    /// <summary>Squads já existentes no Workspace. Fixa: criar squad é digitar um nome.</summary>
    public IReadOnlyList<string> Squads { get; }

    /// <summary>Ciclos do squad escolhido. Recarrega quando o squad muda.</summary>
    public ObservableCollection<string> Cycles { get; } = new();

    public IReadOnlyList<int> MobileColumnOptions { get; } = new[] { 1, 2, 3 };

    public ICommand ConfirmCommand { get; }

    public ICommand CancelCommand { get; }

    public string Squad
    {
        get => _squad;
        set
        {
            if (!SetProperty(ref _squad, value)) return;

            ErrorMessage = "";
            ReloadCycles();
        }
    }

    public string Cycle
    {
        get => _cycle;
        set { if (SetProperty(ref _cycle, value)) ErrorMessage = ""; }
    }

    public string Prefix
    {
        get => _prefix;
        set => SetProperty(ref _prefix, value);
    }

    public string ScenarioId
    {
        get => _scenarioId;
        set { if (SetProperty(ref _scenarioId, value)) ErrorMessage = ""; }
    }

    public string TestCase
    {
        get => _testCase;
        set => SetProperty(ref _testCase, value);
    }

    public string Executor
    {
        get => _executor;
        set => SetProperty(ref _executor, value);
    }

    public string Date
    {
        get => _date;
        set => SetProperty(ref _date, value);
    }

    public string Comments
    {
        get => _comments;
        set => SetProperty(ref _comments, value);
    }

    /// <summary>0 = Padrão, 1 = Mobile. Índice, e não o enum, para o combo bindar sem conversor.</summary>
    public int LayoutIndex
    {
        get => _layoutIndex;
        set
        {
            if (SetProperty(ref _layoutIndex, value))
                OnPropertyChanged(nameof(IsMobileLayout));
        }
    }

    /// <summary>Habilita o combo de colunas — só faz sentido no layout Mobile.</summary>
    public bool IsMobileLayout => _layoutIndex == (int)ReportLayout.Mobile;

    public int MobileColumns
    {
        get => _mobileColumns;
        set => SetProperty(ref _mobileColumns, value);
    }

    /// <summary>Mensagem de validação. Vazia esconde o aviso na tela.</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>O layout escolhido, já como enum.</summary>
    public ReportLayout Layout => IsMobileLayout ? ReportLayout.Mobile : ReportLayout.Padrao;

    public string Title => _strings.GetString("Scenario.Title");
    public string LabelSquad => _strings.GetString("Scenario.Squad");
    public string LabelSquadHint => _strings.GetString("Scenario.SquadHint");
    public string LabelCycle => _strings.GetString("Scenario.Cycle");
    public string LabelCycleHint => _strings.GetString("Scenario.CycleHint");
    public string LabelPrefix => _strings.GetString("Scenario.Prefix");
    public string LabelScenarioId => _strings.GetString("Scenario.Id");
    public string LabelTestCase => _strings.GetString("Scenario.TestCase");
    public string LabelExecutor => _strings.GetString("Scenario.Executor");
    public string LabelDate => _strings.GetString("Scenario.Date");
    public string LabelComments => _strings.GetString("Scenario.Comments");
    public string LabelLayout => _strings.GetString("Scenario.Layout");
    public string LabelLayoutPadrao => _strings.GetString("Scenario.Layout.Padrao");
    public string LabelLayoutMobile => _strings.GetString("Scenario.Layout.Mobile");
    public string LabelMobileColumns => _strings.GetString("Scenario.MobileColumns");
    public string LabelConfirm => _strings.GetString("Scenario.Confirm");
    public string LabelCancel => _strings.GetString("Scenario.Cancel");

    private void ReloadCycles()
    {
        Cycles.Clear();

        if (string.IsNullOrWhiteSpace(_squad)) return;

        foreach (var cycle in _workspace.GetCycles(_squad.Trim()))
            Cycles.Add(cycle);
    }

    private void Confirm()
    {
        var squad = (Squad ?? "").Trim();
        var cycle = (Cycle ?? "").Trim();
        var id = (ScenarioId ?? "").Trim();

        if (string.IsNullOrEmpty(squad))
        {
            ErrorMessage = _strings.GetString("Scenario.Error.SquadRequired");
            return;
        }

        if (string.IsNullOrEmpty(cycle))
        {
            ErrorMessage = _strings.GetString("Scenario.Error.CycleRequired");
            return;
        }

        if (string.IsNullOrEmpty(id))
        {
            ErrorMessage = _strings.GetString("Scenario.Error.IdRequired");
            return;
        }

        // O WorkspaceService troca caractere inválido por "_" em silêncio. Preferimos
        // avisar: um nome que vira outro na pasta é confusão garantida na hora de
        // procurar o cenário depois.
        if (HasInvalidNameChars(squad) || HasInvalidNameChars(cycle) || HasInvalidNameChars(id))
        {
            ErrorMessage = _strings.GetString("Scenario.Error.InvalidChars");
            return;
        }

        if (_workspace.GetScenarios(squad, cycle).Any(name => string.Equals(name, id, StringComparison.OrdinalIgnoreCase)))
        {
            ErrorMessage = _strings.GetString("Scenario.Error.IdExists");
            return;
        }

        // Os valores aparados são gravados nos campos, e não pelas propriedades: o
        // setter de Squad recarrega a lista de ciclos, e recarregá-la agora
        // esvaziaria o combo bem no instante em que o diálogo fecha.
        _squad = squad;
        _cycle = cycle;
        _scenarioId = id;

        OnPropertyChanged(nameof(Squad));
        OnPropertyChanged(nameof(Cycle));
        OnPropertyChanged(nameof(ScenarioId));

        ErrorMessage = "";
        RequestClose?.Invoke(true);
    }

    private static bool HasInvalidNameChars(string value) =>
        value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
}