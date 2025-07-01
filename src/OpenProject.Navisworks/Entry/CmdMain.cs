using System.Reflection;
using Autodesk.Navisworks.Api.Plugins;
namespace OpenProjectNavisworks.Entry;

/// <summary>
/// Obfuscation Ignore for External Interface
/// </summary>
[Obfuscation(Exclude = true, ApplyToMembers = false)]
//[Transaction(TransactionMode.Manual)]
//[Regeneration(RegenerationOption.Manual)]
public sealed class CmdMain
{
    /// <summary>
    /// Main Command Entry Point
    /// </summary>
    /// <param name="commandData"></param>
    /// <param name="message"></param>
    /// <param name="elements"></param>
    /// <returns></returns>
    public static int ExecuteCommand(bool apply, params string[] parameters)
    {
        var message = string.Empty;
        return RibbonButtonClickHandler.OpenMainPluginWindow(Model.NavisworksWrapper.Document, ref message);
    }
}

