FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props MTL.V2.slnx ./
COPY src/MTL.Domain/MTL.Domain.csproj src/MTL.Domain/
COPY src/MTL.Infrastructure/MTL.Infrastructure.csproj src/MTL.Infrastructure/
COPY src/MTL.Api/MTL.Api.csproj src/MTL.Api/
RUN dotnet restore src/MTL.Api/MTL.Api.csproj

COPY src/MTL.Domain src/MTL.Domain
COPY src/MTL.Infrastructure src/MTL.Infrastructure
COPY src/MTL.Api src/MTL.Api
RUN dotnet publish src/MTL.Api/MTL.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /keys \
    && chown -R app:app /app /keys

COPY --from=build --chown=app:app /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "MTL.Api.dll"]

