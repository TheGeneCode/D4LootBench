using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace D4Loot.App.Behaviors;

/// <summary>
/// Attached behavior: when set to <c>true</c> on an <see cref="ItemsControl"/>, any item added
/// to <see cref="ItemsControl.ItemsSource"/> at runtime has its generated container scrolled
/// into view. The user-visible problem this solves: adding a condition off-screen with no
/// feedback. The behavior is no-op for initial population (only reacts to <c>Add</c>).
/// </summary>
public static class ScrollNewItemsIntoView
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(ScrollNewItemsIntoView),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl ic) return;

        if ((bool)e.NewValue)
        {
            ((INotifyCollectionChanged)ic.Items).CollectionChanged += (_, args) => OnCollectionChanged(ic, args);
        }
        // No detach path — controls live for the duration of the window in our usage.
    }

    private static void OnCollectionChanged(ItemsControl ic, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action != NotifyCollectionChangedAction.Add || args.NewItems is null) return;

        var item = args.NewItems[args.NewItems.Count - 1];
        if (item is null) return;

        // Container generation is asynchronous — defer the scroll until layout completes.
        ic.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (ic.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement container)
                container.BringIntoView();
        });
    }
}
