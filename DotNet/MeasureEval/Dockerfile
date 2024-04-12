#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotNet/MeasureEval/MeasureEval.csproj", "DotNet/MeasureEval/"]
COPY ["DotNet/MeasureEval/nuget.config", "DotNet/MeasureEval/"]
RUN dotnet restore "DotNet/MeasureEval/MeasureEval.csproj" --configfile "DotNet/MeasureEval/nuget.config"
COPY . .
WORKDIR "/src/DotNet/MeasureEval"
RUN dotnet build "MeasureEval.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MeasureEval.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
#RUN apk add --no-cache aspnetcore6-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MeasureEval.dll"]
