using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Komorebi.Models;

/// <summary>
/// コミットメッセージテンプレートエンジン。
/// ${branch_name}、${files}、${pure_files}、${files_num}などの変数置換、
/// ${files:N}形式のスライス、${name/正規表現/置換}形式の正規表現置換をサポートする。
/// </summary>
public class TemplateEngine
{
    /// <summary>テンプレート評価時のコンテキスト（ブランチと変更ファイルリスト）</summary>
    private class Context(Branch branch, IReadOnlyList<Change> changes)
    {
        public Branch branch = branch;
        public IReadOnlyList<Change> changes = changes;
    }

    /// <summary>テキストリテラルトークン</summary>
    private class Text(string text)
    {
        public string text = text;
    }

    /// <summary>変数トークン（${name}形式）</summary>
    private class Variable(string name)
    {
        public string name = name;
    }

    /// <summary>スライス付き変数トークン（${name:N}形式）</summary>
    private class SlicedVariable(string name, int count)
    {
        public string name = name;
        public int count = count;
    }

    /// <summary>正規表現置換付き変数トークン（${name/正規表現/置換}形式）</summary>
    private class RegexVariable(string name, Regex regex, string replacement)
    {
        public string name = name;
        public Regex regex = regex;
        public string replacement = replacement;
    }

