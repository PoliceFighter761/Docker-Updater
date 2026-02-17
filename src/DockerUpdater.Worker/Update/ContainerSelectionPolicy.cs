using DockerUpdater.Worker.Options;

namespace DockerUpdater.Worker.Update
{
    public sealed class ContainerSelectionPolicy(UpdaterOptions options)
    {
        public bool ShouldMonitor(ContainerRef container)
        {
            string normalizedName = UpdaterOptions.NormalizeContainerName(container.Name);

            if (options.TargetContainers.Count > 0 && !options.TargetContainers.Contains(normalizedName))
            {
                return false;
            }

            if (options.DisableContainers.Contains(normalizedName))
            {
                return false;
            }

            string? labelValue = container.Labels.TryGetValue(LabelNames.Enable, out string? raw)
                ? raw
                : null;

            bool isEnabledLabelTrue = IsTrue(labelValue);
            bool isEnabledLabelFalse = IsFalse(labelValue);

            if (options.LabelEnable)
            {
                return isEnabledLabelTrue;
            }

            if (isEnabledLabelFalse)
            {
                return false;
            }

            if (!options.IncludeStopped &&
                (container.State.Equals("exited", StringComparison.OrdinalIgnoreCase)
                 || container.State.Equals("created", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return true;
        }

        private static bool IsTrue(string? value)
        {
            return value?.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
        }

        private static bool IsFalse(string? value)
        {
            return value?.Trim().ToLowerInvariant() is "0" or "false" or "no" or "off";
        }
    }
}