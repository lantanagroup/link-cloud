# syntax=docker/dockerfile:1
# See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM lantanagroup/aspnet8-run AS base

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUGET_XMLDOC_MODE=skip
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/Link.Authorization/Link.Authorization.csproj", "DotNet/Link.Authorization/"]
COPY ["DotNet/DataAcquisition.Domain/DataAcquisition.Domain.csproj", "DotNet/DataAcquisition.Domain/"]
COPY ["DotNet/DataAcquisition.AcquisitionWorker/DataAcquisition.AcquisitionWorker.csproj", "DotNet/DataAcquisition.AcquisitionWorker/"]
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    dotnet restore "DotNet/DataAcquisition.AcquisitionWorker/DataAcquisition.AcquisitionWorker.csproj" /p:RestorePackagesWithLockFile=false

COPY ["DotNet/Shared/", "DotNet/Shared/"]
COPY ["DotNet/Link.Authorization/", "DotNet/Link.Authorization/"]
COPY ["DotNet/DataAcquisition.Domain/", "DotNet/DataAcquisition.Domain/"]
COPY ["DotNet/DataAcquisition.AcquisitionWorker/", "DotNet/DataAcquisition.AcquisitionWorker/"]

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/DotNet/DataAcquisition.AcquisitionWorker"
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    extra=""; \
    if [ "$BUILD_CONFIGURATION" = "Release" ]; then extra="/p:DebugType=None /p:DebugSymbols=false"; fi; \
    dotnet publish "DataAcquisition.AcquisitionWorker.csproj" -c "$BUILD_CONFIGURATION" -o /app/publish /p:UseAppHost=false $extra

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataAcquisition.AcquisitionWorker.dll"]
