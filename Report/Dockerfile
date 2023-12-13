#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Report/Report.csproj", "Report/"]
COPY ["Report/nuget.config", "Report/"]
RUN dotnet restore "Report/Report.csproj" --configfile "Report/nuget.config"
COPY . .
WORKDIR "/src/Report"
RUN dotnet build "Report.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Report.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
RUN apk add --no-cache aspnetcore7-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Report.dll"]
