using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.DTOs.Update;
using API.SignalR;
using Flurl.Http;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using MarkdownDeep;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;
#nullable enable

internal class GithubReleaseMetadata
{
    /// <summary>
    /// Name of the Tag
    /// <example>v0.4.3</example>
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public required string Tag_Name { get; init; }
    /// <summary>
    /// Name of the Release
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Body of the Release
    /// </summary>
    public required string Body { get; init; }
    /// <summary>
    /// Url of the release on Github
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public required string Html_Url { get; init; }
    /// <summary>
    /// Date Release was Published
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public required string Published_At { get; init; }
}

public interface IVersionUpdaterService
{
    Task<UpdateNotificationDto?> CheckForUpdate();
    Task PushUpdate(UpdateNotificationDto update);
    Task<IList<UpdateNotificationDto>> GetAllReleases(int count = 0);
    Task<int> GetNumberOfReleasesBehind();
}

public partial class VersionUpdaterService : IVersionUpdaterService
{
    private readonly ILogger<VersionUpdaterService> _logger;
    private readonly IEventHub _eventHub;
    private readonly Markdown _markdown = new MarkdownDeep.Markdown();
#pragma warning disable S1075
    private const string GithubLatestReleasesUrl = "https://api.github.com/repos/Kareadita/Kavita/releases/latest";
    private const string GithubAllReleasesUrl = "https://api.github.com/repos/Kareadita/Kavita/releases";
#pragma warning restore S1075

    [GeneratedRegex(@"^\n*(.*?)\n+#{1,2}\s", RegexOptions.Singleline)]
    private static partial Regex BlogPartRegex();

