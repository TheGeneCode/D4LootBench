using System.Windows;
using ThunderEagle.FilterForge.App.Services;
using ThunderEagle.FilterForge.Core.Data;
using ThunderEagle.FilterForge.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace ThunderEagle.FilterForge.App;

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
