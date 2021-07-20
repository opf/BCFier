using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpenProject.ViewModel;

namespace OpenProject.Windows
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App
  {
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
      var serviceCollection = new ServiceCollection();
      serviceCollection.AddSingleton<MainWindowViewModel>();
      serviceCollection.AddSingleton<BcfierPanelViewModel>();

      _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
      base.OnStartup(e);
      var mainWindowViewModel = _serviceProvider.GetService<MainWindowViewModel>();
      var window = new MainWindow { DataContext = mainWindowViewModel };
      window.Show();
    }
  }
}
