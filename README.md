# Docker Updater

Docker Updater is a standalone .NET Worker app that runs in Docker and updates running containers when newer images are available.

## Features

- Automatic image checks and container updates.
- Label-based container filtering using `com.dockerupdater.enable`.
- Interval or cron-based scheduling.
- Optional old-image cleanup after successful updates.
- Discord webhook notifications.
- Linux and Windows Docker host support.

## Environment variables

### Docker connectivity

- `DOCKER_HOST`
  - Purpose: Docker daemon endpoint used by the updater.
  - Default: `unix:///var/run/docker.sock` on Linux, `npipe://./pipe/docker_engine` on Windows.
  - Examples: `unix:///var/run/docker.sock`, `tcp://docker-host:2375`, `npipe://./pipe/docker_engine`.

- `DOCKER_TLS_VERIFY`
  - Purpose: Enables TLS verification for Docker daemon connections.
  - Values: `true|false` (`1|0`, `yes|no`, `on|off` also supported).
  - Default: `false`.

### Scheduling and execution

- `DOCKER_UPDATER_POLL_INTERVAL`
  - Purpose: Poll interval in seconds when cron scheduling is not used.
  - Type: positive integer.
  - Default: `86400`.

- `DOCKER_UPDATER_SCHEDULE`
  - Purpose: Cron schedule for checks.
  - Format: 6-field cron (includes seconds), e.g. `0 */5 * * * *`.
  - Constraint: mutually exclusive with explicitly setting `DOCKER_UPDATER_POLL_INTERVAL`.

- `DOCKER_UPDATER_RUN_ONCE`
  - Purpose: Runs one update session and exits.
  - Values: `true|false`.
  - Default: `false`.

- `TZ`
  - Purpose: Time zone for cron interpretation.
  - Default: `UTC`.
  - Example: `Europe/Berlin`, `UTC`.

### Container targeting and filtering

- `DOCKER_UPDATER_CONTAINERS`
  - Purpose: Restricts updates to specific container names.
  - Format: comma- or space-separated list.
  - Example: `api,web worker`.

- `DOCKER_UPDATER_LABEL_ENABLE`
  - Purpose: Only include containers with label `com.dockerupdater.enable=true`.
  - Values: `true|false`.
  - Default: `true`.

- `DOCKER_UPDATER_DISABLE_CONTAINERS`
  - Purpose: Excludes container names from updates.
  - Format: comma- or space-separated list.
  - Example: `db,redis`.

- `DOCKER_UPDATER_INCLUDE_STOPPED`
  - Purpose: Includes stopped/created containers in scan results.
  - Values: `true|false`.
  - Default: `false`.

- `DOCKER_UPDATER_REVIVE_STOPPED`
  - Purpose: Starts previously stopped containers after they are updated.
  - Values: `true|false`.
  - Constraint: requires `DOCKER_UPDATER_INCLUDE_STOPPED=true`.

### Update behavior

- `DOCKER_UPDATER_CLEANUP`
  - Purpose: Removes old image layers after successful updates.
  - Values: `true|false`.
  - Default: `false`.

- `DOCKER_UPDATER_TIMEOUT`
  - Purpose: Container stop timeout before force termination during replacement.
  - Formats: `30s`, `2m`, `1h`, or `hh:mm:ss`.
  - Default: `10s`.

### Notifications

- `DOCKER_UPDATER_DISCORD_WEBHOOK_URL`
  - Purpose: Explicit Discord webhook endpoint for session summaries.
  - Type: HTTPS URL.
  - Priority: preferred over `DOCKER_UPDATER_NOTIFICATION_URL`.

- `DOCKER_UPDATER_NOTIFICATION_URL`
  - Purpose: Generic webhook URL fallback (currently HTTPS webhook expected).
  - Type: HTTPS URL.

## Run with docker run (Linux)

```bash
docker run -d \
  --name docker-updater \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e DOCKER_UPDATER_POLL_INTERVAL=300 \
  -e DOCKER_UPDATER_LABEL_ENABLE=true \
  -e DOCKER_UPDATER_CLEANUP=true \
  docker-updater:dev
```

## Run with docker run (Windows host)

```powershell
docker run -d --name docker-updater `
  -e DOCKER_HOST=npipe://./pipe/docker_engine `
  -e DOCKER_UPDATER_POLL_INTERVAL=300 `
  -e DOCKER_UPDATER_LABEL_ENABLE=true `
  -e DOCKER_UPDATER_CLEANUP=true `
  docker-updater:dev
```

## Run with Compose

```bash
docker compose up -d --build
```

## Build and test locally

```bash
dotnet test
docker build -t docker-updater:dev .
```
