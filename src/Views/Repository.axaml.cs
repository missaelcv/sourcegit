using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Repository : UserControl
    {
        public Repository()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            UpdateLeftSidebarLayout();
        }

        private void OpenWithExternalTools(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForExternalTools();
                button.OpenContextMenu(menu);
                e.Handled = true;
            }
        }

        private void OpenGitFlowMenu(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForGitFlow();
                (sender as Control)?.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OpenGitLFSMenu(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForGitLFS();
                (sender as Control)?.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private async void OpenStatistics(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo && TopLevel.GetTopLevel(this) is Window owner)
            {
                var dialog = new Statistics() { DataContext = new ViewModels.Statistics(repo.FullPath) };
                await dialog.ShowDialog(owner);
                e.Handled = true;
            }
        }

        private void OnSearchCommitPanelPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsVisibleProperty && sender is Grid { IsVisible: true})
                txtSearchCommitsBox.Focus();
        }

        private void OnSearchKeyDown(object _, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is ViewModels.Repository repo)
                    repo.StartSearchCommits();

                e.Handled = true;
            }
        }

        private void OnSearchResultDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: Models.Commit commit } && DataContext is ViewModels.Repository repo)
            {
                repo.NavigateToCommit(commit.SHA);
            }
            
            e.Handled = true;
        }

        private void OnBranchTreeRowsChanged(object _, RoutedEventArgs e)
        {
            UpdateLeftSidebarLayout();
            e.Handled = true;
        }

        private void OnLocalBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
        {
            remoteBranchTree.UnselectAll();
            tagsList.SelectedItem = null;
        }
        
        private void OnRemoteBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
        {
            localBranchTree.UnselectAll();
            tagsList.SelectedItem = null;
        }

        private void OnTagDataGridSelectionChanged(object sender, SelectionChangedEventArgs _)
        {
            if (sender is DataGrid { SelectedItem: Models.Tag tag })
            {
                localBranchTree.UnselectAll();
                remoteBranchTree.UnselectAll();

                if (DataContext is ViewModels.Repository repo)
                    repo.NavigateToCommit(tag.SHA);
            }
        }

        private void OnTagContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null && DataContext is ViewModels.Repository repo)
            {
                var tag = datagrid.SelectedItem as Models.Tag;
                var menu = repo.CreateContextMenuForTag(tag);
                datagrid.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnToggleTagFilter(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton { DataContext: Models.Tag tag } toggle && DataContext is ViewModels.Repository repo)
            {
                repo.UpdateFilter(tag.Name, toggle.IsChecked == true);
            }

            e.Handled = true;
        }

        private void OnSubmoduleContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null && DataContext is ViewModels.Repository repo)
            {
                var submodule = datagrid.SelectedItem as string;
                var menu = repo.CreateContextMenuForSubmodule(submodule);
                datagrid.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnDoubleTappedSubmodule(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: not null } grid && DataContext is ViewModels.Repository repo)
            {
                var submodule = grid.SelectedItem as string;
                repo.OpenSubmodule(submodule);
            }

            e.Handled = true;
        }

        private void OnWorktreeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: not null } grid && DataContext is ViewModels.Repository repo)
            {
                var worktree = grid.SelectedItem as Models.Worktree;
                var menu = repo.CreateContextMenuForWorktree(worktree);
                grid.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnDoubleTappedWorktree(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: not null } grid && DataContext is ViewModels.Repository repo)
            {
                var worktree = grid.SelectedItem as Models.Worktree;
                repo.OpenWorktree(worktree);
            }

            e.Handled = true;
        }

        private void OnLeftSidebarDataGridPropertyChanged(object _, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DataGrid.ItemsSourceProperty || e.Property == DataGrid.IsVisibleProperty)
            {
                UpdateLeftSidebarLayout();
            }
        }

        private void UpdateLeftSidebarLayout()
        {
            var vm = DataContext as ViewModels.Repository;
            if (vm == null || vm.Settings == null)
                return;

            if (!IsLoaded)
                return;

            var leftHeight = leftSidebarGroups.Bounds.Height - 28.0 * 5;
            var localBranchRows = vm.IsLocalBranchGroupExpanded ? localBranchTree.Rows.Count : 0;
            var remoteBranchRows = vm.IsRemoteGroupExpanded ? remoteBranchTree.Rows.Count : 0;
            var desiredBranches = (localBranchRows + remoteBranchRows) * 24.0;
            var desiredTag = vm.IsTagGroupExpanded ? tagsList.RowHeight * vm.VisibleTags.Count : 0;
            var desiredSubmodule = vm.IsSubmoduleGroupExpanded ? submoduleList.RowHeight * vm.Submodules.Count : 0;
            var desiredWorktree = vm.IsWorktreeGroupExpanded ? worktreeList.RowHeight * vm.Worktrees.Count : 0;
            var desiredOthers = desiredTag + desiredSubmodule + desiredWorktree;
            var hasOverflow = (desiredBranches + desiredOthers > leftHeight);

            if (vm.IsTagGroupExpanded)
            {
                var height = desiredTag;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches - desiredSubmodule - desiredWorktree;
                    if (test < 0)
                        height = Math.Min(200, height);
                    else
                        height = Math.Max(200, test);
                }

                leftHeight -= height;
                tagsList.Height = height;
                hasOverflow = (desiredBranches + desiredSubmodule + desiredWorktree) > leftHeight;
            }

            if (vm.IsSubmoduleGroupExpanded)
            {
                var height = desiredSubmodule;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches - desiredWorktree;
                    if (test < 0)
                        height = Math.Min(200, height);
                    else
                        height = Math.Max(200, test);
                }

                leftHeight -= height;
                submoduleList.Height = height;
                hasOverflow = (desiredBranches + desiredWorktree) > leftHeight;
            }

            if (vm.IsWorktreeGroupExpanded)
            {
                var height = desiredWorktree;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches;
                    if (test < 0)
                        height = Math.Min(200, height);
                    else
                        height = Math.Max(200, test);
                }

                leftHeight -= height;
                worktreeList.Height = height;
            }

            if (desiredBranches > leftHeight)
            {
                var local = localBranchRows * 24.0;
                var remote = remoteBranchRows * 24.0;
                var half = leftHeight / 2;
                if (vm.IsLocalBranchGroupExpanded)
                {
                    if (vm.IsRemoteGroupExpanded)
                    {
                        if (local < half)
                        {
                            localBranchTree.Height = local;
                            remoteBranchTree.Height = leftHeight - local;
                        }
                        else if (remote < half)
                        {
                            remoteBranchTree.Height = remote;
                            localBranchTree.Height = leftHeight - remote;
                        }
                        else
                        {
                            localBranchTree.Height = half;
                            remoteBranchTree.Height = half;
                        }
                    }
                    else
                    {
                        localBranchTree.Height = leftHeight;
                    }
                }
                else if (vm.IsRemoteGroupExpanded)
                {
                    remoteBranchTree.Height = leftHeight;
                }
            }
            else
            {
                if (vm.IsLocalBranchGroupExpanded)
                {
                    var height = localBranchRows * 24;
                    localBranchTree.Height = height;
                }

                if (vm.IsRemoteGroupExpanded)
                {
                    var height = remoteBranchRows * 24;
                    remoteBranchTree.Height = height;
                }
            }

            leftSidebarGroups.InvalidateMeasure();
        }
    }
}
