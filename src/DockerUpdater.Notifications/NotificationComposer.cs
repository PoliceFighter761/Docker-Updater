using System.Text;
using System.Text.RegularExpressions;
using DockerUpdater.Shared;

namespace DockerUpdater.Notifications
{
    public static partial class NotificationComposer
    {
        [GeneratedRegex("\\{\\{\\s*([a-zA-Z0-9_]+)\\s*\\}\\}", RegexOptions.Compiled)]
        private static partial Regex TemplateVariableRegex();
        [GeneratedRegex("\\{\\{#if\\s+([a-zA-Z0-9_]+)\\s*\\}\\}(.*?)\\{\\{/if\\}\\}", RegexOptions.Compiled | RegexOptions.Singleline)]
        private static partial Regex ConditionalBlockRegex();

        public static string Compose(UpdateSessionResult session, string? template = null)
        {
            if (!string.IsNullOrWhiteSpace(template))
            {
                return ComposeFromTemplate(session, template);
            }

            StringBuilder builder = new();
            builder.AppendLine("Docker Updater session finished.");
            builder.AppendLine($"Scanned: {session.Scanned}, Updated: {session.Updated}, Failed: {session.Failed}");

            foreach (ContainerUpdateResult result in session.Results)
            {
                if (result.State is ContainerUpdateState.Updated or ContainerUpdateState.Failed)
                {
                    string detail = string.IsNullOrWhiteSpace(result.Error) ? string.Empty : $" ({result.Error})";
                    builder.AppendLine($"- {result.Name}: {result.State}{detail}");
                }
            }

            return builder.ToString();
        }

        private static string ComposeFromTemplate(UpdateSessionResult session, string template)
        {
            int fresh = session.Results.Count(static result => result.State == ContainerUpdateState.Fresh);
            int skipped = session.Results.Count(static result => result.State == ContainerUpdateState.Skipped);
            IReadOnlyList<ContainerUpdateResult> updatedItems = session.Results.Where(static result => result.State == ContainerUpdateState.Updated).ToList();
            IReadOnlyList<ContainerUpdateResult> failedItems = session.Results.Where(static result => result.State == ContainerUpdateState.Failed).ToList();

            Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase)
            {
                ["scanned"] = session.Scanned.ToString(),
                ["updated"] = session.Updated.ToString(),
                ["failed"] = session.Failed.ToString(),
                ["fresh"] = fresh.ToString(),
                ["skipped"] = skipped.ToString(),
                ["started_at_utc"] = session.StartedAt.UtcDateTime.ToString("O"),
                ["finished_at_utc"] = session.FinishedAt.UtcDateTime.ToString("O"),
                ["duration_seconds"] = Math.Max(0, (int)(session.FinishedAt - session.StartedAt).TotalSeconds).ToString(),
                ["updated_list"] = updatedItems.Count == 0
                    ? "none"
                    : string.Join(", ", updatedItems.Select(static item => item.Name)),
                ["failed_list"] = failedItems.Count == 0
                    ? "none"
                    : string.Join(", ", failedItems.Select(static item =>
                        string.IsNullOrWhiteSpace(item.Error)
                            ? item.Name
                            : $"{item.Name} ({item.Error})")),
                ["results"] = session.Results.Count == 0
                    ? "none"
                    : string.Join("\n", session.Results.Select(static item =>
                        string.IsNullOrWhiteSpace(item.Error)
                            ? $"- {item.Name}: {item.State}"
                            : $"- {item.Name}: {item.State} ({item.Error})"))
            };

            string normalizedTemplate = template.Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal);

            string withConditionals = ConditionalBlockRegex().Replace(normalizedTemplate, match =>
            {
                string condition = match.Groups[1].Value;
                string content = match.Groups[2].Value;
                return EvaluateCondition(condition, session, updatedItems.Count, failedItems.Count) ? content : string.Empty;
            });

            return TemplateVariableRegex().Replace(withConditionals, match =>
            {
                string key = match.Groups[1].Value;
                return variables.TryGetValue(key, out string? value) ? value : match.Value;
            });
        }

        private static bool EvaluateCondition(string condition, UpdateSessionResult session, int updatedCount, int failedCount)
        {
            return condition.Trim().ToLowerInvariant() switch
            {
                "updated" => updatedCount > 0,
                "failed" => failedCount > 0,
                "updated_and_failed" => updatedCount > 0 && failedCount > 0,
                "updated_only" => updatedCount > 0 && failedCount == 0,
                "failed_only" => failedCount > 0 && updatedCount == 0,
                "no_updates" => updatedCount == 0,
                "no_failures" => failedCount == 0,
                "changes" => updatedCount > 0 || failedCount > 0,
                "no_changes" => updatedCount == 0 && failedCount == 0,
                "scanned" => session.Scanned > 0,
                _ => false
            };
        }
    }
}