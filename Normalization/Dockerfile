#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
# EXPOSE 80
# EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Normalization/Normalization.csproj", "Normalization/"]
COPY ["Normalization/nuget.config", "Normalization/"]
RUN dotnet restore "Normalization/Normalization.csproj" --configfile "Normalization/nuget.config"
COPY . .
WORKDIR "/src/Normalization"
RUN dotnet build "Normalization.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Normalization.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
# RUN apk add --no-cache aspnetcore8.0-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Normalization.dll"]
#
