using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    public class CompareRevisions : Command
    {
        public CompareRevisions(string repo, string start, string end)
        {
            WorkingDirectory = repo;
            Context = repo;

            var based = string.IsNullOrEmpty(start) ? "-R" : start;
            Args = $"diff --name-status {based} {end}";
        }

        public CompareRevisions(string repo, string start, string end, string path)
        {
            WorkingDirectory = repo;
            Context = repo;

            var based = string.IsNullOrEmpty(start) ? "-R" : start;
            Args = $"diff --name-status {based} {end} -- {path.Quoted()}";
        }

        public async Task<List<Models.Change>> ReadAsync()
        {
            var changes = new List<Models.Change>();
            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                // 基底クラスの共通パーサーを使用（旧: 30行の重複ロジック）
                while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    var parsed = ParseNameStatusLine(line);
                    if (parsed == null)
                        continue;

                    var change = new Models.Change() { Path = parsed.Value.path };
                    change.Set(parsed.Value.state);
                    changes.Add(change);
                }

                await proc.WaitForExitAsync().ConfigureAwait(false);

                changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
            }
            catch
            {
                //ignore changes;
            }

            return changes;
        }
    }
}
