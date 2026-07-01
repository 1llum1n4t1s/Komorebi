using System.Collections.Concurrent;

namespace Komorebi.Models;

/// <summary>
/// Gitユーザー情報（名前とメールアドレス）を保持するクラス。
/// スレッドセーフなキャッシュにより、同一ユーザーの重複インスタンスを防止する。
/// </summary>
public class User
{
    /// <summary>無効なユーザーを表すシングルトンインスタンス</summary>
    public static readonly User Invalid = new User(string.Empty);

    /// <summary>ユーザー名</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>メールアドレス</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 「名前±メールアドレス」形式の文字列からユーザーを生成する
    /// </summary>
    /// <param name="data">「名前±メールアドレス」形式の文字列</param>
    public User(string data)
    {
        // null/空文字列を安全に処理する
        if (string.IsNullOrEmpty(data))
            return;

        // 「±」区切りで名前とメールアドレスを分離
        // 全gitコマンドの出力が「$NAME±$EMAIL」形式（メールに山括弧なし）に統一されたため、
        // 山括弧の除去は不要（taggeremail は :trim 指定で取得する）
        var parts = data.Split('±', 2);
        if (parts.Length < 2)
        {
            Email = data;
        }
        else
        {
            Name = parts[0];
            Email = parts[1];
        }
    }

    /// <summary>
    /// キャッシュからユーザーを検索するか、新規作成して追加する（スレッドセーフ）
    /// </summary>
    /// <param name="data">「名前±メールアドレス」形式の文字列</param>
    /// <returns>対応するUserインスタンス</returns>
    public static User FindOrAdd(string data)
    {
        return _caches.GetOrAdd(data, key => new User(key));
    }

    /// <summary>「名前 &lt;メールアドレス&gt;」形式の文字列表現を返す</summary>
    public override string ToString()
    {
        return $"{Name} <{Email}>";
    }

    /// <summary>ユーザーインスタンスのスレッドセーフなキャッシュ</summary>
    private static ConcurrentDictionary<string, User> _caches = new ConcurrentDictionary<string, User>();
}
