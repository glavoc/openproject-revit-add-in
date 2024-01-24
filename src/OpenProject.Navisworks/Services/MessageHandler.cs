using System;
//using Autodesk.Revit.UI;
using Autodesk.Navisworks.Gui;
using Autodesk.Windows;
using Serilog;

namespace OpenProjectNavisworks.Services
{
  public static class MessageHandler
  {
        public static void ShowWarning(string title, string instruction, string content)
        {
            Log.Warning($"{title}: {content}");

            var dialog = new TaskDialog();
            dialog.WindowTitle = title;
            dialog.ContentText = content;
            dialog.ExpandedText = instruction;
            dialog.Show();
        }

        public static void ShowError(Exception exception, string message)
        {
            if (exception != null)
                Log.Error(exception, message);
            else
                Log.Error(message);

            var dialog = new TaskDialog();
            dialog.MainIcon = TaskDialogIcon.Error;
            dialog.ContentText = message;
            
            dialog.Show();
        }
    }
}
