using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    public class ScanRepositories : Popup
    {
        public bool UseCustomDir
        {
            get => _useCustomDir;
            set => SetProperty(ref _useCustomDir, value);
        }

        public string CustomDir
        {
            get => _customDir;
            set => SetProperty(ref _customDir, value);
        }

        public List<Models.ScanDir> ScanDirs
        {
            get;
        }

        public Models.ScanDir Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value, true);
        }

        public ScanRepositories()
        {
            ScanDirs = new List<Models.ScanDir>();

            var workspace = Preferences.Instance.GetActiveWorkspace();
            if (!string.IsNullOrEmpty(workspace.DefaultCloneDir))
                ScanDirs.Add(new Models.ScanDir(workspace.DefaultCloneDir, "Workspace"));

            if (!string.IsNullOrEmpty(Preferences.Instance.GitDefaultCloneDir))
                ScanDirs.Add(new Models.ScanDir(Preferences.Instance.GitDefaultCloneDir, "Global"));

            if (ScanDirs.Count > 0)
                _selected = ScanDirs[0];
            else
                _useCustomDir = true;

            GetManagedRepositories(Preferences.Instance.RepositoryNodes, _managed);
        }

        /// <summary>
        /// Scan a specific directory for git repositories and add them to the repository tree.
        /// This method runs without showing a popup dialog.
        /// </summary>
        public static async Task ScanDirectoryAsync(string rootDir)
        {
            if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir))
                return;

            if (!Preferences.Instance.IsGitConfigured())
                return;

            try
            {
                var managed = new HashSet<string>();
                GetManagedRepositories(Preferences.Instance.RepositoryNodes, managed);

                var rootDirInfo = new DirectoryInfo(rootDir);
                var found = new List<string>();

                await GetUnmanagedRepositoriesAsync(rootDirInfo, found, managed, new EnumerationOptions()
                {
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                    IgnoreInaccessible = true,
                });

                if (found.Count > 0)
                    await AddFoundRepositories(rootDirInfo, found);
            }
            catch (Exception ex)
            {
                App.RaiseException(null, $"Failed to scan repositories: {ex.Message}");
            }
        }

        public override async Task<bool> Sure()
        {
            string selectedDir;
            if (_useCustomDir)
            {
                if (string.IsNullOrEmpty(_customDir))
                {
                    App.RaiseException(null, "Missing root directory to scan!");
                    return false;
                }

                selectedDir = _customDir;
            }
            else
            {
                if (_selected == null || string.IsNullOrEmpty(_selected.Path))
                {
                    App.RaiseException(null, "Missing root directory to scan!");
                    return false;
                }

                selectedDir = _selected.Path;
            }

            if (!Directory.Exists(selectedDir))
                return true;

            ProgressDescription = $"Scan repositories under '{selectedDir}' ...";

            var minDelay = Task.Delay(500);
            var rootDir = new DirectoryInfo(selectedDir);
            var found = new List<string>();

            await GetUnmanagedRepositoriesAsync(rootDir, found, _managed, new EnumerationOptions()
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
            }, desc => ProgressDescription = desc);

            // Make sure this task takes at least 0.5s to avoid the popup panel disappearing too quickly.
            await minDelay;

            if (found.Count > 0)
                await AddFoundRepositories(rootDir, found);

            return true;
        }

        private static void GetManagedRepositories(List<RepositoryNode> group, HashSet<string> repos)
        {
            foreach (var node in group)
            {
                if (node.IsRepository)
                    repos.Add(node.Id);
                else
                    GetManagedRepositories(node.SubNodes, repos);
            }
        }

        private static async Task GetUnmanagedRepositoriesAsync(DirectoryInfo dir, List<string> outs, HashSet<string> managed, EnumerationOptions opts, Action<string> onProgress = null, int depth = 0)
        {
            var subdirs = dir.GetDirectories("*", opts);
            foreach (var subdir in subdirs)
            {
                if (subdir.Name.StartsWith(".", StringComparison.Ordinal) ||
                    subdir.Name.Equals("node_modules", StringComparison.Ordinal))
                    continue;

                onProgress?.Invoke($"Scanning {subdir.FullName}...");

                var normalizedSelf = subdir.FullName.Replace('\\', '/').TrimEnd('/');
                if (managed.Contains(normalizedSelf))
                    continue;

                var gitDir = Path.Combine(subdir.FullName, ".git");
                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    var test = await new Commands.QueryRepositoryRootPath(subdir.FullName).GetResultAsync();
                    if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                    {
                        var normalized = test.StdOut.Trim().Replace('\\', '/').TrimEnd('/');
                        if (!managed.Contains(normalized))
                            outs.Add(normalized);
                    }

                    continue;
                }

                var isBare = await new Commands.IsBareRepository(subdir.FullName).GetResultAsync();
                if (isBare)
                {
                    outs.Add(normalizedSelf);
                    continue;
                }

                if (depth < 5)
                    await GetUnmanagedRepositoriesAsync(subdir, outs, managed, opts, onProgress, depth + 1);
            }
        }

        private static async Task AddFoundRepositories(DirectoryInfo rootDir, List<string> found)
        {
            var normalizedRoot = rootDir.FullName.Replace('\\', '/').TrimEnd('/');
            foreach (var f in found)
            {
                var parent = new DirectoryInfo(f).Parent!.FullName.Replace('\\', '/').TrimEnd('/');
                if (parent.Equals(normalizedRoot, StringComparison.Ordinal))
                {
                    var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(f, null, false, false);
                    await node.UpdateStatusAsync(false, null);
                }
                else if (parent.StartsWith(normalizedRoot, StringComparison.Ordinal))
                {
                    var relative = parent.Substring(normalizedRoot.Length).TrimStart('/');
                    var group = FindOrCreateGroupRecursive(Preferences.Instance.RepositoryNodes, relative);
                    var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(f, group, false, false);
                    await node.UpdateStatusAsync(false, null);
                }
            }

            Preferences.Instance.AutoRemoveInvalidNode();
            Preferences.Instance.Save();
            Welcome.Instance.Refresh();
        }

        private static RepositoryNode FindOrCreateGroupRecursive(List<RepositoryNode> collection, string path)
        {
            RepositoryNode node = null;
            foreach (var name in path.Split('/'))
            {
                node = FindOrCreateGroup(collection, name);
                collection = node.SubNodes;
            }

            return node;
        }

        private static RepositoryNode FindOrCreateGroup(List<RepositoryNode> collection, string name)
        {
            foreach (var node in collection)
            {
                if (node.Name.Equals(name, StringComparison.Ordinal))
                    return node;
            }

            var added = new RepositoryNode()
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                IsRepository = false,
                IsExpanded = true,
            };
            collection.Add(added);

            Preferences.Instance.SortNodes(collection);
            return added;
        }

        private HashSet<string> _managed = new();
        private bool _useCustomDir = false;
        private string _customDir = string.Empty;
        private Models.ScanDir _selected = null;
    }
}
