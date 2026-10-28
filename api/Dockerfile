# syntax=docker/dockerfile:1.7

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /source

# Copy props and package manifest for optimized layer caching
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Identity.Api/Identity.Api.csproj src/Identity.Api/

# Restore NuGet packages
RUN dotnet restore src/Identity.Api/Identity.Api.csproj

# Copy API source and publish
COPY src/Identity.Api/ src/Identity.Api/
RUN dotnet publish src/Identity.Api/Identity.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Hardened Runtime Container
# Chiseled Ubuntu contains no shell, no package manager, runs non-root as 'app' (UID 1654)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_ENVIRONMENT=Production

COPY --from=build --chown=app:app /app/publish .

# Run as non-root user
USER app

EXPOSE 8080

ENTRYPOINT ["dotnet", "Identity.Api.dll"]
