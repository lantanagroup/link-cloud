# syntax=docker/dockerfile:1
# Console tool, not a web service: runtime image, no ASP.NET, no private NuGet feed.

FROM mcr.microsoft.com/dotnet/runtime:8.0-jammy AS base
WORKDIR /app
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    SIM_DATA_DIR=/data
VOLUME ["/data"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUGET_XMLDOC_MODE=skip
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["nuget.config", "./"]
COPY ["DotNet/PatientFlowSimulator/PatientFlowSimulator.csproj", "DotNet/PatientFlowSimulator/"]
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    dotnet restore "DotNet/PatientFlowSimulator/PatientFlowSimulator.csproj" /p:RestorePackagesWithLockFile=false

COPY ["DotNet/PatientFlowSimulator/", "DotNet/PatientFlowSimulator/"]

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/DotNet/PatientFlowSimulator"
RUN --mount=type=cache,id=nuget,sharing=locked,target=/root/.nuget/packages \
    dotnet publish "PatientFlowSimulator.csproj" -c "$BUILD_CONFIGURATION" -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PatientFlowSimulator.dll"]
CMD ["run"]
