# Same LIVE bot code; optional Linux load generator, not another server.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ ./src/
COPY tests/Aion.Bots/ ./tests/Aion.Bots/
COPY tools/Aion.LiveBots/ ./tools/Aion.LiveBots/
RUN dotnet publish tools/Aion.LiveBots/Aion.LiveBots.csproj -c Release -o /publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
ARG AION_BOT_SOURCE_REVISION
LABEL org.opencontainers.image.revision=$AION_BOT_SOURCE_REVISION
COPY --from=build /publish/ /app/
WORKDIR /workspace
# The owner invokes dotnet through compose exec, checks its exit code, then stops
# this container after the watcher drains. An unexpected container death still fails.
ENTRYPOINT ["sleep", "infinity"]
