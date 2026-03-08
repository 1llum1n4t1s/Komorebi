using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    public partial class Diff : Command
    {
        [GeneratedRegex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@")]
        private static partial Regex REG_INDICATOR();

        [GeneratedRegex(@"^index\s([0-9a-f]{6,40})\.\.([0-9a-f]{6,40})(\s[1-9]{6})?")]
        private static partial Regex REG_HASH_CHANGE();

        private const string PREFIX_LFS_NEW = "+version https://git-lfs.github.com/spec/";
        private const string PREFIX_LFS_DEL = "-version https://git-lfs.github.com/spec/";
        private const string PREFIX_LFS_MODIFY = " version https://git-lfs.github.com/spec/";

        public Diff(string repo, Models.DiffOption opt, int unified, bool ignoreWhitespace)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(256);
            builder.Append("diff --no-color --no-ext-diff --patch ");
            if (Models.DiffOption.IgnoreCRAtEOL)
                builder.Append("--ignore-cr-at-eol ");
            if (ignoreWhitespace)
                builder.Append("--ignore-space-change ");
            builder.Append("--unified=").Append(unified).Append(' ');
            builder.Append(opt.ToString());

            Args = builder.ToString();
        }

        public async Task<Models.DiffResult> ReadAsync()
        {
            var result = new Models.DiffResult() { TextDiff = new Models.TextDiff() };

            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                var text = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                result = ParseDiffOutput(text);

                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore exceptions.
            }

            return result;
        }

        internal static Models.DiffResult ParseDiffOutput(string text)
        {
            var result = new Models.DiffResult() { TextDiff = new Models.TextDiff() };
            var deleted = new List<Models.TextDiffLine>();
            var added = new List<Models.TextDiffLine>();
            Models.TextDiffLine last = null;
            var oldLine = 0;
            var newLine = 0;

            var start = 0;
            var end = text.IndexOf('\n', start);
            while (end > 0)
            {
                var line = text[start..end];
                ParseLine(line, result, deleted, added, ref last, ref oldLine, ref newLine);

                start = end + 1;
                end = text.IndexOf('\n', start);
            }

            if (start < text.Length)
                ParseLine(text[start..], result, deleted, added, ref last, ref oldLine, ref newLine);

            if (result.IsBinary || result.IsLFS || result.TextDiff.Lines.Count == 0)
            {
                result.TextDiff = null;
            }
            else
            {
                FlushInlineHighlights(result, deleted, added);
                result.TextDiff.MaxLineNumber = Math.Max(newLine, oldLine);
            }

            return result;
        }

        private static void ParseLine(
            string line,
            Models.DiffResult result,
            List<Models.TextDiffLine> deleted,
            List<Models.TextDiffLine> added,
            ref Models.TextDiffLine last,
            ref int oldLine,
            ref int newLine)
        {
            if (result.IsBinary)
                return;

            if (line.StartsWith("old mode ", StringComparison.Ordinal))
            {
                result.OldMode = line.Substring(9);
                return;
            }

            if (line.StartsWith("new mode ", StringComparison.Ordinal))
            {
                result.NewMode = line.Substring(9);
                return;
            }

            if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
            {
                result.OldMode = line.Substring(18);
                return;
            }

            if (line.StartsWith("new file mode ", StringComparison.Ordinal))
            {
                result.NewMode = line.Substring(14);
                return;
            }

            if (result.IsLFS)
            {
                var ch = line[0];
                if (ch == '-')
                {
                    if (line.StartsWith("-oid sha256:", StringComparison.Ordinal))
                    {
                        result.LFSDiff.Old.Oid = line.Substring(12);
                    }
                    else if (line.StartsWith("-size ", StringComparison.Ordinal))
                    {
                        result.LFSDiff.Old.Size = long.Parse(line.AsSpan(6));
                    }
                }
                else if (ch == '+')
                {
                    if (line.StartsWith("+oid sha256:", StringComparison.Ordinal))
                    {
                        result.LFSDiff.New.Oid = line.Substring(12);
                    }
                    else if (line.StartsWith("+size ", StringComparison.Ordinal))
                    {
                        result.LFSDiff.New.Size = long.Parse(line.AsSpan(6));
                    }
                }
                else if (line.StartsWith(" size ", StringComparison.Ordinal))
                {
                    result.LFSDiff.New.Size = result.LFSDiff.Old.Size = long.Parse(line.AsSpan(6));
                }
                return;
            }

            if (result.TextDiff.Lines.Count == 0)
            {
                if (line.StartsWith("Binary", StringComparison.Ordinal))
                {
                    result.IsBinary = true;
                    return;
                }

                if (string.IsNullOrEmpty(result.OldHash))
                {
                    var match = REG_HASH_CHANGE().Match(line);
                    if (!match.Success)
                        return;

                    result.OldHash = match.Groups[1].Value;
                    result.NewHash = match.Groups[2].Value;
                }
                else
                {
                    var match = REG_INDICATOR().Match(line);
                    if (!match.Success)
                        return;

                    oldLine = int.Parse(match.Groups[1].Value);
                    newLine = int.Parse(match.Groups[2].Value);
                    last = new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, 0, 0);
                    result.TextDiff.Lines.Add(last);
                }
            }
            else
            {
                if (line.Length == 0)
                {
                    FlushInlineHighlights(result, deleted, added);
                    last = new Models.TextDiffLine(Models.TextDiffLineType.Normal, "", oldLine, newLine);
                    result.TextDiff.Lines.Add(last);
                    oldLine++;
                    newLine++;
                    return;
                }

                var ch = line[0];
                if (ch == '-')
                {
                    if (oldLine == 1 && newLine == 0 && line.StartsWith(PREFIX_LFS_DEL, StringComparison.Ordinal))
                    {
                        result.IsLFS = true;
                        result.LFSDiff = new Models.LFSDiff();
                        return;
                    }

                    last = new Models.TextDiffLine(Models.TextDiffLineType.Deleted, line.Substring(1), oldLine, 0);
                    deleted.Add(last);
                    oldLine++;
                }
                else if (ch == '+')
                {
                    if (oldLine == 0 && newLine == 1 && line.StartsWith(PREFIX_LFS_NEW, StringComparison.Ordinal))
                    {
                        result.IsLFS = true;
                        result.LFSDiff = new Models.LFSDiff();
                        return;
                    }

                    last = new Models.TextDiffLine(Models.TextDiffLineType.Added, line.Substring(1), 0, newLine);
                    added.Add(last);
                    newLine++;
                }
                else if (ch != '\\')
                {
                    FlushInlineHighlights(result, deleted, added);
                    var match = REG_INDICATOR().Match(line);
                    if (match.Success)
                    {
                        oldLine = int.Parse(match.Groups[1].Value);
                        newLine = int.Parse(match.Groups[2].Value);
                        last = new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, 0, 0);
                        result.TextDiff.Lines.Add(last);
                    }
                    else
                    {
                        if (oldLine == 1 && newLine == 1 && line.StartsWith(PREFIX_LFS_MODIFY, StringComparison.Ordinal))
                        {
                            result.IsLFS = true;
                            result.LFSDiff = new Models.LFSDiff();
                            return;
                        }

                        last = new Models.TextDiffLine(Models.TextDiffLineType.Normal, line.Substring(1), oldLine, newLine);
                        result.TextDiff.Lines.Add(last);
                        oldLine++;
                        newLine++;
                    }
                }
                else if (line.Equals("\\ No newline at end of file", StringComparison.Ordinal))
                {
                    last.NoNewLineEndOfFile = true;
                }
            }
        }

        private static void FlushInlineHighlights(
            Models.DiffResult result,
            List<Models.TextDiffLine> deleted,
            List<Models.TextDiffLine> added)
        {
            if (deleted.Count > 0)
            {
                if (added.Count == deleted.Count)
                {
                    for (int i = added.Count - 1; i >= 0; i--)
                    {
                        var left = deleted[i];
                        var right = added[i];

                        if (left.Content.Length > 1024 || right.Content.Length > 1024)
                            continue;

                        var chunks = Models.TextInlineChange.Compare(left.Content, right.Content);
                        if (chunks.Count > 4)
                            continue;

                        foreach (var chunk in chunks)
                        {
                            if (chunk.DeletedCount > 0)
                                left.Highlights.Add(new Models.TextRange(chunk.DeletedStart, chunk.DeletedCount));

                            if (chunk.AddedCount > 0)
                                right.Highlights.Add(new Models.TextRange(chunk.AddedStart, chunk.AddedCount));
                        }
                    }
                }

                result.TextDiff.Lines.AddRange(deleted);
                deleted.Clear();
            }

            if (added.Count > 0)
            {
                result.TextDiff.Lines.AddRange(added);
                added.Clear();
            }
        }
    }
}
