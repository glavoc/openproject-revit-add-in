﻿using System;
using System.IO;
using System.Windows;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Bcfier.UserControls;
using Bcfier.ViewModels.Bcf;

namespace Bcfier.Win
{

  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      InitializeComponent();

      string[] args = Environment.GetCommandLineArgs();
      if (args.Length > 1 && File.Exists(args[1]))
      {
        Bcfier.Dispatcher.BeginInvoke(DispatcherPriority.Background,
             new Action(() => Bcfier.BcfFileClicked(args[1])));
      }
    }

    /// <summary>
    /// passing event to the user control
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Window_Closing(object sender, CancelEventArgs e)
    {
      e.Cancel = Bcfier.onClosing(e);
    }

    #region commands


    private void OnAddView(object sender, ExecutedRoutedEventArgs e)
    {
      try
      {

        if (Bcfier.SelectedBcf() == null)
          return;
        var issue = e.Parameter as BcfIssueViewModel;
        if (issue == null)
        {
          MessageBox.Show("No Issue selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
          return;
        }

        var addView = new AddView(issue, Path.Combine(Path.GetTempPath(), issue.Markup.BcfTopic.Id.ToString()));
        var win = new Window
        {
          Content = addView,
          Title = "Add View",
          SizeToContent = SizeToContent.WidthAndHeight,
          WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        win.ShowDialog();
        if (win.DialogResult.HasValue && win.DialogResult.Value)
          Bcfier.SelectedBcf().IsModified = true;

      }
      catch (Exception ex)
      {
        MessageBox.Show("Exception: " + Environment.NewLine + ex);
      }
    }
    #endregion

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
      Task.Run(() =>
      {
        StatHat.Post.EzCounter(@"hello@teocomi.com", "BCFierWinStart", 1);
      });
    }
  }
}