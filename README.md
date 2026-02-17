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

It is recommended to set these environment variables in a .ENV file or in a docker-compose.override.yml file.

### Docker connectivity

- `DOCKER_HOST`
  - Purpose: Docker daemon endpoint used by the updater.
  - Default: `unix:///var/run/docker.sock` on Linux, `npipe://./pipe/docker_engine` on Windows.
  - Examples: `unix:///var/run/docker.sock`, `tcp://docker-host:2375`, `npipe://./pipe/docker_engine`.

- `DOCKER_TLS_VERIFY`
  - Purpose: Enables TLS verification for Docker daemon connections.
  - Values: `true|false` (`1|0`, `yes|no`, `on|off` also supported).
  - Default: `false`.

- `DOCKER_CERT_PATH`
  - Purpose: Directory containing TLS certificates (ca.pem, cert.pem, key.pem) used when `DOCKER_TLS_VERIFY` is enabled.
  - Default: `~/.docker` or system default certificate locations.
  - Required when: `DOCKER_TLS_VERIFY` is `true` and using TLS-secured Docker daemon.

- `DOCKER_CONFIG`
  - Purpose: Directory containing Docker client auth config (`config.json`) used for private registry pulls.
  - Default: container user's `~/.docker`.
  - Recommended in Docker: mount host Docker config and set `DOCKER_CONFIG` to that mount path.

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

- `DOCKER_UPDATER_DISCORD_MESSAGE_TEMPLATE`
  - Purpose: Custom Discord message format for session notifications.
  - Type: text template with replacement variables.
  - Default: built-in summary format.
  - Newlines: use `\n` for line breaks when setting via environment variables.
  - Conditional blocks:
    - Syntax: `{{#if condition}}...{{/if}}`
    - Supported conditions: `updated`, `failed`, `updated_and_failed`, `updated_only`, `failed_only`, `changes`, `no_changes`, `no_updates`, `no_failures`.
  - Supported variables:
    - `{{scanned}}`, `{{updated}}`, `{{failed}}`, `{{fresh}}`, `{{skipped}}`
    - `{{started_at_utc}}`, `{{finished_at_utc}}`, `{{duration_seconds}}`
    - `{{updated_list}}`, `{{failed_list}}`, `{{results}}`
  - Example: `Run {{started_at_utc}} | scanned={{scanned}} updated={{updated}} failed={{failed}}\\n{{#if updated_only}}✅ Updated: {{updated_list}}\\n{{/if}}{{#if failed_only}}❌ Failed: {{failed_list}}\\n{{/if}}{{#if updated_and_failed}}⚠️ Updated: {{updated_list}} | Failed: {{failed_list}}\\n{{/if}}`

- `DOCKER_UPDATER_NOTIFICATION_URL`
  - Purpose: Generic webhook URL fallback (currently HTTPS webhook expected).
  - Type: HTTPS URL.

## Run with docker run (Linux)

```bash
docker run -d \
  --name docker-updater \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v $HOME/.docker:/host-docker-config:ro \
  -e DOCKER_CONFIG=/host-docker-config \
  -e DOCKER_UPDATER_POLL_INTERVAL=300 \
  -e DOCKER_UPDATER_LABEL_ENABLE=true \
  -e DOCKER_UPDATER_CLEANUP=true \
  docker-updater:dev
```

## Run with docker run (Windows host)

```powershell
docker run -d --name docker-updater `
  -v ${Env:USERPROFILE}\.docker:/host-docker-config:ro `
  -e DOCKER_CONFIG=/host-docker-config `
  -e DOCKER_HOST=npipe://./pipe/docker_engine `
  -e DOCKER_UPDATER_POLL_INTERVAL=300 `
  -e DOCKER_UPDATER_LABEL_ENABLE=true `
  -e DOCKER_UPDATER_CLEANUP=true `
  docker-updater:dev
```

## Run with Compose

```bash
docker compose up -d
```

## Build and test locally

```bash
dotnet test
docker build -t docker-updater:dev .
```
