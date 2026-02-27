namespace PromptClipboard.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptClipboard.Application.Services;
using PromptClipboard.Domain.Entities;
using PromptClipboard.Domain.Interfaces;
using Serilog;
using System.Collections.ObjectModel;

public partial class PaletteViewModel : ObservableObject
{
    private readonly SearchRankingService _searchService;
    private readonly IPromptRepository _repository;
    private readonly ILogger _log;
    private readonly int _debounceMs;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private PromptItemViewModel? _selectedPrompt;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private bool _isPasting;

    [ObservableProperty]
    private bool _hasTarget;

    public ObservableCollection<PromptItemViewModel> Prompts { get; } = [];

    public event Action<Prompt>? PasteRequested;
    public event Action<Prompt>? PasteAsTextRequested;
    public event Action<Prompt>? EditRequested;
    public event Action? CreateRequested;
    public event Action? CloseRequested;
    public event Action<Prompt>? CopyRequested;
    public event Action<Prompt>? PinToggleRequested;
    public event Action<Prompt>? DeleteRequested;

    public PaletteViewModel(SearchRankingService searchService, IPromptRepository repository, ILogger log, int debounceMs = 150)
    {
        _searchService = searchService;
        _repository = repository;
        _log = log;
        _debounceMs = debounceMs;
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
            await Task.Delay(_debounceMs, token);
            await LoadPromptsAsync(query, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Error(ex, "Search failed for query '{Query}'", query);
            Prompts.Clear();
            SelectedIndex = -1;
            SelectedPrompt = null;
        }
    }

    public async Task LoadPromptsAsync(string? query = null, CancellationToken ct = default)
    {
        var results = await _searchService.SearchAsync(query ?? SearchText, ct);
        Prompts.Clear();
        foreach (var p in results)
            Prompts.Add(new PromptItemViewModel(p));

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
            PasteRequested?.Invoke(SelectedPrompt.Prompt);
    }

    [RelayCommand]
    private void PasteAsText()
    {
        if (SelectedPrompt != null && !IsPasting)
            PasteAsTextRequested?.Invoke(SelectedPrompt.Prompt);
    }

    [RelayCommand]
    private void OpenEditor()
    {
        if (SelectedPrompt != null)
            EditRequested?.Invoke(SelectedPrompt.Prompt);
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
            CopyRequested?.Invoke(SelectedPrompt.Prompt);
    }

    [RelayCommand]
    private void TogglePin(PromptItemViewModel? item)
    {
        if (item != null)
            PinToggleRequested?.Invoke(item.Prompt);
    }

    [RelayCommand]
    private void DeletePrompt(PromptItemViewModel? item)
    {
        if (item != null)
            DeleteRequested?.Invoke(item.Prompt);
    }

    public void RaiseEditRequested(Prompt prompt) => EditRequested?.Invoke(prompt);
    public void RaiseCopyRequested(Prompt prompt) => CopyRequested?.Invoke(prompt);
}
