namespace PromptClipboard.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptClipboard.Application.Services;
using PromptClipboard.Domain;
using PromptClipboard.Domain.Entities;
using PromptClipboard.Domain.Interfaces;
using PromptClipboard.Domain.Models;
using Serilog;
using System.Collections.ObjectModel;

public partial class PaletteViewModel : ObservableObject
{
    private readonly SearchRankingService _searchService;
    private readonly IPromptRepository _repository;
    private readonly ILogger _log;
    private readonly int _debounceMs;
    private CancellationTokenSource? _searchCts;

    // P0.3: search result metadata
    private bool _hasMoreResults;
    private bool _isTruncated;

    // P0.6: selection restore
    private string? _lastLoadedQueryFingerprint;

    // P0.4: total count for empty states
    private int _cachedTotalCount;

    // P0.4: transient load error
    private string? _transientLoadError;

    // P1.6: chip mutual exclusion and P2.5 RevealNewPrompt suppress guard
    private bool _suppressRequery;

    // P2 scaffold
    private long? _pendingNewPromptId;
    private PromptItemViewModel? _revealedPrompt;

    [ObservableProperty]
    private PaletteMode _mode = PaletteMode.List;

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

    // P1.5: Sort mode (visible only when no chip overrides sort)
    [ObservableProperty]
    private SortMode _currentSortMode = SortMode.Relevance;

    // P1.6: Quick filter chips
    [ObservableProperty]
    private bool _isFilterPinned;

    [ObservableProperty]
    private bool _isFilterRecent;

    [ObservableProperty]
    private bool _isFilterTemplates;

    [ObservableProperty]
    private string? _updateVersion;

    public ObservableCollection<PromptItemViewModel> Prompts { get; } = [];

    public int ResultCount => Prompts.Count;

    public string ResultCountText
    {
        get
        {
            if (Prompts.Count == 0)
                return _isTruncated ? "Query truncated to 20 tokens" : string.Empty;
            var text = _hasMoreResults
                ? $"Showing {SearchDefaults.MaxResults}+"
                : $"Showing {Prompts.Count}";
            if (_isTruncated) text += " (query truncated)";
            return text;
        }
    }

    public bool ShowEmptyDatabase => Prompts.Count == 0 && _cachedTotalCount == 0;
    public bool ShowNoResults => Prompts.Count == 0 && _cachedTotalCount > 0;
    public bool ShowNewPromptBanner => _pendingNewPromptId.HasValue;

    public string? TransientLoadError => _transientLoadError;
    public bool ShowLoadError => _transientLoadError != null;

    public PromptItemViewModel? RevealedPrompt
    {
        get => _revealedPrompt;
        private set
        {
            _revealedPrompt = value;
            OnPropertyChanged(nameof(RevealedPrompt));
            OnPropertyChanged(nameof(ShowRevealedPrompt));
        }
    }

    public bool ShowRevealedPrompt => RevealedPrompt != null;

    public QuickAddViewModel? QuickAdd { get; private set; }

    public event Action? UpdateRequested;

