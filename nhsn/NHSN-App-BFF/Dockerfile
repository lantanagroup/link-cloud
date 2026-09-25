FROM lantanagroup/aspnet8-run AS base
WORKDIR /app
EXPOSE 8079

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/Link.Authorization/Link.Authorization.csproj", "DotNet/Link.Authorization/"]
COPY ["DotNet/LinkSdk/LinkSdk.csproj", "DotNet/LinkSdk/"]
COPY ["nhsn/NHSN-App-BFF/NHSN-App-BFF.csproj", "nhsn/NHSN-App-BFF/"]
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "nhsn/NHSN-App-BFF/NHSN-App-BFF.csproj"

COPY ["DotNet/Shared/", "DotNet/Shared/"]
COPY ["DotNet/Link.Authorization/", "DotNet/Link.Authorization/"]
COPY ["DotNet/LinkSdk/", "DotNet/LinkSdk/"]
COPY ["nhsn/NHSN-App-BFF/", "nhsn/NHSN-App-BFF/"]

RUN dotnet build "nhsn/NHSN-App-BFF/NHSN-App-BFF.csproj" -c ${BUILD_CONFIGURATION} -o /app/build

FROM build AS publish
RUN dotnet publish "nhsn/NHSN-App-BFF/NHSN-App-BFF.csproj" -c ${BUILD_CONFIGURATION} -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NHSN-App-BFF.dll"]