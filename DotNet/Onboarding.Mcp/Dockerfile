# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Packages.props ./
COPY DotNet/Onboarding.Mcp/Onboarding.Mcp.csproj DotNet/Onboarding.Mcp/packages.lock.json DotNet/Onboarding.Mcp/
RUN dotnet restore DotNet/Onboarding.Mcp/Onboarding.Mcp.csproj --locked-mode
COPY DotNet/Onboarding.Mcp/ DotNet/Onboarding.Mcp/
RUN dotnet publish DotNet/Onboarding.Mcp/Onboarding.Mcp.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Onboarding.Mcp.dll"]
