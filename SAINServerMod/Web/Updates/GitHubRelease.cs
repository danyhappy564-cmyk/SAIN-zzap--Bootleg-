using System.Text.Json.Serialization;

namespace SAINServerMod.Web.Updates;

public sealed record GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("body")]
    public string? Body { get; init; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }

    [JsonPropertyName("draft")]
    public bool Draft { get; init; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; init; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; init; }

    [JsonIgnore]
    public string DisplayTitle
    {
        // zzap: the tag decides the title - release names drifted ("SAIN-zzap--Bootleg- remake5.1" on tag remix5.1).
        get { return ZzapRelease.TryParse(TagName, out _) ? ZzapRelease.Label(TagName) : string.IsNullOrWhiteSpace(Name) ? TagName : Name; }
    }
}

public sealed record CachedReleases
{
    public DateTimeOffset FetchedAt { get; init; }
    public List<GitHubRelease> Releases { get; init; } = [];
}

public enum ReleaseFeedState
{
    Idle,
    Loading,
    Loaded,
    Unavailable,
}
