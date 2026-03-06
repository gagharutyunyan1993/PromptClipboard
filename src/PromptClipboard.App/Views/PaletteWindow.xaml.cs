namespace PromptClipboard.App.Views;

using PromptClipboard.App.Routing;
using PromptClipboard.App.ViewModels;
using PromptClipboard.Domain.Entities;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Serilog;

public partial class PaletteWindow : Window
{
    private bool _suppressDeactivate;
    private static readonly ILogger _log = Log.Logger;

    public PaletteViewModel ViewModel => (PaletteViewModel)DataContext;

    public PaletteWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public void SuppressDeactivate(bool suppress)
    {
        _log.Debug("SuppressDeactivate={Suppress}", suppress);
        _suppressDeactivate = suppress;
    }

    public void ShowAndFocus()
    {
        _log.Debug("ShowAndFocus called");
        try
        {
            Opacity = 0;
            Show();
            Activate();
            SearchBox.Focus();
            SearchBox.SelectAll();

            var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(100))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var translateAnim = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(100))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            var transform = new TranslateTransform();
            RenderTransform = transform;
            BeginAnimation(OpacityProperty, opacityAnim);
            transform.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "ShowAndFocus failed");
        }
    }

    public void HideWindow()
    {
        _log.Debug("HideWindow called, isVisible={Visible}", IsVisible);
        try
        {
            Hide();
            ViewModel.OnPaletteHidden();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "HideWindow failed");
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        _log.Debug("Window_Deactivated, suppress={Suppress}", _suppressDeactivate);
        if (_suppressDeactivate) return;
        HideWindow();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PaletteViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is PaletteViewModel newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PaletteViewModel.SelectedIndex) && ViewModel.SelectedPrompt != null)
        {
            PromptListBox.ScrollIntoView(ViewModel.SelectedPrompt);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var input = new KeyInput(
            e.Key,
            Keyboard.Modifiers,
            ViewModel.Mode == PaletteMode.QuickAdd,
            ViewModel.ShowRevealedPrompt);

        var result = PaletteKeyboardRouter.Route(input);
        e.Handled = result.Handled;

        switch (result.Action)
        {
            case KeyRoutingAction.MoveDown:
                ViewModel.MoveDownCommand.Execute(null);
                break;
            case KeyRoutingAction.MoveUp:
                ViewModel.MoveUpCommand.Execute(null);
                break;
            case KeyRoutingAction.RevealDown:
                if (ViewModel.Prompts.Count > 0)
                {
                    ViewModel.SelectedIndex = 0;
                    ViewModel.SelectedPrompt = ViewModel.Prompts[0];
                }
                break;
            case KeyRoutingAction.RevealUp:
                if (ViewModel.Prompts.Count > 0)
                {
                    var last = ViewModel.Prompts.Count - 1;
                    ViewModel.SelectedIndex = last;
                    ViewModel.SelectedPrompt = ViewModel.Prompts[last];
                }
                break;
            case KeyRoutingAction.Paste:
                ViewModel.PasteCommand.Execute(null);
                break;
            case KeyRoutingAction.PasteAsText:
                ViewModel.PasteAsTextCommand.Execute(null);
                break;
            case KeyRoutingAction.OpenEditor:
                ViewModel.OpenEditorCommand.Execute(null);
                break;
            case KeyRoutingAction.HandleEscape:
                ViewModel.HandleEscapeCommand.Execute(null);
                break;
            case KeyRoutingAction.EnterQuickAdd:
                ViewModel.EnterQuickAddCommand.Execute(null);
                break;
            case KeyRoutingAction.Create:
                ViewModel.CreateCommand.Execute(null);
                break;
            case KeyRoutingAction.CreateWithTitle:
                ViewModel.CreateWithTitleCommand.Execute(null);
                break;
            case KeyRoutingAction.QuickAddSave:
                ViewModel.QuickAdd?.SaveCommand.Execute(null);
                break;
        }
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PromptItemViewModel item)
        {
            var action = PaletteClickRouter.RouteMouseDown(
                e.ClickCount, IsButtonClick(e.OriginalSource as DependencyObject));

            if (action == ClickRoutingAction.SelectAndPaste)
            {
                _log.Debug("Card double-clicked: prompt={Id}", item.Prompt.Id);
                ViewModel.SelectedPrompt = item;
                ViewModel.SelectedIndex = ViewModel.Prompts.IndexOf(item);
                ViewModel.PasteCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PromptItemViewModel item)
        {
            var action = PaletteClickRouter.RouteMouseUp(
                IsButtonClick(e.OriginalSource as DependencyObject));

            if (action == ClickRoutingAction.Select)
            {
                _log.Debug("Card clicked: prompt={Id}", item.Prompt.Id);
                ViewModel.SelectedPrompt = item;
                ViewModel.SelectedIndex = ViewModel.Prompts.IndexOf(item);
            }
        }
    }

    private static bool IsButtonClick(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PromptItemViewModel item)
        {
            _log.Debug("EditButton_Click: prompt={Id}", item.Prompt.Id);
            ViewModel.RaiseEditRequested(item.Prompt);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PromptItemViewModel item)
        {
            _log.Debug("CopyButton_Click: prompt={Id}", item.Prompt.Id);
            ViewModel.RaiseCopyRequested(item.Prompt);
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _log.Debug("CloseButton_Click");
        HideWindow();
    }
}
