using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using Avalonia.Controls;
using Avalonia.Media;

namespace Komorebi;

/// <summary>
/// <see cref="DateTime"/>のJSON変換コンバーター。
/// ISO 8601形式（yyyy-MM-ddTHH:mm:ssZ）でUTC保存し、読み込み時はローカル時刻に変換する。
/// </summary>
public class DateTimeConverter : JsonConverter<DateTime>
{
    /// <summary>
    /// JSONからDateTimeを読み取る。UTC文字列をパースしてローカル時刻として返す。
    /// </summary>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // UTC形式の文字列をパースし、AssumeUniversalでUTCとして解釈後、ローカル時刻に変換
        return DateTime.ParseExact(reader.GetString(), FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToLocalTime();
    }

    /// <summary>
    /// DateTimeをJSONに書き込む。ローカル時刻をUTCに変換してISO 8601形式で出力する。
    /// </summary>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // 保存時はUTCに統一し、タイムゾーン差異による不整合を防ぐ
        writer.WriteStringValue(value.ToUniversalTime().ToString(FORMAT));
    }

    /// <summary>
    /// 日時のシリアライズフォーマット（ISO 8601 UTC）。
    /// </summary>
    private const string FORMAT = "yyyy-MM-ddTHH:mm:ssZ";
}

/// <summary>
/// Avalonia <see cref="Color"/>のJSON変換コンバーター。
/// 色を文字列表現（例: "#FF0000FF"）でシリアライズ/デシリアライズする。
/// </summary>
public class ColorConverter : JsonConverter<Color>
{
    /// <summary>
    /// JSON文字列からAvaloniaのColorを復元する。
    /// </summary>
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Color.Parse(reader.GetString());
    }

    /// <summary>
    /// AvaloniaのColorをJSON文字列に変換する。
    /// </summary>
    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Avalonia <see cref="GridLength"/>のJSON変換コンバーター。
/// ピクセル値のdoubleとしてシリアライズし、読み込み時はPixel単位のGridLengthに復元する。
/// </summary>
public class GridLengthConverter : JsonConverter<GridLength>
{
    /// <summary>
    /// JSON数値からPixel単位のGridLengthを生成する。
    /// </summary>
    public override GridLength Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var size = reader.GetDouble();
        // StarやAutoではなく、常にPixel単位で復元する（レイアウト状態の永続化用途）
        return new GridLength(size, GridUnitType.Pixel);
    }

    /// <summary>
    /// GridLengthのピクセル値をJSON数値として出力する。
    /// </summary>
    public override void Write(Utf8JsonWriter writer, GridLength value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}

/// <summary>
/// Avalonia DataGridLengthのJSON変換コンバーター。
/// DataGridの列幅をピクセル値で保存・復元する。
/// </summary>
public class DataGridLengthConverter : JsonConverter<DataGridLength>
{
    /// <summary>
    /// JSON数値からPixel単位のDataGridLengthを生成する。
    /// DesiredValueを0、DisplayValueをsizeに設定し、固定幅として復元する。
    /// </summary>
    public override DataGridLength Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var size = reader.GetDouble();
        // 第3引数(desiredValue)=0, 第4引数(displayValue)=sizeで固定ピクセル幅として復元
        return new DataGridLength(size, DataGridLengthUnitType.Pixel, 0, size);
    }

    /// <summary>
    /// DataGridLengthの実際の表示幅をJSON数値として出力する。
    /// </summary>
    public override void Write(Utf8JsonWriter writer, DataGridLength value, JsonSerializerOptions options)
    {
        // Valueではなく実際の表示サイズ(DisplayValue)を保存する
        writer.WriteNumberValue(value.DisplayValue);
    }
}

/// <summary>
/// System.Text.Jsonのソース生成を使用したJSONシリアライズコンテキスト。
/// AOTコンパイル時にリフレクションを使わずにシリアライズを行うため、
/// アプリ内で永続化が必要な全モデル型をここに登録する。
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    IgnoreReadOnlyFields = true,
    IgnoreReadOnlyProperties = true,
    Converters = [
        typeof(DateTimeConverter),
        typeof(ColorConverter),
        typeof(GridLengthConverter),
        typeof(DataGridLengthConverter),
    ]
)]
[JsonSerializable(typeof(Models.ExternalToolCustomization))]
[JsonSerializable(typeof(Models.InteractiveRebaseJobCollection))]
[JsonSerializable(typeof(Models.JetBrainsState))]
[JsonSerializable(typeof(Models.ThemeOverrides))]
[JsonSerializable(typeof(Models.RepositorySettings))]
[JsonSerializable(typeof(Models.RepositoryUIStates))]
[JsonSerializable(typeof(List<Models.ConventionalCommitType>))]
[JsonSerializable(typeof(List<Models.LFSLock>))]
[JsonSerializable(typeof(List<Models.VisualStudioInstance>))]
[JsonSerializable(typeof(ViewModels.Preferences))]
internal partial class JsonCodeGen : JsonSerializerContext { }
