FROM mcr.microsoft.com/dotnet/sdk:8.0 AS base
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/Terminology/Terminology.csproj", "DotNet/Terminology/"]
COPY ["DotNet/Link.Authorization/Link.Authorization.csproj", "DotNet/Link.Authorization/"]
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "DotNet/Terminology/Terminology.csproj"
COPY . .
WORKDIR "/src/DotNet/Terminology"
RUN dotnet build "Terminology.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "Terminology.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Terminology.dll"]
