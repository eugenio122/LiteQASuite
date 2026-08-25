using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using LiteFlow.Export;
using LiteFlow.Models;
using LiteFlow.Platform;
using LiteFlow.Scenario;
using LiteFlow.Storage;
using LiteFlow.Views;
using LiteQASuite.Core.Events;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Modules;
using LiteQASuite.Core.Mvvm;
using LiteQASuite.Core.Notifications;
using Microsoft.Win32;

namespace LiteFlow.ViewModels;

/// <summary>
/// A tela do LiteFlow: o Workspace à direita, o histórico de evidências à
/// esquerda, a evidência selecionada no meio.
///
/// <b>Este ViewModel nasce com o módulo, não com a tela.</b> É a diferença
/// deliberada em relação ao LiteShot, cuja View é preguiçosa: lá a tela é só
/// configuração, aqui ela é o destino das capturas. Um print que chega enquanto o
/// usuário está noutro módulo precisa entrar no cenário do mesmo jeito — e para
/// isso alguém tem que estar de pé segurando a sessão.
///
/// <b>Chegou captura sem cenário aberto (ou com a gravação pausada):</b> a
/// evidência não entra, e o usuário é avisado por toast. Descartar em silêncio
/// seria a pior combinação possível — a pessoa continua capturando meia hora
/// achando que está tudo sendo guardado.
/// </summary>
public sealed class LiteFlowViewModel : ViewModelBase
{
    private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromSeconds(3);

    private readonly ModuleContext _context;
    private readonly IModuleStrings _strings;
    private readonly SettingsStore _settingsStore;
    private readonly LiteFlowSettings _settings;
    private readonly TemplateStore _templates = new();
    private readonly EvidenceCache _cache;
    private readonly AutoSaveScheduler _autoSave;

    private ScenarioSession? _session;
    private EvidenceStepViewModel? _selectedStep;
    private BitmapSource? _currentImage;
    private string _statusMessage = "";
    private bool _isRecording = true;
    private bool _isExporting;
    private bool _exportWord = true;
    private bool _exportPdf = true;
    private bool _isShutdown;

    public LiteFlowViewModel(ModuleContext context, SettingsStore settingsStore, LiteFlowSettings settings)
    {
        _context = context;
        _strings = context.Language.ForModule(LiteFlowModule.ModuleId);
        _settingsStore = settingsStore;
        _settings = settings;

        _cache = new EvidenceCache();
        _autoSave = new AutoSaveScheduler(SaveSnapshotAsync, AutoSaveDelay);

        StartScenarioCommand = new RelayCommand(StartScenario);
        OpenNodeCommand = new RelayCommand(
            parameter => OpenNode(parameter as WorkspaceNode),
            parameter => parameter is WorkspaceNode { IsScenario: true } or WorkspaceNode { IsReport: true });
        RevealNodeCommand = new RelayCommand(
            parameter => RevealNode(parameter as WorkspaceNode),
            parameter => parameter is WorkspaceNode);
        DeleteScenarioCommand = new RelayCommand(
            parameter => DeleteScenario(parameter as WorkspaceNode),
            parameter => parameter is WorkspaceNode { IsScenario: true });
        RestartScenarioCommand = new RelayCommand(RestartScenario, () => _session is not null && Steps.Count > 0);
        RefreshWorkspaceCommand = new RelayCommand(RefreshWorkspace);
        SaveCommand = new RelayCommand(() => _ = SaveManualAsync(), () => _session is not null);
        ToggleRecordingCommand = new RelayCommand(() => IsRecording = !IsRecording, () => _session is not null);
        DeleteStepCommand = new RelayCommand(
            parameter => DeleteStep(parameter as EvidenceStepViewModel ?? SelectedStep),
            _ => _session is not null);
        ImportTemplateCommand = new RelayCommand(ImportTemplate, () => _session is not null);
        RemoveTemplateCommand = new RelayCommand(RemoveTemplate, () => _session is not null && HasTemplate);
        ExportCommand = new RelayCommand(
            () => _ = ExportAsync(),
            () => _session is not null && Steps.Count > 0 && !IsExporting);

        RefreshWorkspace();
        PublishSessionState();
    }

    // ---------------------------------------------------------------- coleções

    /// <summary>As evidências do cenário aberto, na ordem do relatório.</summary>
    public ObservableCollection<EvidenceStepViewModel> Steps { get; } = new();

    /// <summary>A árvore squad → ciclo → cenário da aba Workspace.</summary>
    public ObservableCollection<WorkspaceNode> WorkspaceTree { get; } = new();

    /// <summary>Opções do combo de colunas do layout Mobile.</summary>
    public IReadOnlyList<int> MobileColumnOptions { get; } = new[] { 1, 2, 3 };

    // ---------------------------------------------------------------- comandos

    public ICommand StartScenarioCommand { get; }
    public ICommand OpenNodeCommand { get; }
    public ICommand RevealNodeCommand { get; }
    public ICommand DeleteScenarioCommand { get; }
    public ICommand RestartScenarioCommand { get; }
    public ICommand RefreshWorkspaceCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ToggleRecordingCommand { get; }
    public ICommand DeleteStepCommand { get; }
    public ICommand ImportTemplateCommand { get; }
    public ICommand RemoveTemplateCommand { get; }
    public ICommand ExportCommand { get; }

