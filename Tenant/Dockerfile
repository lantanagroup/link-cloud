#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Tenant/Tenant.csproj", "Tenant/"]
COPY ["Tenant/nuget.config", "Tenant/"]
RUN dotnet restore "Tenant/Tenant.csproj" --configfile "Tenant/nuget.config"
COPY . .
WORKDIR "/src/Tenant"
RUN dotnet build "Tenant.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Tenant.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
RUN apk add --no-cache aspnetcore7-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tenant.dll"]
