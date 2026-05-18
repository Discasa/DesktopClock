using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopClock.Services;

public sealed class AppUpdateService : IDisposable
{
    private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/Discasa/DesktopClock/releases/latest");
    private static readonly TimeSpan StartupCheckDelay = TimeSpan.FromSeconds(20);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly CancellationTokenSource _cancellation = new();
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private bool _updateStarted;

    public event EventHandler<UpdateInstallerReadyEventArgs>? UpdateInstallerReady;

    public void Start()
    {
        _ = Task.Run(() => RunAsync(_cancellation.Token));
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
        _checkLock.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(StartupCheckDelay, cancellationToken);
            if (!cancellationToken.IsCancellationRequested && !_updateStarted)
            {
                await CheckForUpdateAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        if (_updateStarted || !await _checkLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var currentVersion = GetCurrentVersion();
            using var client = CreateHttpClient(currentVersion);
            await using var releaseStream = await client.GetStreamAsync(LatestReleaseUri, cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(releaseStream, JsonOptions, cancellationToken);
            if (release is null || release.Draft || release.Prerelease)
            {
                return;
            }

            if (!TryParseVersion(release.TagName, out var latestVersion) || latestVersion is null || latestVersion <= currentVersion)
            {
                return;
            }

            var packageAsset = FindPackageAsset(release, latestVersion);
            if (packageAsset?.BrowserDownloadUrl is null)
            {
                return;
            }

            var installerPath = await DownloadAndExtractInstallerAsync(client, packageAsset, latestVersion, cancellationToken);
            if (!File.Exists(installerPath))
            {
                return;
            }

            _updateStarted = true;
            UpdateInstallerReady?.Invoke(this, new UpdateInstallerReadyEventArgs(installerPath, latestVersion));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogUpdateError(ex);
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private static HttpClient CreateHttpClient(Version currentVersion)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"DesktopClock/{currentVersion}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static Version GetCurrentVersion()
    {
        var informationalVersion = typeof(AppUpdateService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            .Split('+')[0];

        if (TryParseVersion(informationalVersion, out var version) && version is not null)
        {
            return version;
        }

        return typeof(AppUpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);
    }

    private static bool TryParseVersion(string? value, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var clean = value.Trim();
        if (clean.StartsWith('v') || clean.StartsWith('V'))
        {
            clean = clean[1..];
        }

        var suffixIndex = clean.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            clean = clean[..suffixIndex];
        }

        return Version.TryParse(clean, out version);
    }

    private static GitHubAsset? FindPackageAsset(GitHubRelease release, Version latestVersion)
    {
        var expectedName = $"DesktopClock-{latestVersion}-win-x64.zip";
        return release.Assets.FirstOrDefault(asset => string.Equals(asset.Name, expectedName, StringComparison.OrdinalIgnoreCase))
            ?? release.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? release.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> DownloadAndExtractInstallerAsync(
        HttpClient client,
        GitHubAsset asset,
        Version latestVersion,
        CancellationToken cancellationToken)
    {
        var updateRoot = Path.Combine(Path.GetTempPath(), "DesktopClock", "Updates", latestVersion.ToString());
        if (Directory.Exists(updateRoot))
        {
            Directory.Delete(updateRoot, recursive: true);
        }

        Directory.CreateDirectory(updateRoot);
        var downloadPath = Path.Combine(updateRoot, asset.Name);
        await using (var source = await client.GetStreamAsync(asset.BrowserDownloadUrl!, cancellationToken))
        await using (var target = File.Create(downloadPath))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        VerifyDigest(downloadPath, asset.Digest);

        if (!downloadPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return downloadPath;
        }

        var extractDir = Path.Combine(updateRoot, "package");
        ZipFile.ExtractToDirectory(downloadPath, extractDir);
        var installerPath = Directory.EnumerateFiles(extractDir, "*Installer*.exe", SearchOption.AllDirectories)
            .FirstOrDefault()
            ?? Directory.EnumerateFiles(extractDir, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
        return installerPath ?? throw new FileNotFoundException("The update package does not contain an installer executable.");
    }

    private static void VerifyDigest(string filePath, string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var expected = digest["sha256:".Length..].Trim();
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The downloaded update package failed SHA256 verification.");
        }
    }

    private static void LogUpdateError(Exception exception)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Desktop Clock");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(
                Path.Combine(logDir, "update.log"),
                $"[{DateTimeOffset.Now:O}] {exception}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("digest")]
        public string? Digest { get; set; }
    }
}

public sealed class UpdateInstallerReadyEventArgs(string installerPath, Version version) : EventArgs
{
    public string InstallerPath { get; } = installerPath;
    public Version Version { get; } = version;
}
