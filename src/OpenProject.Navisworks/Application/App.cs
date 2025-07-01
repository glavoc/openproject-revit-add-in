using Autodesk.Navisworks.Api.Plugins;
using OpenProjectNavisworks.Command;
using OpenProjectNavisworks.Properties;
using Serilog;
using Test = OpenProjectNavisworks.Command.Test;
using OpenProject.Shared.Logging;
using System;


namespace OpenProjectNavisworks.Application;

[Plugin("IDP", "PavelNedviga,TachkovMaksim", DisplayName = "IDP-ViewPoint")]
[RibbonLayout("AddinManagerRibbon.xaml")]
[RibbonTab("ID_AddinManager_TAB", DisplayName = "IDP")]
[Command("IDE_ButtonConnectOpenProject",
    DisplayName = "IDP-ViewPoint",
    Icon = "Resources\\OpenProjectLogo16.png",
    LargeIcon = "Resources\\OpenProjectLogo32.png",
    ToolTip = "Plugin IDP-ViewPoint")]


public class App : CommandHandlerPlugin
{
    public override int ExecuteCommand(string name, params string[] parameters)
    {
        

        // Buttons
        switch (name)
        {
            case "ID_ButtonAddinManagerManual":
                AddInManagerManual addInManagerManual = new AddInManagerManual();
                addInManagerManual.Execute();
                break;
            case "ID_ButtonAddinManagerFaceless":
                AddInManagerFaceLess addInManagerFaceless = new AddInManagerFaceLess();
                addInManagerFaceless.Execute();
                break;
            case "ID_ButtonDockPanelCommand":
                DockPanelCommand dockPanelCommand = new DockPanelCommand();
                dockPanelCommand.Execute();
                break;
            case "ID_ButtonTest":
                Test test = new Test();
                test.Execute();
                break;
            case "IDE_ButtonConnectOpenProject":
                ConnectOpenProject connOP = new ConnectOpenProject();
                connOP.Execute();
                break;
            case "IDE_ButtonSettingsOpenProject":
                ConnectSettingsOpenProject connSetOP = new ConnectSettingsOpenProject();
                connSetOP.Execute();
                break;
            case "IDE_ButtonGuide":
                Guide guide = new Guide();
                guide.Execute();
                break;
            case "IDE_ButtonInfo":
                Info info = new Info();
                info.Execute();
                break;
        }       
        
        return 0;
    }
}