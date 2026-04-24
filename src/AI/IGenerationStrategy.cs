using System;
using System.Threading;
using System.Threading.Tasks;

namespace Komorebi.AI;

/// <summary>
/// AI プロバイダごとの生成戦略を抽象化する。
/// OpenAI 互換 SDK 経路 (OpenAI / Azure OpenAI / Gemini) と
/// Anthropic raw HTTP 経路を同一インターフェースの裏に隠蔽し、
/// 新規プロバイダ追加時の <see cref="Agent"/> 肥大化を防ぐ。
/// </summary>
public interface IGenerationStrategy
{
    /// <summary>
    /// 指定リポジトリの変更ファイル一覧から conventional commit メッセージを生成する。
    /// 結果は <paramref name="onUpdate"/> を通じてストリーム的に通知される。
    /// </summary>
    /// <param name="repo">対象リポジトリの絶対パス</param>
    /// <param name="changeList">変更ファイル一覧（git status --porcelain 相当）</param>
    /// <param name="onUpdate">部分結果のコールバック</param>
    /// <param name="cancellation">キャンセルトークン</param>
    Task GenerateCommitMessageAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation);
}
