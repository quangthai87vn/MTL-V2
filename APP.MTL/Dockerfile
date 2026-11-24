# ─── Build Stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY MTL.APP.csproj ./
RUN dotnet restore MTL.APP.csproj

COPY . ./
RUN dotnet publish MTL.APP.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:PublishTrimmed=false \
    -p:PublishSingleFile=false

# ─── Runtime Stage ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN groupadd --gid 1000 appgroup \
 && useradd --uid 1000 --gid appgroup --shell /bin/bash --create-home appuser
USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

ENTRYPOINT ["dotnet", "MTL.APP.dll"]
