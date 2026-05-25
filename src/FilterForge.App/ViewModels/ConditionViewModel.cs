using CommunityToolkit.Mvvm.ComponentModel;
using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels;

public abstract partial class ConditionViewModel : ObservableObject
{
    public abstract string TypeName { get; }
    public virtual string Summary => "";
    public abstract Condition BuildModel();

    /// <summary>
    /// Called when the global class filter changes. Override in subclasses that have
    /// class-specific picker sources (affixes, item types, etc.).
    /// </summary>
    public virtual void ApplyClassFilter(PlayerClass playerClass) { }
}
