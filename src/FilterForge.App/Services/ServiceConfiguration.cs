using ThunderEagle.FilterForge.Ai;
using ThunderEagle.FilterForge.App.ViewModels;
using ThunderEagle.FilterForge.App.ViewModels.Conditions;
using ThunderEagle.FilterForge.Core.Data;
using ThunderEagle.FilterForge.Core.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace ThunderEagle.FilterForge.App.Services;

internal static class ServiceConfiguration
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFilterDataService, FilterDataService>();
        services.AddSingleton<IFilterValidator, FilterValidator>();
        services.AddSingleton<IConditionViewModelFactory, ConditionViewModelFactory>();

        services.AddSingleton<LlmSettingsService>();
        services.AddSingleton<SystemPromptBuilder>();
        services.AddSingleton<NameResolver>();
        services.AddSingleton<ILlmProvider, SettingsAwareLlmProvider>();
        services.AddSingleton<RuleAssistant>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
