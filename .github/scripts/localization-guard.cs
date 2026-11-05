using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

const string Context = "kagayoi/automation-merge-guard-v1";
const int Size = 100, MaxPages = 10;
try
{
    string Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidDataException(name);
    var repo = Env("GITHUB_REPOSITORY");
    if (!Regex.IsMatch(repo, "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw new InvalidDataException("repository");
    var repositoryId = Env("GUARD_REPOSITORY_ID");
    if (!Regex.IsMatch(repositoryId, "^[1-9][0-9]{0,14}$")) throw new InvalidDataException("repository id");
    var token = Env("GH_TOKEN");
    var testBase = Environment.GetEnvironmentVariable("LOCALIZATION_GUARD_TEST_API");
    if (testBase is not null && (token is not ("github-token" or "app-token") || !Regex.IsMatch(testBase, "^http://127\\.0\\.0\\.1:[0-9]+$")))
        throw new InvalidDataException("test endpoint");
    using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Komorebi-localization-guard");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

    long Positive(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n) && n > 0 ? n : throw new InvalidDataException("id");
    string Text(JsonElement value, string name) => value.TryGetProperty(name, out var child) && child.ValueKind == JsonValueKind.String ? child.GetString()! : throw new InvalidDataException(name);
    string? OptionalText(JsonElement value, string name) => value.TryGetProperty(name, out var child) && child.ValueKind == JsonValueKind.String ? child.GetString() : value.TryGetProperty(name, out child) && child.ValueKind == JsonValueKind.Null ? null : throw new InvalidDataException(name);
    string Canonical(JsonElement value) => value.ValueKind switch {
        JsonValueKind.Object => "{" + string.Join(",", value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", value.EnumerateArray().Select(Canonical)) + "]",
        _ => value.GetRawText()
    };
    string RepoPath(string suffix) => $"/repos/{repo}/{suffix}";
    string IdPath(string suffix) => $"/repositories/{repositoryId}/{suffix}";
    async Task<(JsonElement Body, Dictionary<string,int> Links)> Fetch(string suffix, string idSuffix, int page)
    {
        var path = RepoPath(suffix) + $"?per_page={Size}&page={page}";
        var uri = new Uri(testBase is null ? "https://api.github.com" + path : testBase + path);
        using var response = await client.GetAsync(uri);
        if (response.StatusCode != HttpStatusCode.OK) throw new InvalidDataException("API response");
        var links = new Dictionary<string,int>(StringComparer.Ordinal);
        if (response.Headers.TryGetValues("Link", out var headerValues))
        {
            foreach (var entry in string.Join(",", headerValues).Split(','))
            {
                var match = Regex.Match(entry.Trim(), "^<([^<>]+)>;\\s*rel=\"(next|last|prev|first)\"$");
                if (!match.Success || !Uri.TryCreate(match.Groups[1].Value, UriKind.Absolute, out var link) || link.Scheme != "https" || link.Host != "api.github.com" || !link.IsDefaultPort || link.UserInfo.Length != 0 || link.Fragment.Length != 0 || (link.AbsolutePath != RepoPath(suffix) && link.AbsolutePath != IdPath(idSuffix))) throw new InvalidDataException("link");
                var query = link.Query.TrimStart('?').Split('&');
                if (query.Length != 2) throw new InvalidDataException("link query");
                var pairs = query.Select(part => part.Split('=')).ToArray();
                if (pairs.Any(pair => pair.Length != 2) || pairs.Count(pair => pair[0] == "per_page" && pair[1] == "100") != 1 || pairs.Count(pair => pair[0] == "page") != 1) throw new InvalidDataException("link query");
                var numberText = pairs.Single(pair => pair[0] == "page")[1];
                if (!Regex.IsMatch(numberText, "^[1-9][0-9]*$") || !int.TryParse(numberText, out var number) || number > MaxPages + 1 || !links.TryAdd(match.Groups[2].Value, number)) throw new InvalidDataException("link page");
            }
        }
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (doc.RootElement.Clone(), links);
    }
    async Task<List<JsonElement>> Pages(string suffix, string idSuffix, bool combined, string? sha = null)
    {
        var items = new List<JsonElement>(); int? announcedLast = null, total = null;
        string? firstBody = null; Dictionary<string,int>? firstLinks = null;
        async Task<List<JsonElement>> Finish(int page)
        {
            if (page > 1)
            {
                var (body, links) = await Fetch(suffix, idSuffix, 1);
                if (Canonical(body) != firstBody || !links.OrderBy(x => x.Key, StringComparer.Ordinal).SequenceEqual(firstLinks!.OrderBy(x => x.Key, StringComparer.Ordinal)))
                    throw new InvalidDataException("first page changed");
            }
            return items;
        }
        for (var page = 1; page <= MaxPages + (combined ? 0 : 1); page++)
        {
            var (body, links) = await Fetch(suffix, idSuffix, page);
            if (page == 1) { firstBody = Canonical(body); firstLinks = links; }
            var array = body;
            if (combined)
            {
                if (Text(body, "sha") != sha || !body.TryGetProperty("repository", out var r) || Positive(r.GetProperty("id")).ToString() != repositoryId || !body.TryGetProperty("total_count", out var t) || !t.TryGetInt32(out var n) || n < 0 || (total is not null && total != n)) throw new InvalidDataException("combined identity");
                total = n; array = body.GetProperty("statuses");
            }
            if (array.ValueKind != JsonValueKind.Array) throw new InvalidDataException("array");
            var count = array.GetArrayLength();
            if (count > Size || (page == MaxPages + 1 && count != 0)) throw new InvalidDataException("page size");
            if (links.TryGetValue("next", out var next) && (next != page + 1 || count != Size || page > MaxPages)) throw new InvalidDataException("next");
            if (links.TryGetValue("prev", out var prev) && prev != page - 1) throw new InvalidDataException("prev");
            if (links.TryGetValue("first", out var first) && first != 1) throw new InvalidDataException("first");
            if (links.TryGetValue("last", out var last))
            {
                if ((announcedLast is not null && announcedLast != last) || last < page - (count == 0 ? 1 : 0) || (links.ContainsKey("next") && last <= page)) throw new InvalidDataException("last");
                announcedLast = last;
            }
            if (count == 0 && announcedLast == page) throw new InvalidDataException("empty last page");
            if (links.TryGetValue("next", out var declaredNext) && announcedLast is not null && declaredNext > announcedLast) throw new InvalidDataException("next exceeds last");
            if (announcedLast > page && !links.ContainsKey("next")) throw new InvalidDataException("missing next");
            if (combined)
            {
                var expected = Math.Min(Size, Math.Max(0, total!.Value - (page - 1) * Size));
                var final = Math.Max(1, (total.Value + Size - 1) / Size);
                if (count != expected || final > MaxPages || (announcedLast is not null && announcedLast != final) || (page < final) != links.ContainsKey("next")) throw new InvalidDataException("combined pagination");
            }
            items.AddRange(array.EnumerateArray().Select(x => x.Clone()));
            if (combined && items.Count == total) return await Finish(page);
            if (!combined && !links.ContainsKey("next") && count < Size) return await Finish(page);
            if (page == MaxPages + (combined ? 0 : 1)) throw new InvalidDataException("page limit");
        }
        throw new InvalidDataException("pagination");
    }

    if (args.Length == 1 && args[0] == "rules")
    {
        var rules = await Pages("rules/branches/main", "rules/branches/main", false);
        var seen = new HashSet<string>(StringComparer.Ordinal); var guardSources = new List<long>();
        foreach (var rule in rules)
        {
            if (rule.ValueKind != JsonValueKind.Object || !seen.Add(Canonical(rule))) throw new InvalidDataException("duplicate rule");
            var type = Text(rule, "type");
            if (type != "required_status_checks") continue;
            var checks = rule.GetProperty("parameters").GetProperty("required_status_checks");
            if (checks.ValueKind != JsonValueKind.Array) throw new InvalidDataException("checks");
            foreach (var check in checks.EnumerateArray())
                if (string.Equals(Text(check, "context"), Context, StringComparison.OrdinalIgnoreCase)) guardSources.Add(Positive(check.GetProperty("integration_id")));
        }
        if (guardSources.Count > 1) throw new InvalidDataException("duplicate context");
        Console.WriteLine(guardSources.Count == 0 ? "none" : "id:" + guardSources[0]);
    }
    else if (args.Length == 3 && args[0] == "status" && Regex.IsMatch(args[1], "^[0-9a-f]{40}$") && long.TryParse(args[2], out var statusId) && statusId > 0)
    {
        var sha = args[1]; var suffix = $"commits/{sha}/status"; var idSuffix = $"status/{sha}";
        var target = $"https://github.com/{repo}/actions/runs/{Env("GITHUB_RUN_ID")}/attempts/{Env("GITHUB_RUN_ATTEMPT")}";
        var botId = long.Parse(Env("GUARD_BOT_ID")); var botLogin = Env("GUARD_APP_SLUG") + "[bot]";
        async Task<string[]> Combined()
        {
            var statuses = await Pages(suffix, idSuffix, true, sha);
            var ids = new HashSet<long>(); var contexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var snapshot = new List<string>(); var matches = 0;
            foreach (var item in statuses)
            {
                var id = Positive(item.GetProperty("id")); var context = Text(item, "context"); var state = Text(item, "state"); var url = OptionalText(item, "target_url");
                if (!ids.Add(id) || !contexts.Add(context)) throw new InvalidDataException("combined duplicate");
                if (string.Equals(context, Context, StringComparison.OrdinalIgnoreCase)) { if (id != statusId || state != "success" || url != target) throw new InvalidDataException("latest status"); matches++; }
                snapshot.Add(JsonSerializer.Serialize(new object?[] { context.ToLowerInvariant(), id, state, url }));
            }
            if (matches != 1) throw new InvalidDataException("missing status");
            return snapshot.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }
        var before = await Combined();
        var history = await Pages($"commits/{sha}/statuses", $"statuses/{sha}", false);
        var seen = new HashSet<long>(); var matches = 0; long? latestContextId = null;
        foreach (var item in history)
        {
            var id = Positive(item.GetProperty("id")); if (!seen.Add(id)) throw new InvalidDataException("history duplicate");
            if (latestContextId is null && string.Equals(Text(item, "context"), Context, StringComparison.OrdinalIgnoreCase)) latestContextId = id;
            if (id != statusId) continue;
            var creator = item.GetProperty("creator");
            if (Positive(creator.GetProperty("id")) != botId || Text(creator, "login") != botLogin || !string.Equals(Text(item, "context"), Context, StringComparison.OrdinalIgnoreCase) || Text(item, "state") != "success" || OptionalText(item, "target_url") != target || Text(item, "description") != "Validated localization update") throw new InvalidDataException("history identity");
            matches++;
        }
        var after = await Combined();
        if (matches != 1 || latestContextId != statusId || !Enumerable.SequenceEqual(before, after, StringComparer.Ordinal)) throw new InvalidDataException("status changed");
    }
    else throw new InvalidDataException("arguments");
}
catch (Exception)
{
    Console.Error.WriteLine("Localization guard verification failed");
    Environment.ExitCode = 1;
}
