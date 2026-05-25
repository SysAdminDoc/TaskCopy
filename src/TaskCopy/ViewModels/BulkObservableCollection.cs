using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace TaskCopy.ViewModels;

/// <summary>
/// I37: ObservableCollection that can suspend CollectionChanged notifications
/// during a bulk replace, then fire a single Reset event. WPF rebinds the
/// whole list in one frame instead of issuing N add/remove events.
///
/// Used by SnippetMenuViewModel.ApplyFilter at >500-snippet libraries where
/// typing-while-filter went visibly choppy in the prior per-Add design.
/// </summary>
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suspended;

    /// <summary>
    /// Replace the entire collection with <paramref name="items"/> while
    /// firing only one CollectionChanged(Reset) at the end. Order matches
    /// the input enumerable.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        _suspended = true;
        try
        {
            Items.Clear();
            foreach (var i in items) Items.Add(i);
        }
        finally
        {
            _suspended = false;
        }
        // Single Reset notification rebuilds bindings in one pass.
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_suspended) return;
        base.OnCollectionChanged(e);
    }
}
