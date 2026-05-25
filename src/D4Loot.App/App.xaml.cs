using System.Windows;
using D4Loot.App.Services;
using D4Loot.Core.Data;
using D4Loot.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace D4Loot.App;

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
