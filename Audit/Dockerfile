#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
RUN apk add --no-cache \
    icu-data-full \
    icu-libs

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Audit/Audit.csproj", "Audit/"]
COPY ["Audit/nuget.config", "Audit/"]
RUN dotnet restore "Audit/Audit.csproj" --configfile "Audit/nuget.config"
COPY . .
WORKDIR "/src/Audit"
RUN dotnet build "Audit.csproj" -c Release -o /app/build
ENV ASPNETCORE_HTTP_PORTS 80

FROM build AS publish
RUN dotnet publish "Audit.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Audit.dll"]
#
#
