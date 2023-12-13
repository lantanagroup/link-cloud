#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Notification/Notification.csproj", "Notification/"]
COPY ["Notification/nuget.config", "Notification/"]
RUN dotnet restore "Notification/Notification.csproj" --configfile "Notification/nuget.config"
COPY . .
WORKDIR "/src/Notification"
RUN dotnet build "Notification.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Notification.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apk add --no-cache aspnetcore7-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Notification.dll"]
#
