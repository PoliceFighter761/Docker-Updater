FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore DockerUpdater.slnx
RUN dotnet publish src/DockerUpdater.Worker/DockerUpdater.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
VOLUME /data
ENTRYPOINT ["dotnet", "DockerUpdater.Worker.dll"]