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
COPY ["DotNet/Automation/Automation.csproj", "DotNet/Automation/"]
COPY ["DotNet/MockFhirServer/MockFhirServer.csproj", "DotNet/MockFhirServer/"]
COPY ["Scripts/docker-compose.restore-feed.sh", "Scripts/docker-compose.restore-feed.sh"]
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    --mount=type=secret,id=feed_accesstoken,required=false \
    sh Scripts/docker-compose.restore-feed.sh "DotNet/MockFhirServer/MockFhirServer.csproj"

COPY ["DotNet/Automation/", "DotNet/Automation/"]
COPY ["DotNet/MockFhirServer/", "DotNet/MockFhirServer/"]

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/DotNet/MockFhirServer"
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    extra=""; \
    if [ "$BUILD_CONFIGURATION" = "Release" ]; then extra="/p:DebugType=None /p:DebugSymbols=false"; fi; \
    dotnet publish "MockFhirServer.csproj" -c "$BUILD_CONFIGURATION" -o /app/publish --no-restore /p:UseAppHost=false $extra

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MockFhirServer.dll"]
