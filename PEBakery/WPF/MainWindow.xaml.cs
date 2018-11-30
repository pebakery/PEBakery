﻿/*
    Copyright (C) 2016-2018 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using MahApps.Metro.IconPacks;
using PEBakery.Core;
using PEBakery.Core.ViewModels;
using PEBakery.Helper;
using SQLite;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace PEBakery.WPF
{
    #region MainWindow
    // ReSharper disable once RedundantExtendsListEntry
    public partial class MainWindow : Window
    {
        #region Fields and Properties
        public string BaseDir => Global.BaseDir;

        public Logger Logger { get; }

        private static MainViewModel Model
        {
            get => Global.MainViewModel;
            set => Global.MainViewModel = value;
        }

        public LogWindow LogDialog = null;
        public UtilityWindow UtilityDialog = null;
        public ScriptEditWindow ScriptEditDialog = null;
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            Model = DataContext as MainViewModel;
            if (Model == null)
            {
                MessageBox.Show("MainViewModel is null", "Internal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            string[] args = Global.Args;
            if (!NumberHelper.ParseInt32(Properties.Resources.EngineVersion, out Global.Version))
            {
                MessageBox.Show($"Invalid version [{Global.Version}]", "Invalid Version", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            string argBaseDir = Environment.CurrentDirectory;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("/basedir", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        argBaseDir = Path.GetFullPath(args[i + 1]);
                        if (!Directory.Exists(argBaseDir))
                        {
                            MessageBox.Show($"Directory [{argBaseDir}] does not exist", "Invalid BaseDir", MessageBoxButton.OK, MessageBoxImage.Error);
                            Environment.Exit(1); // Force Shutdown
                        }
                        Environment.CurrentDirectory = argBaseDir;
                    }
                    else
                    {
                        // ReSharper disable once LocalizableElement
                        Console.WriteLine("\'/basedir\' must be used with path\r\n");
                    }
                }
                else if (args[i].Equals("/?", StringComparison.OrdinalIgnoreCase) ||
                         args[i].Equals("/help", StringComparison.OrdinalIgnoreCase) ||
                         args[i].Equals("/h", StringComparison.OrdinalIgnoreCase))
                {
                    // ReSharper disable once LocalizableElement
                    Console.WriteLine("Sorry, help message not implemented\r\n");
                }
            }

            Global.BaseDir = argBaseDir;

            // Setting File
            string settingFile = Path.Combine(BaseDir, "PEBakery.ini");
            Global.Setting = new SettingViewModel(settingFile);
            Model.MonospacedFont = Global.Setting.Interface_MonospaceFont;

            // Custom Title
            if (Global.Setting.Interface_UseCustomTitle)
                Model.TitleBar = Global.Setting.Interface_CustomTitle;

            // Database Directory
            string dbDir = Path.Combine(BaseDir, "Database");
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            // Log Database
            string logDbFile = Path.Combine(dbDir, "PEBakeryLog.db");
            try
            {
                Global.Logger = Logger = new Logger(logDbFile);
                Logger.SystemWrite(new LogInfo(LogState.Info, "PEBakery launched"));
            }
            catch (SQLiteException e)
            { // Update failure
                string msg = $"SQLite Error : {e.Message}\r\n\r\nLog database is corrupted.\r\nPlease delete PEBakeryLog.db and restart.";
                MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
            Global.Setting.LogDb = Logger.Db;

            // If script cache is enabled, generate cache after 5 seconds
            if (Global.Setting.Script_EnableCache)
            {
                string cacheDbFile = Path.Combine(dbDir, "PEBakeryCache.db");
                try
                {
                    Global.ScriptCache = new ScriptCache(cacheDbFile);
                    Logger.SystemWrite(new LogInfo(LogState.Info, $"ScriptCache enabled, {Global.ScriptCache.Table<DB_ScriptCache>().Count()} cached scripts found"));
                }
                catch (SQLiteException e)
                { // Update failure
                    string msg = $"SQLite Error : {e.Message}\r\n\r\nCache database is corrupted.\r\nPlease delete PEBakeryCache.db and restart.";
                    MessageBox.Show(msg, "SQLite Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown(1);
                }

                Global.Setting.ScriptCache = Global.ScriptCache;
            }
            else
            {
                Logger.SystemWrite(new LogInfo(LogState.Info, "ScriptCache disabled"));
            }

            CommandManager.InvalidateRequerySuggested();

            // Load Projects
            Model.StartLoadingProjects();
        }
        #endregion

        #region Main Buttons
        private void ProjectBuildStartCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Model?.CurMainTree?.Script != null && !Model.WorkInProgress && Engine.WorkingLock == 0;
        }

        private async void ProjectBuildStartCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // TODO: Better locking system?
            Interlocked.Increment(ref Engine.WorkingLock);
            try
            {
                // Get current project
                Project p = Model.CurMainTree.Script.Project;

                Model.BuildTreeItems.Clear();
                ProjectTreeItemModel treeRoot = MainViewModel.PopulateOneTreeItem(p.MainScript, null, null);
                MainViewModel.ScriptListToTreeViewModel(p, p.ActiveScripts, false, treeRoot);
                Model.BuildTreeItems.Add(treeRoot);
                Model.CurBuildTree = null;

                EngineState s = new EngineState(p, Logger, Model);
                s.SetOptions(Global.Setting);

                Engine.WorkingEngine = new Engine(s);

                // Build Start, Switch to Build View
                Model.SwitchNormalBuildInterface = false;

                // Turn on progress ring
                Model.WorkInProgress = true;

                // Set StatusBar Text
                CancellationTokenSource ct = new CancellationTokenSource();
                Task printStatus = MainViewModel.PrintBuildElapsedStatus($"Building {p.ProjectName}...", s, ct.Token);

                // Run
                int buildId = await Engine.WorkingEngine.Run($"Project {p.ProjectName}");

#if DEBUG
                Logger.ExportBuildLog(LogExportType.Text, Path.Combine(s.BaseDir, "LogDebugDump.txt"), buildId, new LogExporter.BuildLogOptions
                {
                    IncludeComments = true,
                    IncludeMacros = true,
                });
#endif

                // Cancel and Wait until PrintBuildElapsedStatus stops
                // Report elapsed time
                ct.Cancel();
                await printStatus;
                Model.StatusBarText = $"{p.ProjectName} build done ({s.Elapsed:h\\:mm\\:ss})";

                // Turn off progress ring
                Model.WorkInProgress = false;

                // Build Ended, Switch to Normal View
                Model.SwitchNormalBuildInterface = true;
                Model.BuildTreeItems.Clear();
                Model.DisplayScript(Model.CurMainTree.Script);

                if (Global.Setting.General_ShowLogAfterBuild && LogWindow.Count == 0)
                { // Open BuildLogWindow
                    LogDialog = new LogWindow(1);
                    LogDialog.Show();
                }
            }
            finally
            {
                Engine.WorkingEngine = null;
                Interlocked.Decrement(ref Engine.WorkingLock);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ProjectBuildStopCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Engine.WorkingEngine != null && Engine.WorkingLock != 0;
        }

        private void ProjectBuildStopCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Do not set Engine.WorkingEngine to null, it will take some time to finish a build.
            Engine.WorkingEngine?.ForceStop();
        }

        private void ProjectLoading_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Model != null && Model.ProjectsLoading == 0;
        }

        private void ProjectRefreshCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Model.StartLoadingProjects();
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private void SettingWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            double old_Interface_ScaleFactor = Global.Setting.Interface_ScaleFactor;
            bool old_Compat_AsteriskBugDirLink = Global.Setting.Compat_AsteriskBugDirLink;
            bool old_Compat_OverridableFixedVariables = Global.Setting.Compat_OverridableFixedVariables;
            bool old_Compat_EnableEnvironmentVariables = Global.Setting.Compat_EnableEnvironmentVariables;
            bool old_Script_EnableCache = Global.Setting.Script_EnableCache;

            SettingWindow dialog = new SettingWindow { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                // Apply
                Global.Setting.ApplySetting();

                // Refresh Projects
                if (old_Compat_AsteriskBugDirLink != Global.Setting.Compat_AsteriskBugDirLink ||
                    old_Compat_OverridableFixedVariables != Global.Setting.Compat_OverridableFixedVariables ||
                    old_Compat_EnableEnvironmentVariables != Global.Setting.Compat_EnableEnvironmentVariables)
                {
                    Model.StartLoadingProjects();
                }
                else
                {
                    // Scale Factor
                    double newScaleFactor = Global.Setting.Interface_ScaleFactor;
                    if (double.Epsilon < Math.Abs(newScaleFactor - old_Interface_ScaleFactor)) // Not Equal
                        Model.DisplayScript(Model.CurMainTree.Script);

                    // Script
                    if (!old_Script_EnableCache && Global.Setting.Script_EnableCache)
                        Model.StartScriptCaching();
                }
            }
        }

        private void UtilityWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Model != null && Model.ProjectsLoading == 0 && UtilityWindow.Count == 0;
        }

        private void UtilityWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            UtilityDialog = new UtilityWindow(Global.Setting.Interface_MonospaceFont) { Owner = this };
            UtilityDialog.Show();
        }

        private void LogWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Model != null && Model.ProjectsLoading == 0 && LogWindow.Count == 0;
        }

        private void LogWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            LogDialog = new LogWindow { Owner = this };
            LogDialog.Show();
        }

        private void ProjectUpdateCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Model != null && !Model.WorkInProgress;
        }

        private void ProjectUpdateCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Model.WorkInProgress = true;
            try
            {
                MessageBox.Show("To be implemented", "Sorry", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Model.WorkInProgress = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void AboutWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Global.Setting != null;
        }

        private void AboutWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            AboutWindow dialog = new AboutWindow(Global.Setting.Interface_MonospaceFont) { Owner = this };
            dialog.ShowDialog();
        }
        #endregion

        #region Script Button and Context Menu
        private void ScriptCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Model?.CurMainTree?.Script != null && !Model.WorkInProgress;
        }

        private async void ScriptRunCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Script sc = Model.CurMainTree.Script;
            if (!sc.Sections.ContainsKey(ScriptSection.Names.Process))
            {
                Model.StatusBarText = $"Section [Process] does not exist in {sc.Title}";
                return;
            }

            if (Engine.WorkingLock == 0)  // Start Build
            {
                Interlocked.Increment(ref Engine.WorkingLock);

                // Populate BuildTree
                Model.BuildTreeItems.Clear();
                ProjectTreeItemModel rootItem = MainViewModel.PopulateOneTreeItem(sc, null, null);
                Model.BuildTreeItems.Add(rootItem);
                Model.CurBuildTree = null;

                EngineState s = new EngineState(sc.Project, Logger, Model, EngineMode.RunMainAndOne, sc);
                s.SetOptions(Global.Setting);

                Engine.WorkingEngine = new Engine(s);

                // Switch to Build View
                Model.SwitchNormalBuildInterface = false;
                CancellationTokenSource ct = new CancellationTokenSource();
                Task printStatus = MainViewModel.PrintBuildElapsedStatus($"Running {sc.Title}...", s, ct.Token);

                // Run
                int buildId = await Engine.WorkingEngine.Run($"{sc.Title} - Run");

#if DEBUG
                Logger.ExportBuildLog(LogExportType.Text, Path.Combine(s.BaseDir, "LogDebugDump.txt"), buildId, new LogExporter.BuildLogOptions
                {
                    IncludeComments = true,
                    IncludeMacros = true,
                });
#endif

                // Cancel and Wait until PrintBuildElapsedStatus stops
                // Report elapsed time
                TimeSpan t = s.Elapsed;

                ct.Cancel();
                await printStatus;
                Model.StatusBarText = $"{sc.Title} took {t:h\\:mm\\:ss}";

                // Build Ended, Switch to Normal View
                Model.SwitchNormalBuildInterface = true;
                Model.BuildTreeItems.Clear();
                Model.DisplayScript(Model.CurMainTree.Script);

                if (Global.Setting.General_ShowLogAfterBuild && LogWindow.Count == 0)
                { // Open BuildLogWindow
                    LogDialog = new LogWindow(1);
                    LogDialog.Show();
                }

                Engine.WorkingEngine = null;
                Interlocked.Decrement(ref Engine.WorkingLock);
            }
            else // Stop Build
            {
                Engine.WorkingEngine?.ForceStop();
            }
        }

        private void ScriptRefreshCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Model.StartRefreshScript();
        }

        private void ScriptEditCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (Model.IsTreeEntryFile)
            { // Open Context Menu
                if (e.Source is Button button && button.ContextMenu is ContextMenu menu)
                {
                    menu.PlacementTarget = button;
                    menu.IsOpen = true;
                }
            }
            else
            { // Open Folder
                MainViewCommands.ScriptExternalEditorCommand.Execute(null, null);
            }
        }

        private void ScriptInternalEditorCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Script sc = Model.CurMainTree.Script;
            if (ScriptEditWindow.Count == 0)
            {
                ScriptEditDialog = new ScriptEditWindow(sc) { Owner = this };

                // Open as Modal
                // If ScriptEditWindow returns true in DialogResult, refresh script
                if (ScriptEditDialog.ShowDialog() == true)
                {
                    sc = ScriptEditDialog.Tag as Script;
                    Debug.Assert(sc != null, $"{nameof(sc)} != null");

                    Model.DisplayScript(sc);
                    Model.CurMainTree.Script = sc;
                }
            }
        }

        private void ScriptExternalEditorCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Script sc = Model.CurMainTree.Script;
            switch (sc.Type)
            {
                case ScriptType.Script:
                case ScriptType.Link:
                    MainViewModel.OpenTextFile(sc.RealPath);
                    break;
                default:
                    MainViewModel.OpenFolder(sc.RealPath);
                    break;
            }
        }

        private void ScriptUpdateCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MessageBox.Show("To be implemented", "Sorry", MessageBoxButton.OK, MessageBoxImage.Error);
            /*
            Script sc = CurMainTree.Script;
            Project p = CurMainTree.Script.Project;

            Model.BuildTree.Children.Clear();
            ScriptListToTreeViewModel(p, new List<Script> { sc }, false, Model.BuildTree, null);
            CurBuildTree = null;

            // Switch to Build View
            Model.BuildScriptFullProgressVisibility = Visibility.Collapsed;
            Model.SwitchNormalBuildInterface = false;

            // Turn on progress ring
            Model.WorkInProgress = true;

            Stopwatch watch = Stopwatch.StartNew();

            Script newScript = null;
            string msg = string.Empty;
            Task task = Task.Run(() =>
            {
                FileUpdaterOptions opts = new FileUpdaterOptions { Model = Model };
                if (Setting.General_UseCustomUserAgent)
                    opts.UserAgent = Setting.General_CustomUserAgent;

                (newScript, msg) = FileUpdater.UpdateScript(p, sc, opts);
            });
            task.Wait();

            if (newScript == null)
            { // Failure
                MessageBox.Show(msg, "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Global.Logger.SystemWrite(new LogInfo(LogState.Error, msg));
            }
            else
            {
                PostRefreshScript(CurMainTree, newScript);

                MessageBox.Show(msg, "Update Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Global.Logger.SystemWrite(new LogInfo(LogState.Success, msg));
            }

            watch.Stop();
            TimeSpan t = watch.Elapsed;
            Model.StatusBarText = $"{p.ProjectName} build done ({t:h\\:mm\\:ss})";

            // Turn off progress ring
            Model.BuildScriptFullProgressVisibility = Visibility.Visible;
            Model.WorkInProgress = false;

            // Build Ended, Switch to Normal View
            Model.SwitchNormalBuildInterface = true;
            DrawScript(CurMainTree.Script);
            */
        }

        private void ScriptSyntaxCheckCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Do not use await, let it run in background
            Model.StartSyntaxCheck(false);
        }

        private void ScriptOpenFolderCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Script sc = Model.CurMainTree.Script;
            if (sc.Type == ScriptType.Directory)
                MainViewModel.OpenFolder(sc.RealPath);
            else
                MainViewModel.OpenFolder(Path.GetDirectoryName(sc.RealPath));
        }

        #endregion

        #region TreeView Event Handler
        private void MainTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            KeyDown += MainTreeView_KeyDown;
        }

        private void MainTreeView_Unloaded(object sender, RoutedEventArgs e)
        {
            KeyDown -= MainTreeView_KeyDown;
        }

        /// <summary>
        /// Used to ensure pressing 'Space' to toggle TreeView's checkbox.
        /// </summary>
        private void MainTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space &&
                Keyboard.FocusedElement is FrameworkElement focusedElement &&
                focusedElement.DataContext is ProjectTreeItemModel node)
            {
                node.Checked = !node.Checked;
                e.Handled = true;
            }
        }

        private void MainTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is TreeView tree && tree.SelectedItem is ProjectTreeItemModel itemModel)
            {
                ProjectTreeItemModel item = Model.CurMainTree = itemModel;

                Dispatcher.Invoke(() =>
                {
                    Stopwatch watch = new Stopwatch();
                    watch.Start();
                    Model.DisplayScript(item.Script);
                    watch.Stop();
                    double msec = watch.Elapsed.TotalMilliseconds;
                    string filename = Path.GetFileName(Model.CurMainTree.Script.TreePath);
                    Model.StatusBarText = $"{filename} rendered ({msec:0}ms)";
                });
            }
        }

        private void MainTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem treeViewItem = ControlsHelper.VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                treeViewItem.IsSelected = true;
                e.Handled = true;
            }
        }
        #endregion

        #region Window Event Handler
        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            if (Engine.WorkingEngine != null)
                await Engine.WorkingEngine.ForceStopWait();

            if (0 < LogWindow.Count)
            {
                LogDialog.Close();
                LogDialog = null;
            }

            if (0 < UtilityWindow.Count)
            {
                UtilityDialog.Close();
                UtilityDialog = null;
            }

            if (0 < ScriptEditWindow.Count)
            {
                ScriptEditDialog.Close();
                ScriptEditDialog = null;
            }

            // TODO: Do this in more cleaner way
            while (Model.ScriptRefreshing != 0)
                await Task.Delay(500);
            while (ScriptCache.DbLock != 0)
                await Task.Delay(500);
            if (Model.ProjectsLoading != 0)
                await Task.Delay(500);

            Global.ScriptCache?.WaitClose();
            Logger.Db.Close();
        }

        private void BuildConOutRedirectListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!(sender is ListBox listBox))
                return;
            listBox.Items.MoveCurrentToLast();
            listBox.ScrollIntoView(listBox.Items.CurrentItem);
        }




        #endregion
    }
    #endregion

    #region MainViewCommands
    public static class MainViewCommands
    {
        #region Main Buttons
        public static readonly RoutedCommand ProjectBuildStartCommand = new RoutedUICommand("Build a Project", "ProjectBuildStart", typeof(MainViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F10),
            });
        public static readonly RoutedCommand ProjectBuildStopCommand = new RoutedUICommand("Stop Project Build", "ProjectBuildStop", typeof(MainViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F10),
            });
        public static readonly RoutedCommand ProjectRefreshCommand = new RoutedUICommand("Refresh Projects (F5)", "ProjectRefresh", typeof(MainViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F5),
            });
        public static readonly RoutedCommand SettingWindowCommand = new RoutedUICommand("Settings", "SettingWindow", typeof(MainViewCommands));
        public static readonly RoutedCommand LogWindowCommand = new RoutedUICommand("View Logs (Ctrl + L)", "LogWindow", typeof(MainViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.L, ModifierKeys.Control),
            });
        public static readonly RoutedCommand UtilityWindowCommand = new RoutedUICommand("Open Utilities", "UtilityWindow", typeof(MainViewCommands));
        public static readonly RoutedCommand ProjectUpdateCommand = new RoutedUICommand("Update Project", "ProjectUpdate", typeof(MainViewCommands));
        public static readonly RoutedCommand AboutWindowCommand = new RoutedUICommand("About PEBakery", "AboutWindow", typeof(MainViewCommands));
        #endregion

        #region Script Buttons and Context Menus
        public static readonly RoutedCommand ScriptRunCommand = new RoutedUICommand("Run Script", "ScriptRun", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptRefreshCommand = new RoutedUICommand("Refresh Script (Ctrl + F5)", "ScriptRefresh", typeof(MainViewCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F5, ModifierKeys.Control),
            });
        public static readonly RoutedCommand ScriptEditCommand = new RoutedUICommand("Edit Script", "ScriptEdit", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptInternalEditorCommand = new RoutedUICommand("Edit Script Properties", "ScriptInternalEditor", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptExternalEditorCommand = new RoutedUICommand("Edit Script Source", "ScriptExternalEditor", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptUpdateCommand = new RoutedUICommand("Update Script", "ScriptUpdate", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptSyntaxCheckCommand = new RoutedUICommand("Syntax Check", "ScriptSyntaxCheck", typeof(MainViewCommands));
        public static readonly RoutedCommand ScriptOpenFolderCommand = new RoutedUICommand("Open Script Folder", "ScriptOpenFolder", typeof(MainViewCommands));
        #endregion
    }
    #endregion

    #region Converters
    public class BuildConOutForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Brushes.Black;
            return (bool)value ? Brushes.Red : Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TaskBarProgressConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return (double)values[1] / (double)values[0];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ScriptLogoVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CodeValidatorResultIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(CodeValidator.Result))
                return null;

            PackIconMaterialKind icon;
            CodeValidator.Result result = (CodeValidator.Result)value;
            switch (result)
            {
                case CodeValidator.Result.Clean:
                    icon = PackIconMaterialKind.Check;
                    break;
                case CodeValidator.Result.Warning:
                    icon = PackIconMaterialKind.Alert;
                    break;
                case CodeValidator.Result.Error:
                    icon = PackIconMaterialKind.Close;
                    break;
                default:
                    icon = PackIconMaterialKind.Magnify;
                    break;
            }
            return icon;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CodeValidatorResultColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.GetType() != typeof(CodeValidator.Result))
                return null;

            Brush brush;
            CodeValidator.Result result = (CodeValidator.Result)value;
            switch (result)
            {
                case CodeValidator.Result.Clean:
                    brush = new SolidColorBrush(Colors.Green);
                    break;
                case CodeValidator.Result.Warning:
                    brush = new SolidColorBrush(Colors.OrangeRed);
                    break;
                case CodeValidator.Result.Error:
                    brush = new SolidColorBrush(Colors.Red);
                    break;
                default:
                    brush = new SolidColorBrush(Colors.Gray);
                    break;
            }
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    #endregion
}
