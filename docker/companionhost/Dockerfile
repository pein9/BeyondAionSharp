# Companion host image (docs/companion-live-host.md). Build context = repo root.
#   docker build -f docker/companionhost/Dockerfile .
# The host is a client program: it plays the companions players ask for with .companion, on the seat accounts
# companion1, companion2 and so on. It reaches the login and game servers as a client does and writes to no database.
# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ ./src/
COPY tests/Aion.Bots/ ./tests/Aion.Bots/
COPY tools/Aion.LiveBots/ ./tools/Aion.LiveBots/
RUN dotnet publish tools/Aion.LiveBots/Aion.LiveBots.csproj -c Release -o /publish -p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/runtime:10.0
# The program finds its data from its working directory: parity-artifacts/e2e (its scenarios and the bots' contracts)
# and the game server's static data, geometry and navmeshes beside it.
WORKDIR /repo
COPY game-server/config/ ./game-server/config/
COPY game-server/data/   ./game-server/data/
COPY parity-artifacts/e2e/ ./parity-artifacts/e2e/
COPY --from=build /publish/ /app/
COPY docker/companionhost/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
