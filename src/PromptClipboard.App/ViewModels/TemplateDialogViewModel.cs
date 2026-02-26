namespace PromptClipboard.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptClipboard.Application.Services;
using System.Collections.ObjectModel;

public partial class TemplateDialogViewModel : ObservableObject
{
    public ObservableCollection<TemplateFieldViewModel> Fields { get; } = [];

    public string? Result { get; private set; }
    public event Action<bool>? RequestClose;

    public void LoadVariables(IEnumerable<TemplateEngine.TemplateVariable> variables)
    {
        Fields.Clear();
        foreach (var v in variables)
        {
            Fields.Add(new TemplateFieldViewModel
            {
                Name = v.Name,
                Value = v.DefaultValue ?? string.Empty,
                Placeholder = v.DefaultValue ?? string.Empty
            });
        }
    }

    public Dictionary<string, string> GetValues()
    {
        var dict = new Dictionary<string, string>();
        foreach (var f in Fields)
            dict[f.Name] = f.Value;
        return dict;
    }

    [RelayCommand]
    private void Confirm()
    {
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}

public partial class TemplateFieldViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private string _placeholder = string.Empty;
}
