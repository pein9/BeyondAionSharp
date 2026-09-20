# LoginServer image. Build context = repo root.
# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
RUN dotnet publish src/Aion.LoginServer/Aion.LoginServer.csproj \
      -c Release -o /publish -p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS server
WORKDIR /app
# LoginServer resolves config by walking up from CWD (/app) to find <dir>/login-server/config.
COPY login-server/config/ ./login-server/config/
COPY --from=build /publish/ ./
COPY docker/loginserver/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
EXPOSE 2106
ENTRYPOINT ["/entrypoint.sh"]

# Only the isolated bot compose stack selects this target. Normal deployments
# still use the final runtime target below, with no diagnostic tools installed.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS diagnostic-tools
RUN dotnet tool install --tool-path /diagnostics dotnet-stack --version 10.0.745401

FROM server AS bot-diagnostics
COPY --from=diagnostic-tools /diagnostics/ /opt/aion-diagnostics/

FROM server AS runtime
