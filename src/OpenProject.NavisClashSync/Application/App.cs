using Autodesk.Navisworks.Api.Plugins;
using System.Windows.Forms;

namespace OpenProjectNavisClashSync.Application
{

  [Plugin("CustomTabSample", "LM", DisplayName = "BB-ViewPoint")]
  [RibbonLayout("BBAddinManagerRibbon.xaml")]
  [RibbonTab("BBTab", DisplayName = "BB")]
  [Command("Button_One",
      DisplayName = "BB-One",
      Icon = "Resources\\lab16x16.png",
      LargeIcon = "Resources\\lab32x32.png",
      ToolTip = "Plugin BB-ViewPoint")]
  [Command("Button_Two",
      DisplayName = "BB-Two",
      Icon = "Resources\\OpenProjectLogo16.png",
      LargeIcon = "Resources\\OpenProjectLogo32.png",
      ToolTip = "Plugin BB-ViewPoint")]

  public class App : CommandHandlerPlugin
  {
    public override int ExecuteCommand(string name, params string[] parameters)
    {
      // Buttons
      switch (name)
      {

        case "Button_One":
          MessageBox.Show("Button One");
          break;
        case "Button_Two":
          MessageBox.Show("Button Two");
          break;
      }
      return 0;
    }
  }
}
