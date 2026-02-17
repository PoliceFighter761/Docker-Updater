using DockerUpdater.Notifications;
using DockerUpdater.Worker.Docker;
using DockerUpdater.Worker.Options;
using DockerUpdater.Worker.Scheduling;
using DockerUpdater.Worker.Update;
using DockerUpdater.Worker;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

UpdaterOptions options = UpdaterOptions.LoadFromEnvironment();
IReadOnlyList<string> validationErrors = OptionsValidator.Validate(options);
if (validationErrors.Count > 0)
{
	foreach (string error in validationErrors)
	{
		Console.Error.WriteLine(error);
	}

	throw new InvalidOperationException("Invalid configuration. See errors above.");
}

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(new NotificationOptions(options.NotificationUrl, options.DiscordWebhookUrl, options.DiscordMessageTemplate));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IDockerClientFactory, DockerClientFactory>();
builder.Services.AddSingleton<RegistryAuthResolver>();
builder.Services.AddSingleton<ContainerRecreator>();
builder.Services.AddSingleton<ContainerSelectionPolicy>();
builder.Services.AddSingleton<UpdateCoordinator>();

builder.Services.AddSingleton<IRunScheduler, RunScheduler>();

builder.Services.AddHttpClient<DiscordNotifier>();
builder.Services.AddSingleton<INotifier, DiscordNotifier>();

builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
