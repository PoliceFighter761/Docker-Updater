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

            bool? labelEnabled = container.Labels.TryGetValue(LabelNames.Enable, out string? raw)
                && bool.TryParse(raw?.Trim(), out bool parsed)
                ? parsed
                : null;

            if (options.LabelEnable)
            {
                return labelEnabled == true;
            }

            if (labelEnabled == false)
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


    }
}