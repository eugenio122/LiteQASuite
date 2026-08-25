using System.Windows;

namespace LiteFlow.Views;

/// <summary>
/// O diálogo de iniciar cenário. Não tem lógica: quem valida e quem decide fechar
/// é o <c>StartScenarioViewModel</c>, que pede o fechamento pelo evento
/// <c>RequestClose</c> — quem assina esse evento é quem abriu o diálogo.
///
/// Fica assim para a decisão de "pode iniciar?" ser testável sem abrir janela.
/// </summary>
public partial class StartScenarioDialog : Window
{
    public StartScenarioDialog()
    {
        InitializeComponent();
    }
}