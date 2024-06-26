#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "Shared/"]
COPY ["DotNet/Link.Authorization/Link.Authorization.csproj", "Link.Authorization/"]
COPY ["DotNet/LinkAdmin.BFF/LinkAdmin.BFF.csproj", "LinkAdmin.BFF/"]
RUN dotnet restore "./LinkAdmin.BFF/./LinkAdmin.BFF.csproj"

COPY ["DotNet/Shared/", "Shared/"]
COPY ["DotNet/Link.Authorization/", "Link.Authorization/"]
COPY ["DotNet/LinkAdmin.BFF/", "LinkAdmin.BFF/"]

RUN dotnet build "./LinkAdmin.BFF/LinkAdmin.BFF.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./LinkAdmin.BFF/LinkAdmin.BFF.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LinkAdmin.BFF.dll"]