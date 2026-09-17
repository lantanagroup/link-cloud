# syntax=docker/dockerfile:1
# See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM lantanagroup/aspnet8-run AS base
WORKDIR /app
EXPOSE 5257

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUGET_XMLDOC_MODE=skip
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/Link.Authorization/Link.Authorization.csproj", "DotNet/Link.Authorization/"]
COPY ["DotNet/LinkSdk/LinkSdk.csproj", "DotNet/LinkSdk/"]
COPY ["DotNet/Automation/Automation.csproj", "DotNet/Automation/"]
COPY ["DotNet/Automation.Link/Automation.Link.csproj", "DotNet/Automation.Link/"]
COPY ["DotNet/Normalization.Engine/Normalization.Engine.csproj", "DotNet/Normalization.Engine/"]
COPY ["DotNet/Automation.UI/Automation.UI.csproj", "DotNet/Automation.UI/"]
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    dotnet restore "DotNet/Automation.UI/Automation.UI.csproj" /p:RestorePackagesWithLockFile=false

COPY ["DotNet/Shared/", "DotNet/Shared/"]
COPY ["DotNet/Link.Authorization/", "DotNet/Link.Authorization/"]
COPY ["DotNet/LinkSdk/", "DotNet/LinkSdk/"]
COPY ["DotNet/Automation/", "DotNet/Automation/"]
COPY ["DotNet/Automation.Link/", "DotNet/Automation.Link/"]
COPY ["DotNet/Normalization.Engine/", "DotNet/Normalization.Engine/"]
COPY ["DotNet/Automation.UI/", "DotNet/Automation.UI/"]

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/DotNet/Automation.UI"
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    extra=""; \
    if [ "$BUILD_CONFIGURATION" = "Release" ]; then extra="/p:DebugType=None /p:DebugSymbols=false"; fi; \
    dotnet publish "Automation.UI.csproj" -c "$BUILD_CONFIGURATION" -o /app/publish /p:UseAppHost=false $extra

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Automation.UI.dll"]