    public event Action<Prompt>? PasteRequested;
    public event Action<Prompt>? PasteAsTextRequested;
    public event Action<Prompt>? EditRequested;
    public event Action? CreateRequested;
    public event Action<string, string?, string?>? CreateWithTitleRequested;
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
        Prompts.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ResultCount));
            OnPropertyChanged(nameof(ResultCountText));
            OnPropertyChanged(nameof(ShowEmptyDatabase));
            OnPropertyChanged(nameof(ShowNoResults));
        };
    }

    public void InitializeQuickAdd(QuickAddViewModel quickAdd)
    {
        QuickAdd = quickAdd;
        QuickAdd.PromptCreated += OnPromptCreated;
        QuickAdd.Cancelled += () => Mode = PaletteMode.List;
        OnPropertyChanged(nameof(QuickAdd));
    }

    public void OnPaletteHidden()
    {
        // SearchText is NOT cleared (persist across show/hide)
        // Cleanup CTA banner
        _pendingNewPromptId = null;
        OnPropertyChanged(nameof(ShowNewPromptBanner));
        // Cleanup reveal-card transient state
        if (RevealedPrompt != null)
            RevealedPrompt = null;
    }

    public void SetTransientLoadError(string message)
    {
        _transientLoadError = message;
        OnPropertyChanged(nameof(TransientLoadError));
        OnPropertyChanged(nameof(ShowLoadError));
    }

    public async Task RefreshTotalCountAsync(CancellationToken ct = default)
    {
        _cachedTotalCount = await _repository.GetCountAsync(ct);
        OnPropertyChanged(nameof(ShowEmptyDatabase));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    partial void OnSearchTextChanged(string value)
    {
        // CTA cleanup: user started new query
        if (_pendingNewPromptId.HasValue)
        {
            _pendingNewPromptId = null;
            OnPropertyChanged(nameof(ShowNewPromptBanner));
        }
        if (_suppressRequery) return;
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
        // Clear any transient load error on successful start
        if (_transientLoadError != null)
        {
            _transientLoadError = null;
            OnPropertyChanged(nameof(TransientLoadError));
            OnPropertyChanged(nameof(ShowLoadError));
        }

        var searchQuery = BuildCurrentQuery();
        // If explicit query parameter is passed (e.g. from debounce), override text terms
        if (query != null)
        {
            var parsed = SearchQueryParser.Parse(query.Trim());
            searchQuery = searchQuery with
            {
                FreeTextTerms = parsed.FreeTextTerms,
                IncludeTags = parsed.IncludeTags.Count > 0 ? parsed.IncludeTags : searchQuery.IncludeTags,
                ExcludeTags = parsed.ExcludeTags.Count > 0 ? parsed.ExcludeTags : searchQuery.ExcludeTags,
                ExcludeWords = parsed.ExcludeWords.Count > 0 ? parsed.ExcludeWords : searchQuery.ExcludeWords,
                LangFilter = parsed.LangFilter ?? searchQuery.LangFilter,
                FolderFilter = parsed.FolderFilter ?? searchQuery.FolderFilter,
                IsTruncated = parsed.IsTruncated
            };
        }

        var compareKey = BuildSelectionCompareKey(searchQuery);
        var previousId = SelectedPrompt?.Prompt.Id;
        var queryChanged = compareKey != _lastLoadedQueryFingerprint;
        _lastLoadedQueryFingerprint = compareKey;

        var searchResult = await _searchService.SearchAsync(searchQuery, ct);
        _hasMoreResults = searchResult.HasMore;
        _isTruncated = searchResult.IsTruncated;

        Prompts.Clear();
        foreach (var p in searchResult.Items)
            Prompts.Add(new PromptItemViewModel(p));

        // Clear reveal-card on successful load
        if (RevealedPrompt != null)
            RevealedPrompt = null;

        // Restore selection by Id if query unchanged, otherwise select first
        PromptItemViewModel? restored = null;
        if (!queryChanged && previousId.HasValue)
            restored = Prompts.FirstOrDefault(p => p.Prompt.Id == previousId.Value);

        if (restored != null)
        {
            SelectedIndex = Prompts.IndexOf(restored);
            SelectedPrompt = restored;
        }
        else
        {
            SelectedIndex = Prompts.Count > 0 ? 0 : -1;
            SelectedPrompt = Prompts.Count > 0 ? Prompts[0] : null;
        }
    }

    private static string BuildSelectionCompareKey(SearchQuery q)
        => string.Join("|",
            $"t:{string.Join(",", q.FreeTextTerms)}",
            $"inc:{string.Join(",", q.IncludeTags)}",
            $"excTag:{string.Join(",", q.ExcludeTags)}",
            $"excWord:{string.Join(",", q.ExcludeWords)}",
            $"lang:{q.LangFilter ?? ""}",
            $"folder:{q.FolderFilter ?? ""}",
            $"pin:{q.PinnedFilter?.ToString() ?? ""}",
            $"tmpl:{q.HasTemplate?.ToString() ?? ""}",
            $"recent:{q.RecentLimit?.ToString() ?? ""}",
            $"sort:{q.Sort}");

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
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private async Task RetryLoad()
    {
        try
        {
            await RefreshTotalCountAsync();
            await LoadPromptsAsync();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "RetryLoad failed");
            SetTransientLoadError("Retry failed. Check database file/permissions and try again.");
        }
    }

    [RelayCommand]
    private void DismissError()
    {
        _transientLoadError = null;
        OnPropertyChanged(nameof(TransientLoadError));
        OnPropertyChanged(nameof(ShowLoadError));
    }

    [RelayCommand]
    private void RequestUpdate() => UpdateRequested?.Invoke();

    [RelayCommand]
    private void CreateWithTitle()
    {
        var parsed = SearchQueryParser.Parse(SearchText);
        var title = string.Join(" ", parsed.FreeTextTerms).Trim();
        var tag = parsed.IncludeTags.Count > 0 ? parsed.IncludeTags[0] : null;
        var lang = parsed.LangFilter;
        CreateWithTitleRequested?.Invoke(title, tag, lang);
    }

    [RelayCommand]
    private void HandleEscape()
    {
        if (Mode == PaletteMode.QuickAdd)
        {
            QuickAdd?.Cancel();
            Mode = PaletteMode.List;
            return;
        }
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

    // ── P1.6: Chip mutual exclusion ─────────────────────────────────────

    partial void OnIsFilterPinnedChanged(bool value)
    {
        if (_suppressRequery) return;
        _suppressRequery = true;
        try
        {
            if (value) IsFilterRecent = false;
        }
        finally { _suppressRequery = false; }
        _ = RequeryAsync();
    }

    partial void OnIsFilterRecentChanged(bool value)
    {
        if (_suppressRequery) return;
        _suppressRequery = true;
        try
        {
            if (value) IsFilterPinned = false;
        }
        finally { _suppressRequery = false; }
        _ = RequeryAsync();
    }

    partial void OnIsFilterTemplatesChanged(bool value)
    {
        if (_suppressRequery) return;
        _ = RequeryAsync();
    }

    partial void OnCurrentSortModeChanged(SortMode value)
    {
        if (_suppressRequery) return;
        _ = RequeryAsync();
    }

    private async Task RequeryAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;
        try { await LoadPromptsAsync(ct: ct); }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Error(ex, "RequeryAsync failed");
        }
    }

    // ── P2.5: Post-creation navigation ──────────────────────────────────

    private CancellationTokenSource? _focusCts;

    private void OnPromptCreated(long id)
    {
        Mode = PaletteMode.List;
        _ = FocusOnNewPromptAsync(id);
    }

    private async Task FocusOnNewPromptAsync(long id)
    {
        _focusCts?.Cancel();
        _focusCts = new CancellationTokenSource();
        var ct = _focusCts.Token;

        try
        {
            await RefreshTotalCountAsync(ct);
            await LoadPromptsAsync(ct: ct);
            ct.ThrowIfCancellationRequested();

            var target = Prompts.FirstOrDefault(p => p.Prompt.Id == id);
            if (target != null)
            {
                SelectedIndex = Prompts.IndexOf(target);
                SelectedPrompt = target;
                return;
            }

            _pendingNewPromptId = id;
            OnPropertyChanged(nameof(ShowNewPromptBanner));
            SelectedIndex = Prompts.Count > 0 ? 0 : -1;
            SelectedPrompt = Prompts.Count > 0 ? Prompts[0] : null;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Error(ex, "FocusOnNewPrompt failed for id={Id}", id);
        }
    }

    [RelayCommand]
    private async Task RevealNewPrompt()
    {
        if (_pendingNewPromptId is not { } id) return;
        _pendingNewPromptId = null;
        OnPropertyChanged(nameof(ShowNewPromptBanner));

        _suppressRequery = true;
        try
        {
            SearchText = string.Empty;
            IsFilterPinned = false;
            IsFilterRecent = false;
            IsFilterTemplates = false;
            CurrentSortMode = SortMode.Relevance;
        }
        finally { _suppressRequery = false; }

        await LoadPromptsAsync();
        var target = Prompts.FirstOrDefault(p => p.Prompt.Id == id);
        if (target != null)
        {
            SelectedIndex = Prompts.IndexOf(target);
            SelectedPrompt = target;
            return;
        }

        var created = await _repository.GetByIdAsync(id);
        if (created != null)
        {
            RevealedPrompt = new PromptItemViewModel(created);
            SelectedIndex = -1;
            SelectedPrompt = RevealedPrompt;
            return;
        }

        _log.Warning("RevealNewPrompt: id={Id} no longer exists", id);
        SelectedIndex = Prompts.Count > 0 ? 0 : -1;
        SelectedPrompt = Prompts.Count > 0 ? Prompts[0] : null;
    }

    [RelayCommand]
    private void EnterQuickAdd()
    {
        if (QuickAdd == null) return;
        var parsed = SearchQueryParser.Parse(SearchText);
        var title = string.Join(" ", parsed.FreeTextTerms).Trim();
        var tag = parsed.IncludeTags.Count > 0 ? parsed.IncludeTags[0] : null;
        var lang = parsed.LangFilter;
        QuickAdd.Show(title, tag, lang);
        Mode = PaletteMode.QuickAdd;
    }

    // ── P1: BuildCurrentQuery ───────────────────────────────────────────

    public SearchQuery BuildCurrentQuery()
    {
        var parsed = SearchQueryParser.Parse(SearchText);

        var pinned = IsFilterPinned;
        var recent = IsFilterRecent;
        if (pinned && recent)
        {
            _log.Warning("Invalid chip state detected (Pinned+Recent=true); forcing Recent=false");
            recent = false;
        }

        if (pinned)
            parsed = parsed with { PinnedFilter = true, RecentLimit = null, Sort = SortMode.PinnedFirst };
        else if (recent)
            parsed = parsed with { PinnedFilter = null, RecentLimit = SearchDefaults.MaxResults, Sort = SortMode.Recent };

        if (IsFilterTemplates)
            parsed = parsed with { HasTemplate = true };

        if (!pinned && !recent && CurrentSortMode != SortMode.Relevance)
            parsed = parsed with { Sort = CurrentSortMode };

        return parsed;
    }
}
