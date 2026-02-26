namespace PromptClipboard.App.Views;

using PromptClipboard.App.ViewModels;
using System.Windows;

public partial class EditorWindow : Window
{
    public EditorWindow()
    {
        InitializeComponent();
    }

    public void Initialize(EditorViewModel vm)
    {
        DataContext = vm;
        vm.RequestClose += (saved) =>
        {
            DialogResult = saved;
            Close();
        };
    }
}
