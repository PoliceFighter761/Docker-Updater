namespace DockerUpdater.Worker.Docker
{
    public readonly record struct ImageReference(string Registry, string Repository, string Tag)
    {
        private const string DefaultRegistry = "index.docker.io";
        private const string DefaultTag = "latest";

        public static ImageReference Parse(string imageName)
        {
            string withoutDigest = imageName.Split('@', 2, StringSplitOptions.TrimEntries)[0];

            int lastSlash = withoutDigest.LastIndexOf('/');
            int lastColon = withoutDigest.LastIndexOf(':');

            string repository;
            string tag;
            if (lastColon > lastSlash)
            {
                repository = withoutDigest[..lastColon];
                tag = withoutDigest[(lastColon + 1)..];
            }
            else
            {
                repository = withoutDigest;
                tag = DefaultTag;
            }

            string firstSegment = repository.Split('/', 2, StringSplitOptions.TrimEntries)[0];

            bool hasExplicitRegistry = firstSegment.Contains('.', StringComparison.Ordinal)
                || firstSegment.Contains(':', StringComparison.Ordinal)
                || string.Equals(firstSegment, "localhost", StringComparison.OrdinalIgnoreCase);

            string registry = hasExplicitRegistry ? firstSegment : DefaultRegistry;

            return new ImageReference(registry, repository, tag);
        }
    }
}
