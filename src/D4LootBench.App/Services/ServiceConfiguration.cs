using System.IO;
using D4LootBench.Ai;
using D4LootBench.Ai.Import;
using D4LootBench.App.ViewModels;
using D4LootBench.App.ViewModels.Conditions;
using D4LootBench.App.ViewModels.Progression;
using D4LootBench.App.Views;
using D4LootBench.Core.Codec;
using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
using D4LootBench.Core.Models;
using D4LootBench.Core.Profiles;
using D4LootBench.Core.Progression;
using D4LootBench.Core.Validation;
using D4LootBench.Vision;
using Microsoft.Extensions.DependencyInjection;

namespace D4LootBench.App.Services;

internal static class ServiceConfiguration
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFilterDataService, FilterDataService>();
        services.AddSingleton<IFilterValidator, FilterValidator>();
        services.AddSingleton<IConditionViewModelFactory, ConditionViewModelFactory>();

        services.AddSingleton<LlmSettingsService>();
        services.AddSingleton<WindowSettingsService>();
        services.AddSingleton(new ProfileStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "D4LootBench", "profiles")));
        services.AddSingleton<SystemPromptBuilder>();
        services.AddSingleton<NameResolver>();
        services.AddSingleton<ILlmProvider, SettingsAwareLlmProvider>();
        services.AddSingleton<RuleAssistant>();
        services.AddSingleton<BuildGuideImporter>();
        services.AddSingleton<BuildGuideFilterGenerator>();

        services.AddSingleton<IGearReader, WindowsOcrGearReader>();
        services.AddSingleton<GearTooltipParser>();

        services.AddSingleton<WeaponRoleMap>();
        services.AddSingleton<GoalBuildFactory>();
        services.AddSingleton<SlotDiffEngine>();          // per-slot threshold comes from SlotGoal
        services.AddSingleton<ProgressionFilterGenerator>();
        services.AddSingleton<ProgressionFilterMerger>();

        // Static-block "Edit…" seam: opens a modal BlockEditorWindow hosting the shared rule editor, seeded
        // from the block's current ruleset, and returns the edited block's share code (null on Cancel). The
        // wizard VM invokes this through a Func so it never news up a Window — keeping it headless-testable.
        services.AddSingleton<Func<FilterRuleset, string, string?>>(sp => (ruleset, title) =>
        {
            var factory = sp.GetRequiredService<IConditionViewModelFactory>();
            var editorVm = new VisualEditorViewModel(factory, ruleset);
            var window = new BlockEditorWindow
            {
                DataContext = editorVm,
                Title = title,
                Owner = System.Windows.Application.Current?.Windows
                    .OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive),
            };

            return window.ShowDialog() == true
                ? FilterCodec.Encode(editorVm.BuildRuleset())
                : null;
        });

        services.AddTransient<ProgressionWizardViewModel>();
        services.AddSingleton<Func<ProgressionWizardViewModel>>(
            sp => sp.GetRequiredService<ProgressionWizardViewModel>);

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
