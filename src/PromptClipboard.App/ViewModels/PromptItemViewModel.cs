namespace PromptClipboard.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptClipboard.App.Helpers;
using PromptClipboard.Domain.Entities;

public partial class PromptItemViewModel : ObservableObject
{
    public Prompt Prompt { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewText))]
    [NotifyPropertyChangedFor(nameof(MetaLabel))]
    [NotifyPropertyChangedFor(nameof(ToggleLabel))]
    private bool _isExpanded;

    public bool IsLongBody => BodyPreviewHelper.IsLongBody(Prompt.Body);

    public string PreviewText
    {
        get
        {
            if (!IsLongBody)
                return Prompt.Body;

            return IsExpanded
                ? BodyPreviewHelper.GetExpandedPreview(Prompt.Body)
                : BodyPreviewHelper.GetCollapsedPreview(Prompt.Body);
        }
    }

    public string MetaLabel => BodyPreviewHelper.GetMetaLabel(Prompt.Body);

    public string ToggleLabel => IsExpanded ? "Show less" : "Show more";

    public PromptItemViewModel(Prompt prompt)
    {
        Prompt = prompt;
    }

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
