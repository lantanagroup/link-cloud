FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-amd64 AS base
# curl is required by the compose health check.
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/Link.Authorization/Link.Authorization.csproj", "DotNet/Link.Authorization/"]
COPY ["DotNet/MockDmrpApi/MockDmrpApi.csproj", "DotNet/MockDmrpApi/"]
COPY ["Directory.Packages.props", "./"]
# Also pulls NSwag.MSBuild, which the build below needs to generate the API contract.
RUN dotnet restore "DotNet/MockDmrpApi/MockDmrpApi.csproj"
COPY . .
WORKDIR "/src/DotNet/MockDmrpApi"
# obj/ is excluded by .dockerignore, so the generated controller and DTOs are produced
# here from Contracts/dmrp-openapi.yaml rather than copied in. A build that cannot reach
# the spec or the generator fails here rather than shipping a stale contract.
RUN dotnet build "MockDmrpApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "MockDmrpApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MockDmrpApi.dll"]
