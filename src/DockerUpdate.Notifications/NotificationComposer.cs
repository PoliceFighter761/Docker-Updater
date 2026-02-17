using System.Text;
using DockerUpdater.Shared;

namespace DockerUpdater.Notifications
{
    public static class NotificationComposer
    {
        public static string Compose(UpdateSessionResult session)
        {
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
    }
}