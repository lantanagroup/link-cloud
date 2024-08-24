#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# ARG ACCESS_TOKEN="mbygihet4uek5omhnuatzdxu5npiqz2rw3i6i7bgywaqwtshafaq"
# ARG ARTIFACTS_ENDPOINT="https://pkgs.dev.azure.com/lantanagroup/nhsnlink/_packaging/Shared_BOTW_Feed/nuget/v3/index.json"

# Configure the environment variables
# ENV NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED true
# ENV VSS_NUGET_EXTERNAL_FEED_ENDPOINTS "{\"endpointCredentials\": [{\"endpoint\":\"${ARTIFACTS_ENDPOINT}\", \"password\":\"${ACCESS_TOKEN}\"}]}"

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-amd64 AS base
USER 1001
WORKDIR /app

ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/Account/Account.csproj", "DotNet/Account/"]
COPY ["DotNet/Link.Authorization/Link.Authorization.csproj", "DotNet/Link.Authorization/"]
RUN dotnet restore "DotNet/Account/Account.csproj"
COPY . .
WORKDIR "/src/DotNet/Account"
RUN dotnet build "Account.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Account.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Account.dll"]

# test 3 - cherry pick a signed commit
# test 9 multiple signed and unsigned commits
# test 7 multiple signed and unsigned merge --no-ff