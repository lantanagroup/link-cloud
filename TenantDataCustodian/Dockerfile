#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["TenantDataCustodian/TenantDataCustodian.csproj", "TenantDataCustodian/"]
COPY ["TenantDataCustodian/nuget.config", "TenantDataCustodian/"]
RUN dotnet restore "TenantDataCustodian/TenantDataCustodian.csproj" --configfile "TenantDataCustodian/nuget.config"
COPY . .
WORKDIR "/src/TenantDataCustodian"
RUN dotnet build "TenantDataCustodian.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TenantDataCustodian.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TenantDataCustodian.dll"]