# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Packages.props ./
COPY DotNet/Onboarding.Chat/Onboarding.Chat.csproj DotNet/Onboarding.Chat/packages.lock.json DotNet/Onboarding.Chat/
RUN dotnet restore DotNet/Onboarding.Chat/Onboarding.Chat.csproj --locked-mode
COPY DotNet/Onboarding.Chat/ DotNet/Onboarding.Chat/
RUN dotnet publish DotNet/Onboarding.Chat/Onboarding.Chat.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Onboarding.Chat.dll"]
