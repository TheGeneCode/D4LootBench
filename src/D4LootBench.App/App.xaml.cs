using System.Windows;
using D4LootBench.App.Services;
using D4LootBench.Core.Data;
using D4LootBench.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace D4LootBench.App;

public partial class App
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        Services = ServiceConfiguration.Build();
        FilterDataContext.Set(Services.GetRequiredService<IFilterDataService>());
        base.OnStartup(e);

        var window = Services.GetRequiredService<MainWindow>();
        window.Show();
    }
}
