using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    public partial class Blame : Command
    {
        [GeneratedRegex(@"^\^?([0-9a-f]+)\s+(.*)\s+\((.*)\s+(\d+)\s+[\-\+]?\d+\s+\d+\) (.*)")]
        private static partial Regex REG_FORMAT();

        public Blame(string repo, string file, string revision, bool ignoreWhitespace)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;

            var builder = new StringBuilder();
            builder.Append("blame -f -t ");
            if (ignoreWhitespace)
                builder.Append("-w ");
            builder.Append(revision).Append(" -- ").Append(file.Quoted());

            Args = builder.ToString();
        }

        public async Task<Models.BlameData> ReadAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return new Models.BlameData();

            return ParseBlameOutput(rs.StdOut);
        }

        internal static Models.BlameData ParseBlameOutput(string output)
        {
            var result = new Models.BlameData();
            var content = new StringBuilder();
            var lastSHA = string.Empty;
            var needUnifyCommitSHA = false;
            var minSHALen = 64;

            var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains('\0'))
                {
                    result.IsBinary = true;
                    result.LineInfos.Clear();
                    break;
                }

                var match = REG_FORMAT().Match(line);
                if (!match.Success)
                    continue;

                content.AppendLine(match.Groups[5].Value);

                var commit = match.Groups[1].Value;
                var file = match.Groups[2].Value.Trim();
                var author = match.Groups[3].Value;
                var timestamp = ulong.Parse(match.Groups[4].Value);

                var info = new Models.BlameLineInfo()
                {
                    IsFirstInGroup = commit != lastSHA,
                    CommitSHA = commit,
                    File = file,
                    Author = author,
                    Timestamp = timestamp,
                };

                result.LineInfos.Add(info);
                lastSHA = commit;

                if (line[0] == '^')
                {
                    needUnifyCommitSHA = true;
                    minSHALen = Math.Min(minSHALen, commit.Length);
                }
            }

            if (needUnifyCommitSHA)
            {
                foreach (var line in result.LineInfos)
                {
                    if (line.CommitSHA.Length > minSHALen)
                        line.CommitSHA = line.CommitSHA.Substring(0, minSHALen);
                }
            }

            result.Content = content.ToString();
            return result;
        }
    }
}
