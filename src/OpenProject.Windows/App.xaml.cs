using System;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpenProject.Views;
using OpenProject.ViewModels;
using OpenProject.WebViewIntegration;

namespace OpenProject.Windows
{
  /// <summary>
  /// Root application class. Configures and keeps state of the IoC container.
  /// </summary>
  public partial class App
  {
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
      _serviceProvider = ConfigureIoCContainer().BuildServiceProvider();
    }

    private static IServiceCollection ConfigureIoCContainer()
    {
      var services = new ServiceCollection();

      // Views
      services.AddSingleton<MainWindowView>();
      services.AddSingleton<WebView>();

      // View Models
      services.AddSingleton<MainWindowViewModel>();
      services.AddSingleton<WebViewModel>();

      // other services
      services.AddSingleton<BrowserManager>();
      services.AddSingleton<BcfierJavascriptInterop>();
      services.AddSingleton<JavaScriptBridge>();
      services.AddTransient<OpenProjectInstanceValidator>();

      // We're using HttpClientFactory to ensure that we don't hit any problems with
      // port exhaustion or stale DNS entries in long-lived HttpClients
      services.AddHttpClient(nameof(OpenProjectInstanceValidator))
        .ConfigureHttpMessageHandlerBuilder(h =>
        {
          if (h.PrimaryHandler is HttpClientHandler httpClientHandler)
          {
            // It defaults to true, but let's ensure it stays that way
            httpClientHandler.AllowAutoRedirect = true;
          }
        });

      return services;
    }

    private void OnStartUp(object sender, StartupEventArgs e) => _serviceProvider.GetService<MainWindowView>().Show();
  }
}
