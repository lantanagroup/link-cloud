#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# ARG ACCESS_TOKEN="mbygihet4uek5omhnuatzdxu5npiqz2rw3i6i7bgywaqwtshafaq"
# ARG ARTIFACTS_ENDPOINT="https://pkgs.dev.azure.com/lantanagroup/nhsnlink/_packaging/Shared_BOTW_Feed/nuget/v3/index.json"

# Configure the environment variables
# ENV NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED true
# ENV VSS_NUGET_EXTERNAL_FEED_ENDPOINTS "{\"endpointCredentials\": [{\"endpoint\":\"${ARTIFACTS_ENDPOINT}\", \"password\":\"${ACCESS_TOKEN}\"}]}"

FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Account/Account.csproj", "Account/"]
COPY ["Account/nuget.config", "Account/"]
RUN dotnet restore "Account/Account.csproj" --configfile "Account/nuget.config"
COPY . .
WORKDIR "/src/Account"
RUN dotnet build "Account.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Account.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apk add --no-cache aspnetcore7-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Account.dll"]

