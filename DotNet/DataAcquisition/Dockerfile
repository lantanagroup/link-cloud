#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-amd64 AS base
WORKDIR /app
ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/DataAcquisition/DataAcquisition.csproj", "DotNet/DataAcquisition/"]
COPY ["DotNet/DataAcquisition.Domain/DataAcquisition.Domain.csproj", "DotNet/DataAcquisition.Domain/"]
RUN dotnet restore "DotNet/DataAcquisition/DataAcquisition.csproj" 
COPY . .
WORKDIR "/src/DotNet/DataAcquisition"
RUN dotnet build "DataAcquisition.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DataAcquisition.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataAcquisition.dll"]
