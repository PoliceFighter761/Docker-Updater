using System.Text;
using System.Text.Json;
using Docker.DotNet.Models;

namespace DockerUpdater.Worker.Docker
{
    public sealed class RegistryAuthResolver(ILogger<RegistryAuthResolver> logger)
    {
        private const string DockerHubAuthKey = "https://index.docker.io/v1/";
        private readonly Lazy<string?> _cachedConfigJson = new(LoadDockerConfigJson);

        public AuthConfig? ResolveForImage(string imageName)
        {
            string registry = ExtractRegistry(imageName);
            string? configJson = _cachedConfigJson.Value;
            if (string.IsNullOrWhiteSpace(configJson))
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(configJson);
                if (!document.RootElement.TryGetProperty("auths", out JsonElement auths) || auths.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                foreach (string key in RegistryLookupCandidates(registry))
                {
                    if (!auths.TryGetProperty(key, out JsonElement authEntry) || authEntry.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    AuthConfig? authConfig = ParseAuthEntry(authEntry, registry);
                    if (authConfig is not null)
                    {
                        return authConfig;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed parsing Docker auth config while resolving registry auth for {Image}", imageName);
            }

            return null;
        }

        private static string? LoadDockerConfigJson()
        {
            string? dockerConfigRoot = Environment.GetEnvironmentVariable("DOCKER_CONFIG");
            string configPath = string.IsNullOrWhiteSpace(dockerConfigRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker", "config.json")
                : Path.Combine(dockerConfigRoot, "config.json");

            if (!File.Exists(configPath))
            {
                return null;
            }

            return File.ReadAllText(configPath);
        }

        private static AuthConfig? ParseAuthEntry(JsonElement authEntry, string registry)
        {
            string? identityToken = GetString(authEntry, "identitytoken") ?? GetString(authEntry, "identityToken");
            string? username = GetString(authEntry, "username");
            string? password = GetString(authEntry, "password");

            if ((string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                && TryDecodeAuth(GetString(authEntry, "auth"), out string decodedUsername, out string decodedPassword))
            {
                username = decodedUsername;
                password = decodedPassword;
            }

            if (string.IsNullOrWhiteSpace(identityToken) && (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)))
            {
                return null;
            }

            return new AuthConfig
            {
                ServerAddress = registry,
                IdentityToken = identityToken,
                Username = username,
                Password = password
            };
        }

        private static bool TryDecodeAuth(string? encodedAuth, out string username, out string password)
        {
            username = string.Empty;
            password = string.Empty;

            if (string.IsNullOrWhiteSpace(encodedAuth))
            {
                return false;
            }

            try
            {
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encodedAuth));
                int separator = decoded.IndexOf(':');
                if (separator <= 0)
                {
                    return false;
                }

                username = decoded[..separator];
                password = decoded[(separator + 1)..];
                return !string.IsNullOrWhiteSpace(username);
            }
            catch
            {
                return false;
            }
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        public static string ExtractRegistry(string imageName)
        {
            string imageWithoutTagOrDigest = imageName.Split('@', 2, StringSplitOptions.TrimEntries)[0];
            int lastSlash = imageWithoutTagOrDigest.LastIndexOf('/');
            int lastColon = imageWithoutTagOrDigest.LastIndexOf(':');
            if (lastColon > lastSlash)
            {
                imageWithoutTagOrDigest = imageWithoutTagOrDigest[..lastColon];
            }

            string firstSegment = imageWithoutTagOrDigest.Split('/', 2, StringSplitOptions.TrimEntries)[0];
            bool hasExplicitRegistry = firstSegment.Contains('.', StringComparison.Ordinal)
                || firstSegment.Contains(':', StringComparison.Ordinal)
                || string.Equals(firstSegment, "localhost", StringComparison.OrdinalIgnoreCase);

            return hasExplicitRegistry ? firstSegment : "index.docker.io";
        }

        private static IEnumerable<string> RegistryLookupCandidates(string registry)
        {
            if (string.Equals(registry, "index.docker.io", StringComparison.OrdinalIgnoreCase)
                || string.Equals(registry, "docker.io", StringComparison.OrdinalIgnoreCase))
            {
                yield return DockerHubAuthKey;
                yield return "docker.io";
                yield return "index.docker.io";
            }

            yield return registry;
            yield return $"https://{registry}";
            yield return $"https://{registry}/v1/";
        }
    }
}