    // ------------------------------------------------------------------ estado

    /// <summary><c>true</c> quando há um cenário aberto. Governa a habilitação da tela inteira.</summary>
    public bool HasScenario => _session is not null;

    /// <summary>
    /// A gravação está ligada. Pausar mantém o cenário aberto e a lente do LiteShot
    /// funcionando — o print continua indo para a área de transferência — mas nada
    /// entra no cenário. É o caminho para tirar um print avulso sem sujar o
    /// relatório.
    /// </summary>
    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            if (!SetProperty(ref _isRecording, value)) return;
            OnPropertyChanged(nameof(RecordingLabel));
            PublishSessionState();
        }
    }

    /// <summary>A evidência aberta no canvas.</summary>
    public EvidenceStepViewModel? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (!SetProperty(ref _selectedStep, value)) return;

            CurrentImage = value is null ? null : EvidenceCache.LoadFull(value.Step.CachePath);
            RaiseSelectedStepProperties();
        }
    }

    /// <summary>A imagem em tamanho real da evidência selecionada.</summary>
    public BitmapSource? CurrentImage
    {
        get => _currentImage;
        private set => SetProperty(ref _currentImage, value);
    }

    /// <summary>Mensagem inline do rodapé (salvou, exportou, falhou ao abrir…).</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Exportação em curso: desabilita o botão e conta ao usuário.</summary>
    public bool IsExporting
    {
        get => _isExporting;
        private set => SetProperty(ref _isExporting, value);
    }

    /// <summary>O que aparece na barra superior: "squad / ciclo / ID", ou o aviso de que não há cenário.</summary>
    public string ScenarioLabel => _session is null
        ? _strings.GetString("Toolbar.NoScenario")
        : $"{_session.Squad}  /  {_session.Cycle}  /  {_session.ScenarioId}";

    /// <summary>Rótulo do botão de pausar, que conta o estado atual.</summary>
    public string RecordingLabel => _strings.GetString(IsRecording ? "Toolbar.Recording" : "Toolbar.Paused");

    // ------------------------------------------------ evidência selecionada

    /// <summary>Habilita o bloco de nota da evidência.</summary>
    public bool HasSelectedStep => _selectedStep is not null;

    /// <summary>
    /// O texto que acompanha a evidência no relatório. É o que transforma uma pilha
    /// de prints num documento que alguém consegue ler.
    /// </summary>
    public string SelectedStepNote
    {
        get => _selectedStep?.Step.Note ?? "";
        set
        {
            if (_selectedStep is null || _selectedStep.Step.Note == value) return;

            _selectedStep.Step.Note = value ?? "";
            OnPropertyChanged();
            MarkDirty();
        }
    }

    /// <summary><c>true</c> põe a nota depois da imagem, e não antes.</summary>
    public bool SelectedStepTextBelowImage
    {
        get => _selectedStep?.Step.TextBelowImage ?? false;
        set
        {
            if (_selectedStep is null || _selectedStep.Step.TextBelowImage == value) return;

            _selectedStep.Step.TextBelowImage = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    /// <summary>
    /// Evidência que ilustra mas não é um passo executado: sai da numeração da
    /// linha do tempo. Continua no relatório normalmente — o que muda é a contagem.
    /// </summary>
    public bool SelectedStepEvidenceOnly
    {
        get => _selectedStep?.Step.IsEvidenceOnly ?? false;
        set
        {
            if (_selectedStep is null || _selectedStep.Step.IsEvidenceOnly == value) return;

            _selectedStep.Step.IsEvidenceOnly = value;
            _selectedStep.RefreshEvidenceOnly();
            OnPropertyChanged();
            Reindex();
            MarkDirty();
        }
    }

    // -------------------------------------------------- metadados do relatório

    public string ScenarioId => _session?.ScenarioId ?? "";

    public string CycleName => _session?.Cycle ?? "";

    public string SquadName => _session?.Squad ?? "";

    public string FilePrefix
    {
        get => _session?.Document.FilePrefix ?? "";
        set => SetDocumentValue(value, document => document.FilePrefix, (document, v) => document.FilePrefix = v);
    }

    public string TestCaseName
    {
        get => _session?.Document.TestCaseName ?? "";
        set => SetDocumentValue(value, document => document.TestCaseName, (document, v) => document.TestCaseName = v);
    }

    public string Executor
    {
        get => _session?.Document.QAName ?? "";
        set => SetDocumentValue(value, document => document.QAName, (document, v) => document.QAName = v);
    }

    public string TestDate
    {
        get => _session?.Document.TestDate ?? "";
        set => SetDocumentValue(value, document => document.TestDate, (document, v) => document.TestDate = v);
    }

    public string Comments
    {
        get => _session?.Document.Comments ?? "";
        set => SetDocumentValue(value, document => document.Comments, (document, v) => document.Comments = v);
    }

    /// <summary>0 = Padrão, 1 = Mobile.</summary>
    public int LayoutIndex
    {
        get => (int)(_session?.Document.ReportLayout ?? ReportLayout.Padrao);
        set
        {
            if (_session is null) return;

            var layout = value == (int)ReportLayout.Mobile ? ReportLayout.Mobile : ReportLayout.Padrao;
            if (_session.Document.ReportLayout == layout) return;

            _session.Document.ReportLayout = layout;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMobileLayout));
            MarkDirty();
        }
    }

    public bool IsMobileLayout => _session?.Document.ReportLayout == ReportLayout.Mobile;

    public int MobileColumns
    {
        get => _session?.Document.MobileColumns ?? 2;
        set
        {
            if (_session is null || _session.Document.MobileColumns == value) return;

            _session.Document.MobileColumns = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    // ----------------------------------------------------------------- template

    /// <summary><c>true</c> quando o cenário tem um template escolhido.</summary>
    public bool HasTemplate => !string.IsNullOrWhiteSpace(_session?.Document.TemplatePath);

    /// <summary>Nome do arquivo de template, ou o texto de "documento em branco".</summary>
    public string TemplateName =>
        TemplateStore.DisplayName(_session?.Document.TemplatePath) ?? _strings.GetString("Template.None");

    /// <summary>
    /// Marcar isto guarda o template do cenário atual como o padrão dos próximos.
    /// Reflete o estado real: fica marcado quando o template do cenário já é o
    /// padrão gravado.
    /// </summary>
    public bool UseTemplateAsDefault
    {
        get => HasTemplate
               && string.Equals(_session!.Document.TemplatePath, _settings.DefaultTemplatePath, StringComparison.OrdinalIgnoreCase);
        set
        {
            if (_session is null) return;

            _settings.DefaultTemplatePath = value ? _session.Document.TemplatePath : "";
            _settingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    // ---------------------------------------------------------------- exportação

    /// <summary>Gerar o <c>.docx</c>.</summary>
    public bool ExportWord
    {
        get => _exportWord;
        set => SetProperty(ref _exportWord, value);
    }

    /// <summary>Gerar o <c>.pdf</c> (exige Word ou LibreOffice instalado).</summary>
    public bool ExportPdf
    {
        get => _exportPdf;
        set => SetProperty(ref _exportPdf, value);
    }

    // ------------------------------------------------------------------ textos

    public string LabelSave => _strings.GetString("Toolbar.Save");
    public string LabelNextSlice => _strings.GetString("Toolbar.NextSlice");
    public string LabelPropertiesTab => _strings.GetString("Tab.Properties");
    public string LabelWorkspaceTab => _strings.GetString("Tab.Workspace");
    public string LabelStartScenario => _strings.GetString("Workspace.Start");
    public string LabelRefresh => _strings.GetString("Workspace.Refresh");
    public string LabelWorkspaceEmpty => _strings.GetString("Workspace.Empty");
    public string LabelOpen => _strings.GetString("Workspace.Open");
    public string LabelReveal => _strings.GetString("Workspace.Reveal");
    public string LabelDeleteScenario => _strings.GetString("Workspace.DeleteScenario");
    public string LabelRestart => _strings.GetString("Toolbar.Restart");
    public string LabelStepsTitle => _strings.GetString("Steps.Title");
    public string LabelStepsEmpty => _strings.GetString("Steps.Empty");
    public string LabelDeleteStep => _strings.GetString("Steps.Delete");
    public string LabelEvidenceOnlyBadge => _strings.GetString("Steps.EvidenceOnlyBadge");
    public string LabelEvidenceTitle => _strings.GetString("Evidence.Title");
    public string LabelEvidenceNote => _strings.GetString("Evidence.Note");
    public string LabelTextBelowImage => _strings.GetString("Evidence.TextBelow");
    public string LabelEvidenceOnly => _strings.GetString("Evidence.Only");
    public string LabelPropertiesTitle => _strings.GetString("Properties.Title");
    public string LabelSquad => _strings.GetString("Properties.Squad");
    public string LabelCycle => _strings.GetString("Properties.Cycle");
    public string LabelScenarioId => _strings.GetString("Properties.Id");
    public string LabelPrefix => _strings.GetString("Scenario.Prefix");
    public string LabelTestCase => _strings.GetString("Scenario.TestCase");
    public string LabelExecutor => _strings.GetString("Scenario.Executor");
    public string LabelDate => _strings.GetString("Scenario.Date");
    public string LabelComments => _strings.GetString("Scenario.Comments");
    public string LabelLayout => _strings.GetString("Scenario.Layout");
    public string LabelLayoutPadrao => _strings.GetString("Scenario.Layout.Padrao");
    public string LabelLayoutMobile => _strings.GetString("Scenario.Layout.Mobile");
    public string LabelMobileColumns => _strings.GetString("Scenario.MobileColumns");
    public string LabelTemplateTitle => _strings.GetString("Template.Title");
    public string LabelTemplateImport => _strings.GetString("Template.Import");
    public string LabelTemplateRemove => _strings.GetString("Template.Remove");
    public string LabelTemplateAsDefault => _strings.GetString("Template.AsDefault");
    public string LabelExportTitle => _strings.GetString("Export.Title");
    public string LabelExportHint => _strings.GetString("Export.Hint");
    public string LabelExportRun => _strings.GetString("Export.Run");

    // ------------------------------------------------------------- captura

    /// <summary>
    /// Chegou uma captura confirmada. <b>Vem de uma thread de fundo</b> — o
    /// contrato do <c>IEventBus</c> diz que o barramento invoca na thread de quem
    /// publicou e que marshalar é responsabilidade de quem assina.
    /// </summary>
    public void OnCaptureCompleted(CaptureCompletedEvent captureEvent)
    {
        if (_isShutdown) return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        if (dispatcher.CheckAccess()) AddEvidence(captureEvent);
        else dispatcher.BeginInvoke(new Action(() => AddEvidence(captureEvent)));
    }

    private void AddEvidence(CaptureCompletedEvent captureEvent)
    {
        if (_isShutdown) return;

        if (_session is null)
        {
            _context.Notifications.Show(_strings.GetString("Toast.NoScenario"), NotificationKind.Warning);
            return;
        }

        if (!IsRecording)
        {
            _context.Notifications.Show(_strings.GetString("Toast.Paused"), NotificationKind.Warning);
            return;
        }

        // A "tela limpa" do evento não é assunto nosso: é insumo de análise do
        // LiteJson, que assina o mesmo evento por conta própria. Guardá-la aqui
        // dobraria o tamanho do .lflow para nada.
        var step = new EvidenceStep { StepId = captureEvent.StepId };

        try
        {
            step.CachePath = _cache.Store(step.StepId, captureEvent.Image);
        }
        catch (Exception ex)
        {
            // Disco cheio, pasta temporária sem permissão: a captura se perde, mas
            // o cenário que já está aberto não pode cair junto.
            StatusMessage = _strings.GetString("Status.CaptureFailed", ex.Message);
            _context.Notifications.Show(StatusMessage, NotificationKind.Error);
            return;
        }

        _session.Document.Steps.Add(step);

        var stepViewModel = new EvidenceStepViewModel(step, EvidenceCache.LoadThumbnail(step.CachePath));
        Steps.Add(stepViewModel);

        Reindex();
        SelectedStep = stepViewModel;
        MarkDirty();
    }

    /// <summary>
    /// Remove uma evidência da linha do tempo. Pede confirmação porque não há
    /// desfazer de projeto nesta versão — o print sai e não volta.
    /// </summary>
    private void DeleteStep(EvidenceStepViewModel? step)
    {
        if (_session is null || step is null) return;

        var answer = MessageBox.Show(
            _strings.GetString("Steps.DeleteConfirm", step.DisplayIndex),
            _strings.GetString("Steps.DeleteTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        var index = Steps.IndexOf(step);

        Steps.Remove(step);
        _session.Document.Steps.Remove(step.Step);

        // Quando o LiteJson existir, é aqui que sai o aviso de passo excluído — o
        // DTO ainda não está no Core (ver o doc de decisões, seção 7).

        Reindex();
        SelectedStep = Steps.Count == 0 ? null : Steps[Math.Min(index, Steps.Count - 1)];
        MarkDirty();
    }

    // ------------------------------------------------------------- workspace

    private void RefreshWorkspace()
    {
        WorkspaceTree.Clear();

        var workspace = _context.Workspace;
        if (!workspace.IsConfigured) return;

        foreach (var squad in workspace.GetSquads())
        {
            var squadNode = new WorkspaceNode(
                WorkspaceNodeKind.Squad, squad, workspace.EnsureSquad(squad), squad);

            foreach (var cycle in workspace.GetCycles(squad))
            {
                var cycleNode = new WorkspaceNode(
                    WorkspaceNodeKind.Cycle, cycle, workspace.EnsureCycle(squad, cycle), squad, cycle);

                foreach (var scenarioId in workspace.GetScenarios(squad, cycle))
                {
                    // Só entra na árvore quem tem .lflow. A pasta pode existir com só
                    // o .json do LiteJson ou um relatório exportado dentro.
                    var lflowPath = workspace.GetScenarioFilePath(squad, cycle, scenarioId, "lflow");
                    if (!File.Exists(lflowPath)) continue;

                    var folder = Path.GetDirectoryName(lflowPath)!;

                    // Só o cabeçalho do arquivo é lido aqui — ver ScenarioStore.ReadSummary.
                    var summary = ScenarioStore.ReadSummary(lflowPath);

                    var scenarioNode = new WorkspaceNode(
                        WorkspaceNodeKind.Scenario,
                        BuildScenarioLabel(scenarioId, summary.TestCaseName),
                        folder, squad, cycle, scenarioId);

                    foreach (var report in ListReports(folder))
                        scenarioNode.Children.Add(report);

                    cycleNode.Children.Add(scenarioNode);
                }

                squadNode.Children.Add(cycleNode);
            }

            WorkspaceTree.Add(squadNode);
        }
    }

    /// <summary>
    /// Os relatórios exportados de um cenário. O <c>.json</c> do LiteJson não entra:
    /// é artefato de outro módulo, e abrir JSON cru não é coisa que o LiteFlow faça.
    /// </summary>
    private static IReadOnlyList<WorkspaceNode> ListReports(string scenarioFolder)
    {
        List<string> files;

        try
        {
            files = Directory.EnumerateFiles(scenarioFolder)
                .Where(path => path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                            || path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.CurrentCulture)
                .ToList();
        }
        catch (IOException)
        {
            return Array.Empty<WorkspaceNode>();
        }

        return files
            .Select(path => new WorkspaceNode(
                path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? WorkspaceNodeKind.PdfReport
                    : WorkspaceNodeKind.WordReport,
                Path.GetFileName(path),
                path))
            .ToList();
    }

    /// <summary>
    /// "PED-1042 — Login com senha expirada". O caso de teste é multilinha e pode
    /// ser longo, então vira uma linha só e é cortado — a árvore é um índice, não
    /// o lugar de ler o cenário.
    /// </summary>
    private static string BuildScenarioLabel(string scenarioId, string testCaseName)
    {
        if (string.IsNullOrWhiteSpace(testCaseName)) return scenarioId;

        var flattened = string.Join(' ', testCaseName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (flattened.Length > 60) flattened = flattened[..57] + "…";

        return $"{scenarioId} — {flattened}";
    }

    /// <summary>
    /// O duplo clique e o "Abrir" do menu: cenário abre no editor, relatório abre
    /// no programa padrão do Windows. Squad e ciclo não abrem nada — o duplo clique
    /// neles já expande o nó.
    /// </summary>
    private void OpenNode(WorkspaceNode? node)
    {
        if (node is null) return;

        if (node.IsScenario) OpenScenario(node);
        else if (node.IsReport) OpenExternal(node.FullPath);
    }

    private void OpenExternal(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = _strings.GetString("Status.OpenExternalFailed", ex.Message);
        }
    }

    /// <summary>
    /// Abre o Explorer: na pasta, quando o nó é pasta; com o arquivo já
    /// selecionado, quando é relatório.
    /// </summary>
    private void RevealNode(WorkspaceNode? node)
    {
        if (node is null) return;

        try
        {
            if (node.IsReport && File.Exists(node.FullPath))
                Process.Start("explorer.exe", $"/select,\"{node.FullPath}\"");
            else if (Directory.Exists(node.FullPath))
                Process.Start(new ProcessStartInfo(node.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = _strings.GetString("Status.OpenExternalFailed", ex.Message);
        }
    }

    /// <summary>
    /// Manda a pasta do cenário inteira para a Lixeira — o <c>.lflow</c>, o
    /// <c>.json</c> do LiteJson e os relatórios exportados vão juntos, porque é
    /// isso que "excluir o cenário" quer dizer.
    /// </summary>
    private void DeleteScenario(WorkspaceNode? node)
    {
        if (node is null || !node.IsScenario) return;

        var answer = MessageBox.Show(
            _strings.GetString("Workspace.DeleteConfirm", node.DisplayName),
            _strings.GetString("Workspace.DeleteTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        // Se o cenário excluído é o que está aberto, fecha antes: um autosave
        // pendente recriaria o .lflow logo depois de a pasta ir para a lixeira.
        if (_session is not null &&
            string.Equals(_session.FolderPath, node.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            CloseSession();
        }

        if (RecycleBin.TryDeleteFolder(node.FullPath, out var errorCode, OwnerHandle()))
        {
            StatusMessage = _strings.GetString("Status.ScenarioDeleted", node.DisplayName);
        }
        else
        {
            // A sessão já foi fechada acima, mas a pasta continua no disco — o
            // cenário reaparece na árvore e pode ser reaberto depois de resolver o
            // motivo (quase sempre um arquivo da pasta aberto noutro programa).
            StatusMessage = _strings.GetString("Status.DeleteFailed", node.DisplayName, errorCode);
            _context.Notifications.Show(StatusMessage, NotificationKind.Error);
        }

        RefreshWorkspace();
    }

    /// <summary>
    /// A janela da casca, para o diálogo de erro do Windows nascer modal a ela. Sem
    /// dono, ele pode aparecer atrás do aplicativo e parecer que nada aconteceu.
    /// </summary>
    private static IntPtr OwnerHandle()
    {
        var window = Application.Current?.MainWindow;
        return window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
    }

    /// <summary>
    /// Recomeçar: apaga todas as evidências e <b>mantém os dados do teste</b>. É o
    /// caso de reexecutar o mesmo cenário do zero — o caso de teste, o executor e o
    /// layout continuam valendo, só os prints é que não.
    /// </summary>
    private void RestartScenario()
    {
        if (_session is null || Steps.Count == 0) return;

        var answer = MessageBox.Show(
            _strings.GetString("Steps.RestartConfirm", Steps.Count),
            _strings.GetString("Steps.RestartTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        _session.Document.Steps.Clear();
        Steps.Clear();
        SelectedStep = null;
        _cache.Clear();

        MarkDirty();
        StatusMessage = _strings.GetString("Status.Restarted");
    }

    /// <summary>Fecha o cenário aberto sem abrir outro.</summary>
    private void CloseSession()
    {
        _autoSave.Reset();
        _session = null;

        Steps.Clear();
        SelectedStep = null;
        _cache.Clear();

        RaiseScenarioProperties();
        PublishSessionState();
    }

    private void StartScenario()
    {
        var dialogViewModel = new StartScenarioViewModel(_strings, _context.Workspace, _settings);
        var dialog = new StartScenarioDialog
        {
            DataContext = dialogViewModel,
            Owner = Application.Current?.MainWindow
        };

        dialogViewModel.RequestClose += confirmed =>
        {
            dialog.DialogResult = confirmed;
            dialog.Close();
        };

        if (dialog.ShowDialog() != true) return;

        var squad = dialogViewModel.Squad;
        var cycle = dialogViewModel.Cycle;
        var id = dialogViewModel.ScenarioId;

        // Sai do cenário atual com o que estava pendente já no disco — o cache é
        // esvaziado logo abaixo, e um salvamento pendente perderia os PNGs.
        SaveNow();

        try
        {
            var folder = _context.Workspace.EnsureScenarioFolder(squad, cycle, id);
            var filePath = _context.Workspace.GetScenarioFilePath(squad, cycle, id, "lflow");

            var document = new ScenarioDocument
            {
                ScenarioId = id,
                FilePrefix = dialogViewModel.Prefix.Trim(),
                TestCaseName = dialogViewModel.TestCase.Trim(),
                QAName = dialogViewModel.Executor.Trim(),
                TestDate = dialogViewModel.Date.Trim(),
                Comments = dialogViewModel.Comments,
                ReportLayout = dialogViewModel.Layout,
                MobileColumns = dialogViewModel.MobileColumns,
                TemplatePath = File.Exists(_settings.DefaultTemplatePath) ? _settings.DefaultTemplatePath : ""
            };

            // O .lflow nasce antes do evento: quem ouvir o ScenarioStartedEvent tem
            // que encontrar a pasta com o cenário já lá dentro, e não uma pasta que
            // talvez venha a ter algo.
            ScenarioStore.Save(filePath, document);

            _cache.Clear();
            AdoptSession(new ScenarioSession(squad, cycle, id, folder, filePath, document));

            RememberDefaults(dialogViewModel);
            RefreshWorkspace();

            _context.Events.Publish(new ScenarioStartedEvent(id, squad, cycle, folder, DateTime.Now));

            StatusMessage = _strings.GetString("Status.ScenarioStarted", id);
        }
        catch (Exception ex)
        {
            StatusMessage = _strings.GetString("Status.StartFailed", ex.Message);
            _context.Notifications.Show(StatusMessage, NotificationKind.Error);
        }
    }

    private void OpenScenario(WorkspaceNode? node)
    {
        if (node is null || !node.IsScenario) return;
        if (node.Squad is null || node.Cycle is null || node.ScenarioId is null) return;

        var filePath = _context.Workspace.GetScenarioFilePath(node.Squad, node.Cycle, node.ScenarioId, "lflow");
        if (!File.Exists(filePath))
        {
            StatusMessage = _strings.GetString("Status.OpenFailed", filePath);
            return;
        }

        // Sai do cenário atual com o que estava pendente já no disco.
        SaveNow();

        try
        {
            _cache.Clear();

            var document = ScenarioStore.Load(filePath, (stepId, png) => _cache.Store(stepId, png));

            AdoptSession(new ScenarioSession(
                node.Squad, node.Cycle, node.ScenarioId, node.FullPath, filePath, document));

            StatusMessage = _strings.GetString("Status.ScenarioOpened", node.ScenarioId);
        }
        catch (Exception ex)
        {
            StatusMessage = _strings.GetString("Status.OpenFailed", ex.Message);
            _context.Notifications.Show(StatusMessage, NotificationKind.Error);
        }
    }

    /// <summary>Assume um cenário como o cenário aberto e reconstrói a tela em volta dele.</summary>
    private void AdoptSession(ScenarioSession session)
    {
        _autoSave.Reset();
        _session = session;

        Steps.Clear();
        foreach (var step in session.Document.Steps)
            Steps.Add(new EvidenceStepViewModel(step, EvidenceCache.LoadThumbnail(step.CachePath)));

        Reindex();
        SelectedStep = Steps.Count > 0 ? Steps[0] : null;

        IsRecording = true;
        RaiseScenarioProperties();
        PublishSessionState();
    }

    // ------------------------------------------------------------------ template

    private void ImportTemplate()
    {
        if (_session is null) return;

        var dialog = new OpenFileDialog
        {
            Title = _strings.GetString("Template.Import"),
            Filter = "Word (*.docx;*.dotx)|*.docx;*.dotx"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var stored = _templates.Import(dialog.FileName);

            _session.Document.TemplatePath = stored;

            // Se o padrão apontava para este mesmo arquivo, ele continua apontando
            // para a cópia nova — reimportar um template corrigido não deve exigir
            // remarcar o checkbox.
            if (string.Equals(TemplateStore.DisplayName(_settings.DefaultTemplatePath),
                              Path.GetFileName(stored), StringComparison.OrdinalIgnoreCase))
            {
                _settings.DefaultTemplatePath = stored;
                _settingsStore.Save(_settings);
            }

            RaiseTemplateProperties();
            MarkDirty();

            StatusMessage = _strings.GetString("Status.TemplateImported", Path.GetFileName(stored));
        }
        catch (Exception ex)
        {
            StatusMessage = _strings.GetString("Status.TemplateFailed", ex.Message);
            _context.Notifications.Show(StatusMessage, NotificationKind.Error);
        }
    }

    private void RemoveTemplate()
    {
        if (_session is null) return;

        _session.Document.TemplatePath = "";
        RaiseTemplateProperties();
        MarkDirty();
    }

    // ---------------------------------------------------------------- exportação

    /// <summary>
    /// Gera o relatório <b>na própria pasta do cenário</b>, ao lado do
    /// <c>.lflow</c>. Não há diálogo de pasta: o cenário já sabe onde mora, e
    /// espalhar o entregável longe da evidência é como se perde relatório.
    /// </summary>
    private async Task ExportAsync()
    {
        if (_session is null || Steps.Count == 0) return;

        if (!ExportWord && !ExportPdf)
        {
            StatusMessage = _strings.GetString("Export.PickFormat");
            return;
        }

        // O relatório sai do que está gravado; salvar antes evita exportar uma nota
        // que ainda estava só na tela.
        await _autoSave.FlushAsync();

        var items = Steps
            .Select(step => new ExportEvidence(step.Step.CachePath, step.Step.Note, step.Step.TextBelowImage))
            .ToList();

        var tags = ExportService.BuildTags(_session.Document);
        var template = ResolveTemplatePath();
        var layout = _session.Document.ReportLayout;
        var columns = _session.Document.MobileColumns;

        var folder = _session.FolderPath;
        var baseName = Path.Combine(folder, BuildReportFileName());
        var docxPath = baseName + ".docx";
        var pdfPath = baseName + ".pdf";

        var wantWord = ExportWord;
        var wantPdf = ExportPdf;

        IsExporting = true;
        StatusMessage = _strings.GetString("Status.Exporting");

        try
        {
            await Task.Run(() =>
            {
                if (wantWord && wantPdf)
                {
                    // Monta o documento uma vez só e converte o próprio arquivo —
                    // gerar o .docx duas vezes seria o dobro do trabalho pesado.
                    ExportService.ExportToWord(docxPath, template, tags, items, layout, columns);
                    ExportService.ConvertDocxToPdf(docxPath, pdfPath);
                }
                else if (wantWord)
                {
                    ExportService.ExportToWord(docxPath, template, tags, items, layout, columns);
                }
                else
                {
                    ExportService.ExportToPdf(pdfPath, template, tags, items, layout, columns);
                }
            });

            StatusMessage = _strings.GetString("Status.Exported", folder);
            _context.Notifications.Show(StatusMessage, NotificationKind.Success);

            // O relatório recém-gerado passa a aparecer sob o cenário na árvore.
            RefreshWorkspace();
        }
        catch (Exception ex)
        {
            StatusMessage = _strings.GetString("Status.ExportFailed", ex.Message);
            _context.Notifications.Show(StatusMessage, NotificationKind.Error);
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>
    /// O template do cenário; se ele sumiu do disco, o padrão; se nem esse existe,
    /// documento em branco. Falhar a exportação por causa de um template movido
    /// seria pior do que entregar o relatório sem cabeçalho.
    /// </summary>
    private string ResolveTemplatePath()
    {
        var scenarioTemplate = _session?.Document.TemplatePath ?? "";
        if (!string.IsNullOrWhiteSpace(scenarioTemplate) && File.Exists(scenarioTemplate)) return scenarioTemplate;

        var defaultTemplate = _settings.DefaultTemplatePath;
        return !string.IsNullOrWhiteSpace(defaultTemplate) && File.Exists(defaultTemplate) ? defaultTemplate : "";
    }

    /// <summary>
    /// "<c>PREFIXO ID</c>", sem extensão. O prefixo é do relatório, não da pasta —
    /// a pasta do cenário continua sendo só o ID.
    /// </summary>
    private string BuildReportFileName()
    {
        var prefix = (_session?.Document.FilePrefix ?? "").Trim();
        var id = _session?.ScenarioId ?? "Relatorio";
        var raw = string.IsNullOrEmpty(prefix) ? id : $"{prefix} {id}";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = raw.Trim().ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
        }

        var name = new string(chars).Trim();
        return string.IsNullOrEmpty(name) ? "Relatorio" : name;
    }

    // ------------------------------------------------------------ salvamento

    private void MarkDirty()
    {
        _autoSave.MarkDirty();
        StatusMessage = "";
    }

    private async Task SaveManualAsync()
    {
        if (_session is null) return;

        _autoSave.MarkDirty();
        await _autoSave.FlushAsync();

        if (!_autoSave.IsDirty) StatusMessage = _strings.GetString("Status.Saved");
    }

    /// <summary>
    /// O salvamento em si. Tira uma cópia do documento na thread de interface e só
    /// então escreve em segundo plano — sem isso o escritor percorreria a lista de
    /// passos enquanto o usuário continua capturando.
    /// </summary>
    private Task SaveSnapshotAsync()
    {
        if (_session is null) return Task.CompletedTask;

        var snapshot = _session.Document.Clone();
        var path = _session.FilePath;

        return Task.Run(() =>
        {
            try
            {
                ScenarioStore.Save(path, snapshot);
            }
            catch (Exception ex)
            {
                // O serviço de notificação marshala sozinho, então pode ser chamado daqui.
                _context.Notifications.Show(_strings.GetString("Status.SaveFailed", ex.Message), NotificationKind.Error);
                throw;
            }
        });
    }

    /// <summary>
    /// Grava agora, na thread chamadora. Usado no encerramento e na troca de
    /// cenário, onde não dá para esperar um <c>await</c> voltar.
    /// </summary>
    private void SaveNow()
    {
        if (_session is null || !_autoSave.IsDirty) return;

        try
        {
            ScenarioStore.Save(_session.FilePath, _session.Document.Clone());
            _autoSave.Reset();
        }
        catch (Exception)
        {
            // Best-effort: no encerramento não há mais tela para avisar.
        }
    }

    // ---------------------------------------------------------------- suporte

    /// <summary>
    /// Renumera as evidências. As marcadas como "só evidência" mostram travessão e
    /// não consomem número — é o que faz a contagem da linha do tempo bater com a
    /// contagem dos passos executados.
    /// </summary>
    private void Reindex()
    {
        var number = 1;

        foreach (var step in Steps)
        {
            if (step.Step.IsEvidenceOnly)
            {
                step.DisplayIndex = "—";
            }
            else
            {
                step.DisplayIndex = number.ToString();
                number++;
            }
        }
    }

    /// <summary>
    /// Publica no contexto de sessão o que o LiteShot precisa para dizer, no toast
    /// de confirmação, o que aconteceu com o print. É o único canal entre os dois
    /// módulos aqui — nenhum conhece o outro, e nenhum evento novo foi preciso.
    /// </summary>
    private void PublishSessionState()
    {
        _context.Session.Set(SessionKeys.Recording, _session is not null && IsRecording);
        _context.Session.Set(SessionKeys.ScenarioId, _session?.ScenarioId ?? "");
    }

    private void RememberDefaults(StartScenarioViewModel dialogViewModel)
    {
        _settings.LastSquad = dialogViewModel.Squad;
        _settings.LastCycle = dialogViewModel.Cycle;
        _settings.DefaultPrefix = dialogViewModel.Prefix.Trim();
        _settings.DefaultExecutor = dialogViewModel.Executor.Trim();
        _settings.DefaultLayout = dialogViewModel.Layout;
        _settings.DefaultMobileColumns = dialogViewModel.MobileColumns;

        _settingsStore.Save(_settings);
    }

    private void SetDocumentValue(
        string value,
        Func<ScenarioDocument, string> read,
        Action<ScenarioDocument, string> write,
        [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (_session is null) return;

        var document = _session.Document;
        if (read(document) == value) return;

        write(document, value ?? "");
        OnPropertyChanged(propertyName);
        MarkDirty();
    }

    private void RaiseSelectedStepProperties()
    {
        OnPropertyChanged(nameof(HasSelectedStep));
        OnPropertyChanged(nameof(SelectedStepNote));
        OnPropertyChanged(nameof(SelectedStepTextBelowImage));
        OnPropertyChanged(nameof(SelectedStepEvidenceOnly));
    }

    private void RaiseTemplateProperties()
    {
        OnPropertyChanged(nameof(HasTemplate));
        OnPropertyChanged(nameof(TemplateName));
        OnPropertyChanged(nameof(UseTemplateAsDefault));
    }

    private void RaiseScenarioProperties()
    {
        OnPropertyChanged(nameof(HasScenario));
        OnPropertyChanged(nameof(ScenarioLabel));
        OnPropertyChanged(nameof(ScenarioId));
        OnPropertyChanged(nameof(CycleName));
        OnPropertyChanged(nameof(SquadName));
        OnPropertyChanged(nameof(FilePrefix));
        OnPropertyChanged(nameof(TestCaseName));
        OnPropertyChanged(nameof(Executor));
        OnPropertyChanged(nameof(TestDate));
        OnPropertyChanged(nameof(Comments));
        OnPropertyChanged(nameof(LayoutIndex));
        OnPropertyChanged(nameof(IsMobileLayout));
        OnPropertyChanged(nameof(MobileColumns));

        RaiseTemplateProperties();
        RaiseSelectedStepProperties();
    }

    /// <summary>
    /// Encerramento: grava o que estiver pendente, para de aceitar capturas e
    /// devolve a pasta temporária. Seguro mesmo que a View nunca tenha sido criada.
    /// </summary>
    public void Shutdown()
    {
        if (_isShutdown) return;
        _isShutdown = true;

        SaveNow();

        _autoSave.Dispose();
        _cache.Dispose();

        _context.Session.Set(SessionKeys.Recording, false);
        _context.Session.Set(SessionKeys.ScenarioId, "");
    }
}