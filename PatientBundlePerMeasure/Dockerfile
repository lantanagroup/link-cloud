#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["PatientBundlePerMeasure/PatientBundlePerMeasure.csproj", "PatientBundlePerMeasure/"]
COPY ["PatientBundlePerMeasure/nuget.config", "PatientBundlePerMeasure/"]
RUN dotnet restore "PatientBundlePerMeasure/PatientBundlePerMeasure.csproj" --configfile "PatientBundlePerMeasure/nuget.config"
COPY . .
WORKDIR "/src/PatientBundlePerMeasure"
RUN dotnet build "PatientBundlePerMeasure.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PatientBundlePerMeasure.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apk add --no-cache aspnetcore6-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PatientBundlePerMeasure.dll"]
