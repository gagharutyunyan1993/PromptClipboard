namespace PromptClipboard.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptClipboard.Domain.Entities;
using PromptClipboard.Domain.Interfaces;

public partial class EditorViewModel : ObservableObject
{
    private readonly IPromptRepository _repository;
    private long _promptId;
    private bool _isNew;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _body = string.Empty;

    [ObservableProperty]
    private string _tagsInput = string.Empty;

    [ObservableProperty]
    private string _folder = string.Empty;

    [ObservableProperty]
    private string _lang = string.Empty;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private string _windowTitle = "Новый промпт";

    public event Action<bool>? RequestClose;

    public EditorViewModel(IPromptRepository repository)
    {
        _repository = repository;
    }

    public void LoadForCreate()
    {
        _isNew = true;
        _promptId = 0;
        WindowTitle = "Новый промпт";
        Title = string.Empty;
        Body = string.Empty;
        TagsInput = string.Empty;
        Folder = string.Empty;
        Lang = string.Empty;
        IsPinned = false;
    }

    public void LoadForEdit(Prompt prompt)
    {
        _isNew = false;
        _promptId = prompt.Id;
        WindowTitle = "Редактировать промпт";
        Title = prompt.Title;
        Body = prompt.Body;
        TagsInput = string.Join(", ", prompt.GetTags());
        Folder = prompt.Folder;
        Lang = prompt.Lang;
        IsPinned = prompt.IsPinned;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Body))
            return;

        var prompt = new Prompt
        {
            Id = _promptId,
            Title = Title.Trim(),
            Body = Body,
            Folder = Folder.Trim(),
            Lang = Lang.Trim(),
            IsPinned = IsPinned,
            UpdatedAt = DateTime.UtcNow
        };

        var tags = TagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        prompt.SetTags(tags);

        if (_isNew)
        {
            prompt.CreatedAt = DateTime.UtcNow;
            await _repository.CreateAsync(prompt);
        }
        else
        {
            await _repository.UpdateAsync(prompt);
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!_isNew && _promptId > 0)
        {
            await _repository.DeleteAsync(_promptId);
        }
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
