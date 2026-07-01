using D4LootBench.Ai;
using D4LootBench.Ai.Import;
using D4LootBench.App.ViewModels;
using D4LootBench.App.ViewModels.Conditions;
using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
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
        services.AddSingleton<SystemPromptBuilder>();
        services.AddSingleton<NameResolver>();
        services.AddSingleton<ILlmProvider, SettingsAwareLlmProvider>();
        services.AddSingleton<RuleAssistant>();
        services.AddSingleton<BuildGuideImporter>();
        services.AddSingleton<BuildGuideFilterGenerator>();

        services.AddSingleton<IGearReader, WindowsOcrGearReader>();
        services.AddSingleton<GearTooltipParser>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
