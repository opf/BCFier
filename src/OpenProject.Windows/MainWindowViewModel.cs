using System;
using System.Windows;
using OpenProject.ViewModel;

namespace OpenProject.Windows
{
  public sealed class MainWindowViewModel : ViewModelBase
  {
    private const double _windowMinWidth = 730.00;
    private const double _taskBarHeight = 50;


    public string TestText => "Dies ist ein Testtext";

    /// <summary>
    /// The view model of the nested BcfierPanel.
    /// </summary>
    public BcfierPanelViewModel BcfierPanelViewModel { get; }

    /// <summary>
    /// The MainWindow width. It allows to IFC issue cards even when the OP menu on the left is open.
    /// </summary>
    public static double Width => SystemParameters.PrimaryScreenHeight < _windowMinWidth
      ? SystemParameters.PrimaryScreenHeight
      : Math.Max(_windowMinWidth, SystemParameters.PrimaryScreenHeight * 0.25);

    /// <summary>
    /// The MainWindow height.
    /// </summary>
    public static double Height => SystemParameters.PrimaryScreenHeight - _taskBarHeight;

    /// <summary>
    /// The MainWindow top margin.
    /// </summary>
    public static double Top => 0;

    /// <summary>
    /// The MainWindow left margin.
    /// </summary>
    public static double Left => SystemParameters.PrimaryScreenWidth - Width;


    public MainWindowViewModel(BcfierPanelViewModel bcfierPanelViewModel)
    {
      BcfierPanelViewModel = bcfierPanelViewModel;
    }
  }
}
