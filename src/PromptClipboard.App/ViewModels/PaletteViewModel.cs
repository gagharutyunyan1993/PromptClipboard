namespace PromptClipboard.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptClipboard.Application.Services;
using PromptClipboard.Domain.Entities;
using PromptClipboard.Domain.Interfaces;
using System.Collections.ObjectModel;

public partial class PaletteViewModel : ObservableObject
{
    private readonly SearchRankingService _searchService;
    private readonly IPromptRepository _repository;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private Prompt? _selectedPrompt;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private bool _isPasting;

    [ObservableProperty]
    private bool _hasTarget;

    public ObservableCollection<Prompt> Prompts { get; } = [];

    public event Action<Prompt>? PasteRequested;
    public event Action<Prompt>? PasteAsTextRequested;
    public event Action<Prompt>? EditRequested;
    public event Action? CreateRequested;
    public event Action? CloseRequested;
    public event Action<Prompt>? CopyRequested;
    public event Action<Prompt>? PinToggleRequested;
    public event Action<Prompt>? DeleteRequested;

    public PaletteViewModel(SearchRankingService searchService, IPromptRepository repository)
    {
        _searchService = searchService;
        _repository = repository;
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = DebounceSearchAsync(value);
    }

    private async Task DebounceSearchAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(150, token);
            await LoadPromptsAsync(query, token);
        }
        catch (OperationCanceledException) { }
    }

    public async Task LoadPromptsAsync(string? query = null, CancellationToken ct = default)
    {
        var results = await _searchService.SearchAsync(query ?? SearchText, ct);
        Prompts.Clear();
        foreach (var p in results)
            Prompts.Add(p);

        SelectedIndex = Prompts.Count > 0 ? 0 : -1;
        SelectedPrompt = Prompts.Count > 0 ? Prompts[0] : null;
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (Prompts.Count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? Prompts.Count - 1 : SelectedIndex - 1;
        SelectedPrompt = Prompts[SelectedIndex];
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (Prompts.Count == 0) return;
        SelectedIndex = SelectedIndex >= Prompts.Count - 1 ? 0 : SelectedIndex + 1;
        SelectedPrompt = Prompts[SelectedIndex];
    }

    [RelayCommand]
    private void Paste()
    {
        if (SelectedPrompt != null && !IsPasting)
            PasteRequested?.Invoke(SelectedPrompt);
    }

    [RelayCommand]
    private void PasteAsText()
    {
        if (SelectedPrompt != null && !IsPasting)
            PasteAsTextRequested?.Invoke(SelectedPrompt);
    }

    [RelayCommand]
    private void OpenEditor()
    {
        if (SelectedPrompt != null)
            EditRequested?.Invoke(SelectedPrompt);
    }

    [RelayCommand]
    private void Create()
    {
        CreateRequested?.Invoke();
    }

    [RelayCommand]
    private void HandleEscape()
    {
        if (!string.IsNullOrEmpty(SearchText))
            SearchText = string.Empty;
        else
            CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void CopyPrompt()
    {
        if (SelectedPrompt != null)
            CopyRequested?.Invoke(SelectedPrompt);
    }

    [RelayCommand]
    private void TogglePin(Prompt? prompt)
    {
        if (prompt != null)
            PinToggleRequested?.Invoke(prompt);
    }

    [RelayCommand]
    private void DeletePrompt(Prompt? prompt)
    {
        if (prompt != null)
            DeleteRequested?.Invoke(prompt);
    }

    public void RaiseEditRequested(Prompt prompt) => EditRequested?.Invoke(prompt);
    public void RaiseCopyRequested(Prompt prompt) => CopyRequested?.Invoke(prompt);
}
