﻿using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using OpenProjectNavisworks.ViewModel;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace OpenProjectNavisworks.View;

/// <summary>
/// Interaction logic for FrmAddInManager.xaml
/// </summary>
public partial class FrmAddInManager : Window
{
    private readonly AddInManagerViewModel viewModel;

    public FrmAddInManager(AddInManagerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        viewModel = vm;
        vm.FrmAddInManager = this;
        this.Closing += FrmAddInManager_Closing;
    }

    private void FrmAddInManager_Closing(object sender, CancelEventArgs e)
    {
        viewModel.FrmAddInManager = null;
    }

    private void TbxDescription_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (viewModel.MAddinManagerBase.ActiveCmdItem != null && TabControl.SelectedIndex == 0)
        {
            viewModel.MAddinManagerBase.ActiveCmdItem.Description = TbxDescription.Text;
        }
        if (viewModel.MAddinManagerBase.ActiveAppItem != null && TabControl.SelectedIndex == 1)
        {
            viewModel.MAddinManagerBase.ActiveAppItem.Description = TbxDescription.Text;
        }
        viewModel.MAddinManagerBase.AddinManager.SaveToAimIni();
    }

    private void TreeViewCommand_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        if (e.Delta > 0)
            ZoomIn();

        else if (e.Delta < 0)
            ZoomOut();
    }
    void ZoomIn()
    {
        if (TreeViewCommand.FontSize <= 30)
        {
            TreeViewCommand.FontSize += 2f;
        }
    }
    void ZoomOut()
    {
        if(TreeViewCommand.FontSize >= 10)
        {
            TreeViewCommand.FontSize -= 2f;
        }
    }

    private void HandleTextboxKeyPress(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (viewModel.IsTabCmdSelected)
            {
                TreeViewCommand.Focus();
            }
            else
            {
                LogControl.Focus();
            }
        }
    }
    private void HandleTreeViewCommandKeyPress(object sender, KeyEventArgs e)
    {
        int indexCmd = TreeViewCommand.Items.IndexOf(TreeViewCommand.SelectedItem);
        if (e.Key == Key.Up && TabCommand.IsFocused)
        {
            tbxSearch.Focus();
        }
        else if (e.Key == Key.Up && indexCmd==0 && TabCommand.IsSelected)
        {
            TabCommand.Focus();
        }
        if (e.Key == Key.Down && TabCommand.IsSelected)
        {
            TreeViewCommand.Focus();
        }
        if (e.Key == Key.Enter)
        {
            viewModel.ExecuteAddinCommandClick();
        }
    }
    private void CloseFormEvent(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}