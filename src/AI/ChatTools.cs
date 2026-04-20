using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace Komorebi.AI;

public static class ChatTools
{
    public static readonly ChatTool GetDetailChangesInFile = ChatTool.CreateFunctionTool(
        "GetDetailChangesInFile",
        "Get the detailed changes in the specified file in the specified repository.",
        BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "repo": {
                    "type": "string",
                    "description": "The path to the repository."
                },
                "file": {
                    "type": "string",
                    "description": "The path to the file."
                },
                "originalFile": {
                    "type": "string",
                    "description": "The path to the original file when it has been renamed or copied."
                }
             },
             "required": ["repo", "file"]
        }
        """), false);

    /// <summary>
    /// AI から返された repo/file のパスが想定リポジトリ配下であるかを検証する。
    /// 悪意ある AI レスポンス（または通信路の改ざん）で repo="/etc" 等が渡るのを防ぐ。
    /// file は repo からの相対パスに正規化し、repo 外への逸脱を拒否する。
    /// </summary>
    /// <returns>(OK, 安全な repo, 安全な file) のタプル。OK が false なら file にエラー文言が入る</returns>
    private static (bool Ok, string Repo, string File) ValidatePaths(string expectedRepoFullPath, string claimedRepo, string claimedFile)
    {
        if (string.IsNullOrEmpty(expectedRepoFullPath))
            return (false, string.Empty, "no repository context available");

        var expectedFull = Path.GetFullPath(expectedRepoFullPath);
        string claimedFull;
        try
        { claimedFull = Path.GetFullPath(claimedRepo ?? string.Empty); }
        catch { return (false, string.Empty, $"invalid repo path: {claimedRepo}"); }

        // 完全一致（ケース非対応 OS では大小文字を区別するが、Windows では一致するように正規化は .NET に任せる）
        var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(expectedFull, claimedFull, cmp))
            return (false, string.Empty, $"repo path does not match current repository (claimed '{claimedRepo}')");

        // file は repo からの相対で解決し、repo 外なら拒否
        if (string.IsNullOrEmpty(claimedFile))
            return (false, string.Empty, "file path is empty");
        string resolvedFile;
        try
        { resolvedFile = Path.GetFullPath(Path.Combine(expectedFull, claimedFile)); }
        catch { return (false, string.Empty, $"invalid file path: {claimedFile}"); }

        var withSep = expectedFull.EndsWith(Path.DirectorySeparatorChar) ? expectedFull : expectedFull + Path.DirectorySeparatorChar;
        if (!resolvedFile.StartsWith(withSep, cmp) && !string.Equals(resolvedFile, expectedFull, cmp))
            return (false, string.Empty, $"file path escapes repository: {claimedFile}");

        return (true, expectedFull, claimedFile);
    }

    public static async Task<ToolChatMessage> ProcessAsync(ChatToolCall call, string expectedRepoFullPath, Action<string> output)
    {
        using var doc = JsonDocument.Parse(call.FunctionArguments);

        if (call.FunctionName.Equals(GetDetailChangesInFile.FunctionName))
        {
            var hasRepo = doc.RootElement.TryGetProperty("repo", out var repoPath);
            var hasFile = doc.RootElement.TryGetProperty("file", out var filePath);
            var hasOriginalFile = doc.RootElement.TryGetProperty("originalFile", out var originalFilePath);
            if (!hasRepo)
                throw new ArgumentException("The repo argument is required", "repo");
            if (!hasFile)
                throw new ArgumentException("The file argument is required", "file");

            var check = ValidatePaths(expectedRepoFullPath, repoPath.GetString(), filePath.GetString());
            if (!check.Ok)
                return new ToolChatMessage(call.Id, $"refused: {check.File}");

            output?.Invoke($"Read changes in file: {check.File}");

            var orgFilePath = hasOriginalFile ? originalFilePath.GetString() : string.Empty;
            var rs = await new Commands.GetFileChangeForAI(check.Repo, check.File, orgFilePath).ReadAsync();
            var message = rs.IsSuccess ? rs.StdOut : $"Failed to get diff for '{check.File}'. Error: {rs.StdErr}";
            return new ToolChatMessage(call.Id, message);
        }

        throw new NotSupportedException($"The tool {call.FunctionName} is not supported");
    }

    public static async Task<JsonObject> ProcessAnthropicCall(string toolId, string toolName, JsonElement toolInput, string expectedRepoFullPath, Action<string> output)
    {
        if (toolName == GetDetailChangesInFile.FunctionName)
        {
            var hasRepo = toolInput.TryGetProperty("repo", out var repoPath);
            var hasFile = toolInput.TryGetProperty("file", out var filePath);
            var hasOriginalFile = toolInput.TryGetProperty("originalFile", out var originalFilePath);
            if (!hasRepo)
                throw new ArgumentException("The repo argument is required", "repo");
            if (!hasFile)
                throw new ArgumentException("The file argument is required", "file");

            var check = ValidatePaths(expectedRepoFullPath, repoPath.GetString(), filePath.GetString());
            if (!check.Ok)
            {
                return new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = toolId,
                    ["content"] = $"refused: {check.File}"
                };
            }

            output?.Invoke($"Read changes in file: {check.File}");

            var orgFilePath = hasOriginalFile ? originalFilePath.GetString() : string.Empty;
            var rs = await new Commands.GetFileChangeForAI(check.Repo, check.File, orgFilePath).ReadAsync();
            var message = rs.IsSuccess ? rs.StdOut : $"Failed to get diff for '{check.File}'. Error: {rs.StdErr}";

            return new JsonObject
            {
                ["type"] = "tool_result",
                ["tool_use_id"] = toolId,
                ["content"] = message
            };
        }

        throw new NotSupportedException($"The tool {toolName} is not supported");
    }
}
