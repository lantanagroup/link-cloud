#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MeasureEval/MeasureEval.csproj", "MeasureEval/"]
COPY ["MeasureEval/nuget.config", "MeasureEval/"]
RUN dotnet restore "MeasureEval/MeasureEval.csproj" --configfile "MeasureEval/nuget.config"
COPY . .
WORKDIR "/src/MeasureEval"
RUN dotnet build "MeasureEval.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MeasureEval.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apk add --no-cache aspnetcore6-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MeasureEval.dll"]
