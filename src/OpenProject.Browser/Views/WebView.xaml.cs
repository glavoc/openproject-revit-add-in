using OpenProject.Browser.ViewModels;

namespace OpenProject.Browser.Views
{
  /// <summary>
  /// Main panel UI and logic that need to be used by all modules
  /// </summary>
  public partial class WebView
  {
    public WebView(WebViewModel viewModel)
    {
      DataContext = viewModel;
      InitializeComponent();
    }
  }
}
