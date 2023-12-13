#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["DataAcquisition/DataAcquisition.csproj", "DataAcquisition/"]
COPY ["DataAcquisition/nuget.config", "DataAcquisition/"]
RUN dotnet restore "DataAcquisition/DataAcquisition.csproj"  --configfile "DataAcquisition/nuget.config"
COPY . .
WORKDIR "/src/DataAcquisition"
RUN dotnet build "DataAcquisition.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DataAcquisition.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apk add --no-cache aspnetcore7-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataAcquisition.dll"]
