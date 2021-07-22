using System.Windows;

namespace OpenProject.Windows
{
  /// <summary>
  /// Interaction logic for NewVersion.xaml
  /// </summary>
  public partial class NewVersion
  {
    public NewVersion()
    {
      InitializeComponent();
    }

    private void Button_Cancel(object sender, RoutedEventArgs e)
    {
      DialogResult = false;
    }

    private void Button_OK(object sender, RoutedEventArgs e)
    {
      DialogResult = true;
    }
  }
}
