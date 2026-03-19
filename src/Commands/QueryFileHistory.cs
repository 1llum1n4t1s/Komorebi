using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    public class QueryFileHistory : Command
    {
        public QueryFileHistory(string repo, string path, string head)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;

            var builder = new StringBuilder();
            builder.Append("log --no-show-signature --date-order -n 10000 --decorate=no --format=\"@%H%x00%P%x00%aN±%aE%x00%at%x00%s\" --follow --name-status ");
            if (!string.IsNullOrEmpty(head))
                builder.Append(head).Append(" ");
            builder.Append("-- ").Append(path.Quoted());

            Args = builder.ToString();
        }

        public async Task<List<Models.FileVersion>> GetResultAsync()
        {
            var versions = new List<Models.FileVersion>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return versions;

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return versions;

            Models.FileVersion last = null;
            foreach (var line in lines)
            {
                if (line.StartsWith('@'))
                {
                    var parts = line.Split('\0');
                    if (parts.Length != 5)
                        continue;

                    last = new Models.FileVersion();
                    last.SHA = parts[0].Substring(1);
                    last.HasParent = !string.IsNullOrEmpty(parts[1]);
                    last.Author = Models.User.FindOrAdd(parts[2]);
                    last.AuthorTime = ulong.Parse(parts[3]);
                    last.Subject = parts[4];
                    versions.Add(last);
                }
                else if (last != null)
                {
                    // 基底クラスの共通パーサーを使用（旧: 25行の重複ロジック）
                    var parsed = ParseNameStatusLine(line);
                    if (parsed == null)
                        continue;

                    last.Change.Path = parsed.Value.path;
                    last.Change.Set(parsed.Value.state);
                }
            }

            return versions;
        }
    }
}
