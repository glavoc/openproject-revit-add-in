//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;
using System.Reflection;

namespace OpenProjectNavisworks.Entry
{
    /// <summary>
    /// Obfuscation Ignore for External Interface
    /// </summary>
    [Obfuscation(Exclude = true, ApplyToMembers = false)]
    //[Transaction(TransactionMode.Manual)]
    //[Regeneration(RegenerationOption.Manual)]
    public sealed class CmdMainSettings 
    {
        public static int ExecuteCommand(bool apply, params string[] parameters)
        {
            var message = string.Empty;
            return RibbonButtonClickHandler.OpenSettingsPluginWindow(Model.NavisworksWrapper.Document, ref message);
        }
    }
}
