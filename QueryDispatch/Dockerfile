#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["QueryDispatch/QueryDispatch.csproj", "QueryDispatch/"]
COPY ["QueryDispatch/nuget.config", "QueryDispatch/"]
RUN dotnet restore "QueryDispatch/QueryDispatch.csproj" --configfile "QueryDispatch/nuget.config"
COPY . .
WORKDIR "/src/QueryDispatch"
RUN dotnet build "QueryDispatch.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "QueryDispatch.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apk add --no-cache aspnetcore6-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QueryDispatch.dll"]
