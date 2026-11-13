# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Packages.props ./
COPY DotNet/Onboarding.Mcp/Onboarding.Mcp.csproj DotNet/Onboarding.Mcp/packages.lock.json DotNet/Onboarding.Mcp/
COPY DotNet/LinkSdk/LinkSdk.csproj DotNet/LinkSdk/packages.lock.json DotNet/LinkSdk/
COPY DotNet/Shared/Shared.csproj DotNet/Shared/packages.lock.json DotNet/Shared/
COPY DotNet/Link.Authorization/Link.Authorization.csproj DotNet/Link.Authorization/packages.lock.json DotNet/Link.Authorization/
RUN dotnet restore DotNet/Onboarding.Mcp/Onboarding.Mcp.csproj --locked-mode
COPY DotNet/Onboarding.Mcp/ DotNet/Onboarding.Mcp/
COPY DotNet/LinkSdk/ DotNet/LinkSdk/
COPY DotNet/Shared/ DotNet/Shared/
COPY DotNet/Link.Authorization/ DotNet/Link.Authorization/
RUN dotnet publish DotNet/Onboarding.Mcp/Onboarding.Mcp.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Onboarding.Mcp.dll"]