    // パーサー用の定数
    private const char ESCAPE = '\\';
    private const char VARIABLE_ANCHOR = '$';
    private const char VARIABLE_START = '{';
    private const char VARIABLE_END = '}';
    private const char VARIABLE_SLICE = ':';
    private const char VARIABLE_REGEX = '/';
    private const char NEWLINE = '\n';
    private const RegexOptions REGEX_OPTIONS = RegexOptions.Singleline | RegexOptions.IgnoreCase;
    private static readonly TimeSpan s_regexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// テンプレート文字列を評価し、変数を展開した結果を返す
    /// </summary>
    /// <param name="text">テンプレート文字列</param>
    /// <param name="branch">現在のブランチ</param>
    /// <param name="changes">ステージされた変更ファイルリスト</param>
    /// <returns>変数が展開された文字列</returns>
    public string Eval(string text, Branch branch, IReadOnlyList<Change> changes)
    {
        Reset();

        _chars = text.ToCharArray();
        Parse();

        var context = new Context(branch, changes);
        var sb = new StringBuilder();
        sb.EnsureCapacity(text.Length);
        foreach (var token in _tokens)
        {
            switch (token)
            {
                case Text text_token:
                    sb.Append(text_token.text);
                    break;
                case Variable var_token:
                    sb.Append(EvalVariable(context, var_token));
                    break;
                case SlicedVariable sliced_var:
                    sb.Append(EvalVariable(context, sliced_var));
                    break;
                case RegexVariable regex_var:
                    sb.Append(EvalVariable(context, regex_var));
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>パーサーの状態をリセットする</summary>
    private void Reset()
    {
        _pos = 0;
        _chars = [];
        _tokens.Clear();
    }

    /// <summary>次の文字を読み進めて返す。末尾の場合はnull。</summary>
    private char? Next()
    {
        var c = Peek();
        if (c is not null)
            _pos++;
        return c;
    }

    /// <summary>次の文字を先読みする（位置は進めない）。末尾の場合はnull。</summary>
    private char? Peek()
    {
        return (_pos >= _chars.Length) ? null : _chars[_pos];
    }

    /// <summary>現在位置から整数をパースする。数字がない場合はnull。</summary>
    private int? Integer()
    {
        var start = _pos;
        while (Peek() is >= '0' and <= '9')
        {
            _pos++;
        }
        if (start >= _pos)
            return null;

        var chars = new ReadOnlySpan<char>(_chars, start, _pos - start);
        return int.Parse(chars);
    }

    /// <summary>
    /// テンプレート文字列全体をパースし、テキストトークンと変数トークンのリストを構築する
    /// </summary>
    private void Parse()
    {
        // テキストトークンの開始位置
        var tok = _pos;
        bool esc = false;
        while (Next() is { } c)
        {
            if (esc)
            {
                esc = false;
                continue;
            }
            switch (c)
            {
                case ESCAPE:
                    // エスケープ対象は「\」と「$」のみ
                    if (Peek() is ESCAPE or VARIABLE_ANCHOR)
                    {
                        esc = true;
                        FlushText(tok, _pos - 1);
                        tok = _pos;
                    }
                    break;
                case VARIABLE_ANCHOR:
                    // 位置をバックアップして変数のパースを試みる
                    var bak = _pos;
                    var variable = TryParseVariable();
                    if (variable is null)
                    {
                        // 変数が見つからなかった場合、位置をロールバック
                        _pos = bak;
                    }
                    else
                    {
                        // 変数が見つかった場合、直前のテキストトークンをフラッシュ
                        FlushText(tok, bak - 1);
                        _tokens.Add(variable);
                        tok = _pos;
                    }
                    break;
            }
        }
        // 残りのテキストトークンをフラッシュ
        FlushText(tok, _pos);
    }

    /// <summary>指定範囲のテキストをトークンリストに追加する</summary>
    private void FlushText(int start, int end)
    {
        int len = end - start;
        if (len <= 0)
            return;
        var text = new string(_chars, start, len);
        _tokens.Add(new Text(text));
    }

    /// <summary>${...}形式の変数をパースする。失敗時はnullを返す。</summary>
    private object TryParseVariable()
    {
        if (Next() != VARIABLE_START)
            return null;
        var nameStart = _pos;
        while (Next() is { } c)
        {
            // name character, continue advancing
            if (IsNameChar(c))
                continue;

            var nameEnd = _pos - 1;
            // not a name character but name is empty, cancel
            if (nameStart >= nameEnd)
                return null;
            var name = new string(_chars, nameStart, nameEnd - nameStart);

            return c switch
            {
                // 通常の変数 ${name}
                VARIABLE_END => new Variable(name),
                // スライス付き変数 ${name:N}
                VARIABLE_SLICE => TryParseSlicedVariable(name),
                // 正規表現付き変数 ${name/regex/replacement}
                VARIABLE_REGEX => TryParseRegexVariable(name),
                _ => null,
            };
        }

        return null;
    }

    /// <summary>スライス付き変数（${name:N}形式）をパースする</summary>
    private object TryParseSlicedVariable(string name)
    {
        int? n = Integer();
        if (n is null)
            return null;
        if (Next() != VARIABLE_END)
            return null;

        return new SlicedVariable(name, (int)n);
    }

    /// <summary>正規表現付き変数（${name/regex/replacement}形式）をパースする</summary>
    private object TryParseRegexVariable(string name)
    {
        var regex = ParseRegex();
        if (regex is null)
            return null;
        var replacement = ParseReplacement();
        if (replacement is null)
            return null;

        return new RegexVariable(name, regex, replacement);
    }

    /// <summary>正規表現パターン部分をパースする。「/」で終端。</summary>
    private Regex ParseRegex()
    {
        var sb = new StringBuilder();
        var tok = _pos;
        var esc = false;
        var found = false;
        while (Next() is { } c)
        {
            if (esc)
            {
                esc = false;
                continue;
            }
            switch (c)
            {
                case ESCAPE:
                    // 正規表現内では「/」のみエスケープ可能（「\」や「{」は正規表現で頻用されるため）
                    if (Peek() == VARIABLE_REGEX)
                    {
                        esc = true;
                        sb.Append(_chars, tok, _pos - 1 - tok);
                        tok = _pos;
                    }
                    break;
                case VARIABLE_REGEX:
                    found = true;
                    goto Loop_exit;
                case NEWLINE:
                    // 改行は許可されない
                    return null;
            }
        }
    Loop_exit:
        // 終端デリミタが見つからずにEOFに達した場合は不正な構文
        if (!found)
            return null;

        sb.Append(_chars, tok, _pos - 1 - tok);

        try
        {
            var pattern = sb.ToString();
            if (pattern.Length == 0)
                return null;
            var regex = new Regex(pattern, REGEX_OPTIONS, s_regexTimeout);

            return regex;
        }
        catch (RegexParseException)
        {
            return null;
        }
    }

    /// <summary>正規表現の置換文字列部分をパースする。「}」で終端。</summary>
    private string ParseReplacement()
    {
        var sb = new StringBuilder();
        var tok = _pos;
        var esc = false;
        var found = false;
        while (Next() is { } c)
        {
            if (esc)
            {
                esc = false;
                continue;
            }
            switch (c)
            {
                case ESCAPE:
                    // 閉じ波括弧のみエスケープ可能
                    if (Peek() == VARIABLE_END)
                    {
                        esc = true;
                        sb.Append(_chars, tok, _pos - 1 - tok);
                        tok = _pos;
                    }
                    break;
                case VARIABLE_END:
                    found = true;
                    goto Loop_exit;
                case NEWLINE:
                    // no newlines allowed
                    return null;
            }
        }
    Loop_exit:
        // 終端デリミタが見つからずにEOFに達した場合は不正な構文
        if (!found)
            return null;

        sb.Append(_chars, tok, _pos - 1 - tok);

        var replacement = sb.ToString();

        return replacement;
    }

    /// <summary>変数名に使用できる文字かどうかを判定する（英数字とアンダースコア）</summary>
    private static bool IsNameChar(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
    }

    /// <summary>名前から変数を評価し結果文字列を返す</summary>
    private static string EvalVariable(Context context, string name)
    {
        if (!s_variables.TryGetValue(name, out var getter))
            return string.Empty;
        return getter(context);
    }

    /// <summary>通常変数トークンを評価する</summary>
    private static string EvalVariable(Context context, Variable variable)
    {
        return EvalVariable(context, variable.name);
    }

    /// <summary>スライス付き変数トークンを評価する</summary>
    private static string EvalVariable(Context context, SlicedVariable variable)
    {
        if (!s_slicedVariables.TryGetValue(variable.name, out var getter))
            return string.Empty;
        return getter(context, variable.count);
    }

    /// <summary>正規表現付き変数トークンを評価する</summary>
    private static string EvalVariable(Context context, RegexVariable variable)
    {
        var str = EvalVariable(context, variable.name);
        if (string.IsNullOrEmpty(str))
            return str;

        try
        {
            return variable.regex.Replace(str, variable.replacement);
        }
        catch (RegexMatchTimeoutException)
        {
            // ReDoS対策：タイムアウト時は元の文字列をそのまま返す
            return str;
        }
    }

    /// <summary>現在のパーサー位置</summary>
    private int _pos = 0;
    /// <summary>パース対象の文字配列</summary>
    private char[] _chars = [];
    /// <summary>パース結果のトークンリスト</summary>
    private readonly List<object> _tokens = [];

    /// <summary>コンテキストから変数値を取得するデリゲート</summary>
    private delegate string VariableGetter(Context context);

    /// <summary>サポートされる変数名とゲッターのマップ</summary>
    private static readonly IReadOnlyDictionary<string, VariableGetter> s_variables = new Dictionary<string, VariableGetter>() {
        {"branch_name", GetBranchName},
        {"files_num", GetFilesCount},
        {"files", GetFiles},
        {"pure_files", GetPureFiles},
    };

    /// <summary>現在のブランチ名を取得する</summary>
    private static string GetBranchName(Context context)
    {
        return context.branch.Name;
    }

    /// <summary>変更ファイル数を文字列で取得する</summary>
    private static string GetFilesCount(Context context)
    {
        return context.changes.Count.ToString();
    }

    /// <summary>変更ファイルのフルパスをカンマ区切りで取得する</summary>
    private static string GetFiles(Context context)
    {
        List<string> paths = [];
        foreach (var c in context.changes)
            paths.Add(c.Path);
        return string.Join(", ", paths);
    }

    /// <summary>変更ファイルのファイル名のみをカンマ区切りで取得する</summary>
    private static string GetPureFiles(Context context)
    {
        List<string> names = [];
        foreach (var c in context.changes)
            names.Add(Path.GetFileName(c.Path));
        return string.Join(", ", names);
    }

    /// <summary>スライス付き変数用のデリゲート</summary>
    private delegate string VariableSliceGetter(Context context, int count);

    /// <summary>スライス対応変数名とゲッターのマップ</summary>
    private static readonly IReadOnlyDictionary<string, VariableSliceGetter> s_slicedVariables = new Dictionary<string, VariableSliceGetter>() {
        {"files", GetFilesSliced},
        {"pure_files", GetPureFilesSliced}
    };

    /// <summary>変更ファイルのフルパスを指定件数まで取得し、残りは「and N other files」で表示</summary>
    private static string GetFilesSliced(Context context, int count)
    {
        var sb = new StringBuilder();
        List<string> paths = [];
        var max = Math.Min(count, context.changes.Count);
        for (int i = 0; i < max; i++)
            paths.Add(context.changes[i].Path);

        sb.AppendJoin(", ", paths);
        if (max < context.changes.Count)
            sb.Append($" and {context.changes.Count - max} other files");

        return sb.ToString();
    }

    /// <summary>変更ファイルのファイル名のみを指定件数まで取得し、残りは「and N other files」で表示</summary>
    private static string GetPureFilesSliced(Context context, int count)
    {
        var sb = new StringBuilder();
        List<string> names = [];
        var max = Math.Min(count, context.changes.Count);
        for (int i = 0; i < max; i++)
            names.Add(Path.GetFileName(context.changes[i].Path));

        sb.AppendJoin(", ", names);
        if (max < context.changes.Count)
            sb.Append($" and {context.changes.Count - max} other files");

        return sb.ToString();
    }
}
