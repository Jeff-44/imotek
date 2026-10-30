# syntax=docker/dockerfile:1

# ---- Build ------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only, so the layer is cached between code changes.
COPY global.json dotnet-tools.json Imotek.slnx ./
COPY src/Imotek.Domain/Imotek.Domain.csproj src/Imotek.Domain/
COPY src/Imotek.Application/Imotek.Application.csproj src/Imotek.Application/
COPY src/Imotek.Infrastructure/Imotek.Infrastructure.csproj src/Imotek.Infrastructure/
COPY src/Imotek.API/Imotek.API.csproj src/Imotek.API/
RUN dotnet restore src/Imotek.API/Imotek.API.csproj && dotnet tool restore

COPY src/ src/
RUN dotnet publish src/Imotek.API/Imotek.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# EF Core migration bundle: a single executable that applies pending migrations.
RUN dotnet ef migrations bundle \
      --project src/Imotek.Infrastructure --startup-project src/Imotek.API \
      --configuration Release --output /app/efbundle --force

# ---- Migrator (one-shot job, run before the API) ----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=build /app/efbundle ./efbundle
COPY --from=build /app/publish/appsettings.json ./
USER $APP_UID
# The connection string comes from ConnectionStrings__ImotekDb.
ENTRYPOINT ["./efbundle"]

# ---- API --------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /app/publish .

# Writable directories for uploads and Data Protection keys, owned by the non-root user.
RUN mkdir -p /app/data/uploads /app/data/keys && chown -R $APP_UID:$APP_UID /app/data

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    FileStorage__RootPath=/app/data/uploads \
    DataProtection__KeysPath=/app/data/keys

USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=3 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && head -1 <&3 | grep -q " 200 "'

ENTRYPOINT ["dotnet", "Imotek.API.dll"]
