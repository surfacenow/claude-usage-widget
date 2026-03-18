using FloatingClockWidget.Native.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FloatingClockWidget.Native.Services;

public sealed class ThemeService
{
    public const string DefaultThemeId = "pop-neon-default";

    private static readonly Regex SemverRegex = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);
    private static readonly Regex ThemeIdRegex = new(@"^[a-z0-9][a-z0-9-]{2,63}$", RegexOptions.Compiled);
    private static readonly Regex HexColorRegex = new(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly RuntimePaths _paths;

    public ThemeService(RuntimePaths paths)
    {
        _paths = paths;
    }

    public async Task EnsureInitializedAsync(string appVersion, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.ThemesDir);
        Directory.CreateDirectory(_paths.ImportsTmpDir);
        Directory.CreateDirectory(_paths.ImportsFailedDir);
        Directory.CreateDirectory(_paths.LogsDir);

        await EnsureDefaultThemeAsync(appVersion, cancellationToken);
    }

    public Task<IReadOnlyList<ThemeSummary>> ListThemesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ThemeSummary>();

        if (!Directory.Exists(_paths.ThemesDir))
        {
            return Task.FromResult<IReadOnlyList<ThemeSummary>>(results);
        }

        foreach (var themeDir in Directory.EnumerateDirectories(_paths.ThemesDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryName = Path.GetFileName(themeDir);
            if (directoryName.StartsWith(".staging-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var manifestPath = Path.Combine(themeDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var source = File.ReadAllText(manifestPath, Encoding.UTF8);
                var manifest = JsonSerializer.Deserialize<ThemeManifest>(source, JsonOptions);
                if (manifest is null)
                {
                    continue;
                }

                results.Add(new ThemeSummary
                {
                    Id = manifest.Id,
                    Name = manifest.Name,
                    Version = manifest.Version,
                    Author = manifest.Author,
                });
            }
            catch
            {
                // Ignore invalid themes and keep listing healthy themes only.
            }
        }

        var sorted = results
            .OrderBy(theme => theme.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<ThemeSummary>>(sorted);
    }

    public async Task<ThemeBundle> LoadThemeBundleAsync(
        string themeId,
        CancellationToken cancellationToken = default)
    {
        var themeDir = Path.Combine(_paths.ThemesDir, themeId);
        var manifestPath = Path.Combine(themeDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"manifest not found for theme '{themeId}'");
        }

        var manifestText = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        var manifest = JsonSerializer.Deserialize<ThemeManifest>(manifestText, JsonOptions)
            ?? throw new InvalidOperationException("manifest parse failed");

        var tokensPath = ResolveInsideRoot(themeDir, manifest.EntryTokens)
            ?? throw new InvalidOperationException("invalid entryTokens path");
        if (!File.Exists(tokensPath))
        {
            throw new InvalidOperationException("tokens file not found");
        }

        var tokensText = await File.ReadAllTextAsync(tokensPath, cancellationToken);
        var tokens = JsonSerializer.Deserialize<ThemeTokens>(tokensText, JsonOptions)
            ?? throw new InvalidOperationException("tokens parse failed");

        return new ThemeBundle
        {
            Manifest = manifest,
            Tokens = tokens,
        };
    }

    public async Task<ThemeImportResult> ImportThemeZipAsync(
        string zipPath,
        string currentAppVersion,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(zipPath))
        {
            return Fail("zip_not_found", "Zip file was not found.", []);
        }

        if (!zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("not_zip", "Only .zip files are supported.", []);
        }

        var importId = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}".Substring(0, 24);
        var workDir = Path.Combine(_paths.ImportsTmpDir, importId);
        Directory.CreateDirectory(workDir);

        try
        {
            ExtractZipSecure(zipPath, workDir, cancellationToken);
            var themeRoot = FindThemeRoot(workDir)
                ?? throw new InvalidOperationException("manifest.json and tokens.json were not found in zip");

            var manifestPath = Path.Combine(themeRoot, "manifest.json");
            var tokensPath = Path.Combine(themeRoot, "tokens.json");
            var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var tokensJson = await File.ReadAllTextAsync(tokensPath, cancellationToken);

            var manifest = JsonSerializer.Deserialize<ThemeManifest>(manifestJson, JsonOptions)
                ?? new ThemeManifest();
            var tokens = JsonSerializer.Deserialize<ThemeTokens>(tokensJson, JsonOptions)
                ?? new ThemeTokens();

            var checks = ValidateTheme(manifestJson, tokensJson, manifest, tokens, themeRoot, currentAppVersion);
            if (checks.Any(check => !check.Pass))
            {
                await CopyZipToFailedAsync(zipPath, importId, cancellationToken);
                return Fail("validation_failed", "Theme validation failed.", checks);
            }

            var targetDir = Path.Combine(_paths.ThemesDir, manifest.Id);
            if (Directory.Exists(targetDir))
            {
                var duplicateChecks = checks
                    .Append(new ThemeValidationCheck
                    {
                        Id = "theme-id-unique",
                        Label = "theme id unique",
                        Pass = false,
                        Detail = $"theme id '{manifest.Id}' already exists",
                    })
                    .ToList();
                await CopyZipToFailedAsync(zipPath, importId, cancellationToken);
                return Fail("duplicate_theme_id", $"Theme id already exists: {manifest.Id}", duplicateChecks);
            }

            var stagingDir = Path.Combine(_paths.ThemesDir, $".staging-{manifest.Id}-{importId}");
            CopyDirectory(themeRoot, stagingDir);

            try
            {
                Directory.Move(stagingDir, targetDir);
            }
            catch
            {
                if (Directory.Exists(stagingDir))
                {
                    Directory.Delete(stagingDir, recursive: true);
                }
                throw;
            }

            return new ThemeImportResult
            {
                Ok = true,
                ErrorCode = string.Empty,
                Message = "Theme imported successfully.",
                ThemeId = manifest.Id,
                Manifest = manifest,
                Tokens = tokens,
                Checks = checks,
            };
        }
        catch (Exception ex)
        {
            await CopyZipToFailedAsync(zipPath, importId, cancellationToken);
            return Fail("import_failed", ex.Message, []);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }
        }
    }

    private async Task EnsureDefaultThemeAsync(string appVersion, CancellationToken cancellationToken)
    {
        var themeDir = Path.Combine(_paths.ThemesDir, DefaultThemeId);
        Directory.CreateDirectory(themeDir);

        var manifest = new ThemeManifest
        {
            Id = DefaultThemeId,
            Name = "Pop Neon Default",
            Version = "1.0.0",
            RequiredAppVersion = appVersion,
            Author = "Floating Clock Team",
            Preview = "preview.webp",
            EntryCss = "panel.css",
            EntryTokens = "tokens.json",
        };

        var tokens = new ThemeTokens
        {
            Colors = new ThemeColors
            {
                Panel = "#0B0B12DD",
                TextMain = "#F7FDFF",
                TextSub = "#9CB6C9",
                AccentA = "#00E5FF",
                AccentB = "#FF3DFF",
                AccentC = "#C8FF3D",
            },
            Typography = new ThemeTypography
            {
                ClockFont = "Segoe UI Semibold",
                UiFont = "Segoe UI",
            },
            Radius = new ThemeRadius { Panel = 22 },
            Shadow = new ThemeShadow
            {
                PanelGlow = "0 0 18px rgba(0,229,255,0.20), 0 0 34px rgba(255,61,255,0.14)",
            },
            Blur = new ThemeBlur { PanelBackdrop = 10 },
            Motion = new ThemeMotion
            {
                DriftPx = 4,
                DriftCycleSec = 14,
                LocatorPulseSec = 8,
            },
        };

        await WriteIfMissingAsync(
            Path.Combine(themeDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, JsonOptions),
            cancellationToken);
        await WriteIfMissingAsync(
            Path.Combine(themeDir, "tokens.json"),
            JsonSerializer.Serialize(tokens, JsonOptions),
            cancellationToken);
        await WriteIfMissingAsync(
            Path.Combine(themeDir, "panel.css"),
            "/* native app ignores css, kept for compatibility */",
            cancellationToken);
        await WriteIfMissingAsync(
            Path.Combine(themeDir, "preview.webp"),
            "placeholder",
            cancellationToken);
    }

    private static async Task WriteIfMissingAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken)
    {
        if (File.Exists(filePath))
        {
            return;
        }

        await File.WriteAllTextAsync(filePath, content + Environment.NewLine, cancellationToken);
    }

    private static void ExtractZipSecure(string zipPath, string destinationDir, CancellationToken cancellationToken)
    {
        var destinationRoot = Path.GetFullPath(destinationDir);

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sanitized = SanitizeZipEntryPath(entry.FullName);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                continue;
            }

            if (IsSymlinkEntry(entry))
            {
                throw new InvalidOperationException($"symlink is not allowed: {entry.FullName}");
            }

            var outputPath = Path.GetFullPath(Path.Combine(destinationRoot, sanitized));
            if (!IsChildPath(destinationRoot, outputPath))
            {
                throw new InvalidOperationException($"path traversal rejected: {entry.FullName}");
            }

            var isDirectory = entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                              entry.FullName.EndsWith("\\", StringComparison.Ordinal);

            if (isDirectory)
            {
                Directory.CreateDirectory(outputPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            entry.ExtractToFile(outputPath, overwrite: false);
        }
    }

    private static bool IsSymlinkEntry(ZipArchiveEntry entry)
    {
        var mode = (entry.ExternalAttributes >> 16) & 0xF000;
        return mode == 0xA000;
    }

    private static string SanitizeZipEntryPath(string fullName)
    {
        var normalized = fullName.Replace("\\", "/").TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var trimmed = normalized.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                throw new InvalidOperationException($"unsafe zip entry path: {fullName}");
            }
        }

        return Path.Combine(segments);
    }

    private static string? FindThemeRoot(string extractedRoot)
    {
        var roots = new List<string>();

        foreach (var dir in Directory.EnumerateDirectories(extractedRoot, "*", SearchOption.AllDirectories))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            var tokensPath = Path.Combine(dir, "tokens.json");
            if (File.Exists(manifestPath) && File.Exists(tokensPath))
            {
                roots.Add(dir);
            }
        }

        var rootManifest = Path.Combine(extractedRoot, "manifest.json");
        var rootTokens = Path.Combine(extractedRoot, "tokens.json");
        if (File.Exists(rootManifest) && File.Exists(rootTokens))
        {
            roots.Add(extractedRoot);
        }

        if (roots.Count == 0)
        {
            return null;
        }

        return roots
            .OrderBy(path => path.Count(ch => ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static IReadOnlyList<ThemeValidationCheck> ValidateTheme(
        string manifestJson,
        string tokensJson,
        ThemeManifest manifest,
        ThemeTokens tokens,
        string themeRoot,
        string currentAppVersion)
    {
        var checks = new List<ThemeValidationCheck>();

        var manifestSchemaDetail = string.Empty;
        var manifestValueDetail = string.Empty;
        var manifestSchemaOk = ValidateManifestJson(manifestJson, out manifestSchemaDetail) &&
                               ValidateManifestValues(manifest, out manifestValueDetail);
        checks.Add(new ThemeValidationCheck
        {
            Id = "manifest-schema",
            Label = "manifest schema",
            Pass = manifestSchemaOk,
            Detail = manifestSchemaOk ? "pass" : $"{manifestSchemaDetail}; {manifestValueDetail}".Trim(';', ' '),
        });

        var tokensSchemaDetail = string.Empty;
        var tokensValueDetail = string.Empty;
        var tokensSchemaOk = ValidateTokensJson(tokensJson, out tokensSchemaDetail) &&
                             ValidateTokensValues(tokens, out tokensValueDetail);
        checks.Add(new ThemeValidationCheck
        {
            Id = "tokens-schema",
            Label = "tokens schema",
            Pass = tokensSchemaOk,
            Detail = tokensSchemaOk ? "pass" : $"{tokensSchemaDetail}; {tokensValueDetail}".Trim(';', ' '),
        });

        var versionOk = IsVersionCompatible(manifest.RequiredAppVersion, currentAppVersion);
        checks.Add(new ThemeValidationCheck
        {
            Id = "required-app-version",
            Label = "required app version",
            Pass = versionOk,
            Detail = versionOk
                ? $"app {currentAppVersion} satisfies {manifest.RequiredAppVersion}"
                : $"app {currentAppVersion} does not satisfy {manifest.RequiredAppVersion}",
        });

        checks.Add(FileCheck("preview-file", "preview file", themeRoot, manifest.Preview));
        checks.Add(FileCheck("entry-css", "entry css", themeRoot, manifest.EntryCss));
        checks.Add(FileCheck("entry-tokens", "entry tokens", themeRoot, manifest.EntryTokens));

        return checks;
    }

    private static ThemeValidationCheck FileCheck(
        string id,
        string label,
        string themeRoot,
        string relativePath)
    {
        var resolved = ResolveInsideRoot(themeRoot, relativePath);
        var ok = resolved is not null && File.Exists(resolved);
        return new ThemeValidationCheck
        {
            Id = id,
            Label = label,
            Pass = ok,
            Detail = ok ? "pass" : "missing or invalid path",
        };
    }

    private static bool ValidateManifestJson(string manifestJson, out string detail)
    {
        var requiredKeys = new[]
        {
            "id",
            "name",
            "version",
            "requiredAppVersion",
            "author",
            "preview",
            "entryCss",
            "entryTokens",
        };

        return ValidateRequiredObjectKeys(manifestJson, requiredKeys, out detail);
    }

    private static bool ValidateTokensJson(string tokensJson, out string detail)
    {
        var topLevelKeys = new[]
        {
            "colors",
            "typography",
            "radius",
            "shadow",
            "blur",
            "motion",
        };

        if (!ValidateRequiredObjectKeys(tokensJson, topLevelKeys, out detail))
        {
            return false;
        }

        using var document = JsonDocument.Parse(tokensJson);
        var root = document.RootElement;

        var checks = new List<string>();
        if (!HasObjectKeys(root, "colors", "panel", "textMain", "textSub", "accentA", "accentB", "accentC"))
        {
            checks.Add("colors keys missing");
        }
        if (!HasObjectKeys(root, "typography", "clockFont", "uiFont"))
        {
            checks.Add("typography keys missing");
        }
        if (!HasObjectKeys(root, "motion", "driftPx", "driftCycleSec", "locatorPulseSec"))
        {
            checks.Add("motion keys missing");
        }

        if (checks.Count == 0)
        {
            detail = "pass";
            return true;
        }

        detail = string.Join("; ", checks);
        return false;
    }

    private static bool HasObjectKeys(JsonElement root, string objectName, params string[] keys)
    {
        if (!root.TryGetProperty(objectName, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var key in keys)
        {
            if (!node.TryGetProperty(key, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateRequiredObjectKeys(string json, IReadOnlyCollection<string> keys, out string detail)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                detail = "json root is not object";
                return false;
            }

            var missing = keys
                .Where(key => !document.RootElement.TryGetProperty(key, out _))
                .ToList();

            if (missing.Count == 0)
            {
                detail = "pass";
                return true;
            }

            detail = "missing keys: " + string.Join(", ", missing);
            return false;
        }
        catch (Exception ex)
        {
            detail = "invalid JSON: " + ex.Message;
            return false;
        }
    }

    private static bool ValidateManifestValues(ThemeManifest manifest, out string detail)
    {
        var issues = new List<string>();

        if (!ThemeIdRegex.IsMatch(manifest.Id))
        {
            issues.Add("id must match ^[a-z0-9][a-z0-9-]{2,63}$");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            issues.Add("name is required");
        }

        if (!SemverRegex.IsMatch(manifest.Version))
        {
            issues.Add("version must be semver");
        }

        if (!SemverRegex.IsMatch(manifest.RequiredAppVersion))
        {
            issues.Add("requiredAppVersion must be semver");
        }

        if (string.IsNullOrWhiteSpace(manifest.Author))
        {
            issues.Add("author is required");
        }

        if (string.IsNullOrWhiteSpace(manifest.Preview))
        {
            issues.Add("preview is required");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryCss))
        {
            issues.Add("entryCss is required");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryTokens))
        {
            issues.Add("entryTokens is required");
        }

        if (issues.Count == 0)
        {
            detail = "pass";
            return true;
        }

        detail = string.Join("; ", issues);
        return false;
    }

    private static bool ValidateTokensValues(ThemeTokens tokens, out string detail)
    {
        var issues = new List<string>();

        ValidateColor(tokens.Colors.Panel, "colors.panel", issues);
        ValidateColor(tokens.Colors.TextMain, "colors.textMain", issues);
        ValidateColor(tokens.Colors.TextSub, "colors.textSub", issues);
        ValidateColor(tokens.Colors.AccentA, "colors.accentA", issues);
        ValidateColor(tokens.Colors.AccentB, "colors.accentB", issues);
        ValidateColor(tokens.Colors.AccentC, "colors.accentC", issues);

        if (string.IsNullOrWhiteSpace(tokens.Typography.ClockFont))
        {
            issues.Add("typography.clockFont is required");
        }

        if (string.IsNullOrWhiteSpace(tokens.Typography.UiFont))
        {
            issues.Add("typography.uiFont is required");
        }

        if (tokens.Radius.Panel is < 8 or > 64)
        {
            issues.Add("radius.panel must be between 8 and 64");
        }

        if (tokens.Blur.PanelBackdrop is < 0 or > 40)
        {
            issues.Add("blur.panelBackdrop must be between 0 and 40");
        }

        if (tokens.Motion.DriftPx is < 0 or > 16)
        {
            issues.Add("motion.driftPx must be between 0 and 16");
        }

        if (tokens.Motion.DriftCycleSec is < 4 or > 60)
        {
            issues.Add("motion.driftCycleSec must be between 4 and 60");
        }

        if (tokens.Motion.LocatorPulseSec is < 2 or > 60)
        {
            issues.Add("motion.locatorPulseSec must be between 2 and 60");
        }

        if (issues.Count == 0)
        {
            detail = "pass";
            return true;
        }

        detail = string.Join("; ", issues);
        return false;
    }

    private static void ValidateColor(string value, string fieldName, ICollection<string> issues)
    {
        if (!HexColorRegex.IsMatch(value))
        {
            issues.Add($"{fieldName} must be #RRGGBB or #RRGGBBAA");
        }
    }

    private static bool IsVersionCompatible(string requiredVersion, string currentVersion)
    {
        return CompareSemver(currentVersion, requiredVersion) >= 0;
    }

    private static int CompareSemver(string left, string right)
    {
        var l = ParseSemver(left);
        var r = ParseSemver(right);

        for (var i = 0; i < 3; i++)
        {
            if (l[i] > r[i])
            {
                return 1;
            }

            if (l[i] < r[i])
            {
                return -1;
            }
        }

        return 0;
    }

    private static int[] ParseSemver(string version)
    {
        var match = SemverRegex.Match(version.Trim());
        if (!match.Success)
        {
            return [0, 0, 0];
        }

        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return
        [
            int.Parse(parts[0]),
            int.Parse(parts[1]),
            int.Parse(parts[2]),
        ];
    }

    private static string? ResolveInsideRoot(string root, string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        if (normalized is null)
        {
            return null;
        }

        var rootPath = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(rootPath, normalized));
        return IsChildPath(rootPath, candidate) ? candidate : null;
    }

    private static bool IsChildPath(string rootPath, string candidatePath)
    {
        var rootNormalized = Path.GetFullPath(rootPath);
        var candidateNormalized = Path.GetFullPath(candidatePath);

        var rootWithSep = rootNormalized.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidateNormalized.Equals(rootNormalized, StringComparison.OrdinalIgnoreCase) ||
               candidateNormalized.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalized = relativePath.Replace("\\", "/").TrimStart('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        if (segments.Any(segment => segment is "." or ".."))
        {
            return null;
        }

        return Path.Combine(segments);
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var filePath in Directory.EnumerateFiles(sourceDir))
        {
            var fileName = Path.GetFileName(filePath);
            var targetFilePath = Path.Combine(targetDir, fileName);
            File.Copy(filePath, targetFilePath, overwrite: false);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDir))
        {
            var directoryName = Path.GetFileName(directoryPath);
            var targetChild = Path.Combine(targetDir, directoryName);
            CopyDirectory(directoryPath, targetChild);
        }
    }

    private async Task CopyZipToFailedAsync(string zipPath, string importId, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = Path.GetFileName(zipPath);
            var safeName = SanitizeFileName(fileName);
            var failedPath = Path.Combine(_paths.ImportsFailedDir, $"{importId}-{safeName}");

            await using var source = File.OpenRead(zipPath);
            await using var destination = File.Create(failedPath);
            await source.CopyToAsync(destination, cancellationToken);
        }
        catch
        {
            // no-op
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safe = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return safe.Replace(' ', '_');
    }

    private static ThemeImportResult Fail(string code, string message, IReadOnlyList<ThemeValidationCheck> checks)
    {
        return new ThemeImportResult
        {
            Ok = false,
            ErrorCode = code,
            Message = message,
            Checks = checks,
        };
    }
}
