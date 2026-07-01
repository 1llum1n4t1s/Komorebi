using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Komorebi.AI;

/// <summary>
/// AI コミットメッセージ生成のエントリポイント。
/// プロバイダ毎の <see cref="IGenerationStrategy"/> 実装に委譲する Facade。
/// 新規プロバイダ追加時はこの switch に分岐を 1 行追加し、対応する Strategy クラスを実装するだけで済む
/// （以前は 1 メソッドに 2 経路が混在し、肥大化していた）。
/// </summary>
public class Agent(Service service)
{
    public Task GenerateCommitMessageAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation)
    {
        var strategy = CreateStrategy();
        return strategy.GenerateCommitMessageAsync(repo, changeList, onUpdate, cancellation);
    }

    private IGenerationStrategy CreateStrategy() => service.Provider switch
    {
        Provider.Anthropic => new AnthropicHttpStrategy(service),
        _ => new OpenAISdkStrategy(service),
    };

    /// <summary>
    /// すべての Strategy で共通利用するユーザーメッセージのプロンプト構築。
    /// プロンプト変更時はここ 1 箇所を直せば全プロバイダに反映される。
    /// </summary>
    internal static string BuildUserMessage(Service service, string repo, string changeList)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Generate a commit message (follow the rule of conventional commit message) for given git repository.")
          .AppendLine("- Read all given changed files before generating. Only binary files (such as images, audios ...) can be skipped.")
          .AppendLine("- Output the conventional commit message (with detail changes in list) directly. Do not explain your output nor introduce your answer.");

        if (!string.IsNullOrEmpty(service.AdditionalPrompt))
            sb.AppendLine(service.AdditionalPrompt);

        sb.Append("Repository path: ").AppendLine(repo.Quoted())
          .AppendLine("Changed files ('A' means added, 'M' means modified, 'D' means deleted, 'T' means type changed, 'R' means renamed, 'C' means copied): ")
          .Append(changeList);

        return sb.ToString();
    }

    /// <summary>
    /// AI 応答全体を囲む Markdown コードフェンス (```) を取り除く（upstream 39fdc1af）。
    /// 一部のモデルはコミットメッセージ全体をコードブロックとして返すため、
    /// そのままコミットメッセージ欄へ貼り付けられる形に整形する。
    /// 除去した結果が空になった場合は空文字列を返す（呼び出し側でフォールバック表示する）。
    /// </summary>
    internal static string TrimCodeFence(string rawText)
    {
        var text = rawText.ReplaceLineEndings("\n").Trim();
        var start = 0;
        var len = text.Length;
        if (text.StartsWith("```\n", StringComparison.Ordinal))
        {
            start += 4;
            len -= 4;
        }

        if (text.EndsWith("\n```", StringComparison.Ordinal))
            len -= 4;

        return len > 0 ? text.Substring(start, len) : string.Empty;
    }
}
