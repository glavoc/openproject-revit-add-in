using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Autodesk.Navisworks.Api.Plugins;
using OpenProjectNavisworks.Model;
using OpenProjectNavisworks.Entry;
using MessageBox = System.Windows.MessageBox;
using System;

namespace OpenProjectNavisworks.Command;

public abstract class IAddinCommand
{
    public abstract int Action(params string[] parameters);

    public void Execute(params string[] parameters)
    {
        try
        {
            Action(parameters);
        }
        catch (Exception e)
        {
            MessageBox.Show(e.ToString());
        }
    }
}
/// <summary>
/// Executer the command Addin Manager Manual
/// </summary>
public class AddInManagerManual : IAddinCommand
{
    public override int Action(params string[] parameters)
    {
        Debug.Listeners.Clear();
        Trace.Listeners.Clear();
        CodeListener codeListener = new CodeListener();
        Debug.Listeners.Add(codeListener);
        return AddinManagerBase.Instance.ExecuteCommand(false, parameters);
    }
}

/// <summary>
/// Execute the command Addin Manager Faceless
/// </summary>
public class AddInManagerFaceLess : IAddinCommand
{
    public override int Action(params string[] parameters)
    {
        return AddinManagerBase.Instance.ExecuteCommand(true, parameters);
    }
}


/// <summary>
/// Execute the command for connection to project
/// </summary>
public class ConnectOpenProject : IAddinCommand
{
    public override int Action(params string[] parameters)
    {
        Model.NavisworksWrapper.Document = Autodesk.Navisworks.Api.Application.ActiveDocument;
        //Model.NavisworksWrapper.Document Autodesk.Navisworks.Api.Application;
        return CmdMain.ExecuteCommand(true, parameters);
    }
}


/// <summary>
/// Execute the command for connection settings to project
/// </summary>
public class ConnectSettingsOpenProject : IAddinCommand
{
    public override int Action(params string[] parameters)
    {
        Model.NavisworksWrapper.Document = Autodesk.Navisworks.Api.Application.ActiveDocument;
        return CmdMainSettings.ExecuteCommand(true, parameters);
    }
}


public class Test : IAddinCommand
{
    public override int Action(params string[] parameters)
    {
        MessageBox.Show("Hello World");
        return 0;
    }
}

public class Guide : IAddinCommand
{
    public override int Action(params string[] parameters)
    {
        Process.Start("http://books.ide-spb.com/books/autodesk-revit/page/idp-viewpoint");
        return 0;
    }
}

public class Info : IAddinCommand
{
    public override int Action(params string[] parameters)
    {
        Process.Start("https://gitlab.ide-spb.com/Nedviga.Pavel/openprojectnavisbcf/-/releases");
        return 0;
    }
}