    public VersionUpdaterService(ILogger<VersionUpdaterService> logger, IEventHub eventHub)
    {
        _logger = logger;
        _eventHub = eventHub;

        FlurlHttp.ConfigureClient(GithubLatestReleasesUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
        FlurlHttp.ConfigureClient(GithubAllReleasesUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
    }

    /// <summary>
    /// Fetches the latest release from Github
    /// </summary>
    /// <returns>Latest update</returns>
    public async Task<UpdateNotificationDto?> CheckForUpdate()
    {
        var update = await GetGithubRelease();
        return CreateDto(update);
    }

    public async Task<IList<UpdateNotificationDto>> GetAllReleases(int count = 0)
    {
        var updates = await GetGithubReleases();
        var query = updates.Select(CreateDto)
            .Where(d => d != null)
            .OrderByDescending(d => d!.PublishDate)
            .Select(d => d!);

        if (count > 0)
        {
            query = query.Take(count);
        }

        var updateDtos = query.ToList();

        // Find the latest dto
        var latestRelease = updateDtos[0]!;
        var updateVersion = new Version(latestRelease.UpdateVersion);
        var isNightly = BuildInfo.Version > new Version(latestRelease.UpdateVersion);

        // isNightly can be true when we compare something like v0.8.1 vs v0.8.1.0
        if (IsVersionEqualToBuildVersion(updateVersion))
        {
            isNightly = false;
        }


        latestRelease.IsOnNightlyInRelease = isNightly;

        return updateDtos;
    }

    private static bool IsVersionEqualToBuildVersion(Version updateVersion)
    {
        return updateVersion.Revision < 0 && BuildInfo.Version.Revision == 0 &&
               CompareWithoutRevision(BuildInfo.Version, updateVersion);
    }

    private static bool CompareWithoutRevision(Version v1, Version v2)
    {
        if (v1.Major != v2.Major)
            return v1.Major == v2.Major;
        if (v1.Minor != v2.Minor)
            return v1.Minor == v2.Minor;
        if (v1.Build != v2.Build)
            return v1.Build == v2.Build;
        return true;
    }

    public async Task<int> GetNumberOfReleasesBehind()
    {
        var updates = await GetAllReleases();
        return updates.TakeWhile(update => update.UpdateVersion != update.CurrentVersion).Count();
    }

    private UpdateNotificationDto? CreateDto(GithubReleaseMetadata? update)
    {
        if (update == null || string.IsNullOrEmpty(update.Tag_Name)) return null;
        var updateVersion = new Version(update.Tag_Name.Replace("v", string.Empty));
        var currentVersion = BuildInfo.Version.ToString(4);

        var bodyHtml = _markdown.Transform(update.Body.Trim());
        var parsedSections = ParseReleaseBody(update.Body);
        var blogPart = _markdown.Transform(ExtractBlogPart(update.Body).Trim());

        return new UpdateNotificationDto()
        {
            CurrentVersion = currentVersion,
            UpdateVersion = updateVersion.ToString(),
            UpdateBody = bodyHtml,
            UpdateTitle = update.Name,
            UpdateUrl = update.Html_Url,
            IsDocker = OsInfo.IsDocker,
            PublishDate = update.Published_At,
            IsReleaseEqual = IsVersionEqualToBuildVersion(updateVersion),
            IsReleaseNewer = BuildInfo.Version < updateVersion,

            Added = parsedSections.TryGetValue("Added", out var added) ? added : [],
            Removed = parsedSections.TryGetValue("Removed", out var removed) ? removed : [],
            Changed = parsedSections.TryGetValue("Changed", out var changed) ? changed : [],
            Fixed = parsedSections.TryGetValue("Fixed", out var fixes) ? fixes : [],
            Theme = parsedSections.TryGetValue("Theme", out var theme) ? theme : [],
            Developer = parsedSections.TryGetValue("Developer", out var developer) ? developer : [],
            Api = parsedSections.TryGetValue("Api", out var api) ? api : [],
            BlogPart = blogPart
        };
    }


    public async Task PushUpdate(UpdateNotificationDto? update)
    {
        if (update == null) return;

        var updateVersion = new Version(update.UpdateVersion);

        if (BuildInfo.Version < updateVersion)
        {
            _logger.LogWarning("Server is out of date. Current: {CurrentVersion}. Available: {AvailableUpdate}", BuildInfo.Version, updateVersion);
            await _eventHub.SendMessageAsync(MessageFactory.UpdateAvailable, MessageFactory.UpdateVersionEvent(update),
                true);
        }
    }


    private static async Task<GithubReleaseMetadata> GetGithubRelease()
    {
        var update = await GithubLatestReleasesUrl
            .WithHeader("Accept", "application/json")
            .WithHeader("User-Agent", "Kavita")
            .GetJsonAsync<GithubReleaseMetadata>();

        return update;
    }

    private static async Task<IEnumerable<GithubReleaseMetadata>> GetGithubReleases()
    {
        var update = await GithubAllReleasesUrl
            .WithHeader("Accept", "application/json")
            .WithHeader("User-Agent", "Kavita")
            .GetJsonAsync<IEnumerable<GithubReleaseMetadata>>();

        return update;
    }

    private static string ExtractBlogPart(string body)
    {
        var match = BlogPartRegex().Match(body);
        return match.Success ? match.Groups[1].Value.Trim() : body.Trim();
    }

    private static Dictionary<string, List<string>> ParseReleaseBody(string body)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var lines = body.Split('\n');
        string currentSection = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Check for section headers (case-insensitive)
            if (trimmedLine.StartsWith('#'))
            {
                currentSection = trimmedLine.TrimStart('#').Trim();
                sections[currentSection] = [];
                continue;
            }

            // Parse items under a section
            if (currentSection != null &&
                trimmedLine.StartsWith("- ") &&
                !string.IsNullOrWhiteSpace(trimmedLine))
            {
                // Remove "Fixed:", "Added:" etc. if present
                var cleanedItem = CleanSectionItem(trimmedLine);

                // Only add non-empty items
                if (!string.IsNullOrWhiteSpace(cleanedItem))
                {
                    sections[currentSection].Add(cleanedItem);
                }
            }
        }

        return sections;
    }

    private static string CleanSectionItem(string item)
    {
        // Remove everything up to and including the first ":"
        var colonIndex = item.IndexOf(':');
        if (colonIndex != -1)
        {
            item = item.Substring(colonIndex + 1).Trim();
        }

        return item;
    }

}
