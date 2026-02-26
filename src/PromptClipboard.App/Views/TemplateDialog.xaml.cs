namespace PromptClipboard.App.Views;

using PromptClipboard.App.ViewModels;
using System.Windows;

public partial class TemplateDialog : Window
{
    public TemplateDialog()
    {
        InitializeComponent();
    }

    public void Initialize(TemplateDialogViewModel vm)
    {
        DataContext = vm;
        vm.RequestClose += (confirmed) =>
        {
            DialogResult = confirmed;
            Close();
        };
    }
}
