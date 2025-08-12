#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM lantanagroup/aspnet8-run AS base
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/DataAcquisition.AcquisitionWorker/DataAcquisition.AcquisitionWorker.csproj", "DotNet/DataAcquisition.AcquisitionWorker/"]
COPY ["DotNet/DataAcquisition.Domain/DataAcquisition.Domain.csproj", "DotNet/DataAcquisition.Domain/"]
RUN dotnet restore "DotNet/DataAcquisition.AcquisitionWorker/DataAcquisition.AcquisitionWorker.csproj" 
COPY . .
WORKDIR "/src/DotNet/DataAcquisition.AcquisitionWorker"
RUN dotnet build "DataAcquisition.AcquisitionWorker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DataAcquisition.AcquisitionWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataAcquisition.AcquisitionWorker.dll"]
