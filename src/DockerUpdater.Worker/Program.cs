using DockerUpdater.Notifications;
using DockerUpdater.Worker.Docker;
using DockerUpdater.Worker.Options;
using DockerUpdater.Worker.Scheduling;
using DockerUpdater.Worker.Update;
using Quartz;

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

builder.Services.AddSingleton<IDockerClientFactory, DockerClientFactory>();
builder.Services.AddSingleton<RegistryAuthResolver>();
builder.Services.AddSingleton<ContainerRecreator>();
builder.Services.AddSingleton<ContainerSelectionPolicy>();
builder.Services.AddSingleton<UpdateCoordinator>();

builder.Services.AddHttpClient<DiscordNotifier>();
builder.Services.AddSingleton<INotifier, DiscordNotifier>();

builder.Services.AddQuartz(q =>
{
	JobKey jobKey = new("update-job");
	q.AddJob<UpdateJob>(j => j.WithIdentity(jobKey));

	if (!string.IsNullOrWhiteSpace(options.Schedule))
	{
		TimeZoneInfo timeZone = TimeZoneResolver.Resolve(options.TimeZone);
		q.AddTrigger(t => t
			.ForJob(jobKey)
			.WithIdentity("update-cron-trigger")
			.WithCronSchedule(options.Schedule, x => x
				.InTimeZone(timeZone)
				.WithMisfireHandlingInstructionFireAndProceed())
			.StartNow());
	}
	else
	{
		q.AddTrigger(t => t
			.ForJob(jobKey)
			.WithIdentity("update-interval-trigger")
			.WithSimpleSchedule(x => x
				.WithIntervalInSeconds(options.PollIntervalSeconds)
				.RepeatForever()
				.WithMisfireHandlingInstructionNextWithRemainingCount())
			.StartNow());
	}
});

builder.Services.AddQuartzHostedService(q =>
{
	q.WaitForJobsToComplete = true;
});

IHost host = builder.Build();
host.Run();
