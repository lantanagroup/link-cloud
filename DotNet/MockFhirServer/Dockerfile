# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-amd64 AS base
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUGET_XMLDOC_MODE=skip
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["nuget.config", "./"]
COPY ["local-thetis-nupkgs/", "local-thetis-nupkgs/"]
COPY ["DotNet/Automation/Automation.csproj", "DotNet/Automation/"]
COPY ["DotNet/MockFhirServer/MockFhirServer.csproj", "DotNet/MockFhirServer/"]
ARG FEED_ACCESSTOKEN
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    if ls local-thetis-nupkgs/*.nupkg >/dev/null 2>&1; then \
      echo "Using local Thetis nupkgs"; \
      rm -rf /root/.nuget/packages/lantanagroup.thetis.generation.abstractions \
             /root/.nuget/packages/lantanagroup.thetis.generation.engine; \
      dotnet nuget update source Shared_BOTW_Feed \
        --source /src/local-thetis-nupkgs \
        --configfile nuget.config; \
    elif [ -n "$FEED_ACCESSTOKEN" ]; then \
      dotnet nuget update source Shared_BOTW_Feed \
        --username docker \
        --password "$FEED_ACCESSTOKEN" \
        --store-password-in-clear-text \
        --configfile nuget.config; \
    else \
      echo "No local Thetis nupkgs and FEED_ACCESSTOKEN is empty; restore of LantanaGroup.Thetis.* will fail."; \
    fi; \
    dotnet restore "DotNet/MockFhirServer/MockFhirServer.csproj" /p:RestorePackagesWithLockFile=false

COPY ["DotNet/Automation/", "DotNet/Automation/"]
COPY ["DotNet/MockFhirServer/", "DotNet/MockFhirServer/"]

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/DotNet/MockFhirServer"
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    extra=""; \
    if [ "$BUILD_CONFIGURATION" = "Release" ]; then extra="/p:DebugType=None /p:DebugSymbols=false"; fi; \
    dotnet publish "MockFhirServer.csproj" -c "$BUILD_CONFIGURATION" -o /app/publish /p:UseAppHost=false $extra

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MockFhirServer.dll"]
