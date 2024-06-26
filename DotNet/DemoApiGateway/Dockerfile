#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app

ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotNet/Link.Authorization/Link.Authorization.csproj", "Link.Authorization/"]
COPY ["DotNet/Shared/Shared.csproj", "Shared/"]
COPY ["DotNet/DemoApiGateway/DemoApiGateway.csproj", "DemoApiGateway/"]
RUN dotnet restore "DemoApiGateway/DemoApiGateway.csproj"
COPY ["DotNet/Link.Authorization/", "Link.Authorization/"]
COPY ["DotNet/Shared/", "Shared/"]
COPY ["DotNet/DemoApiGateway/", "DemoApiGateway/"]
WORKDIR "/src/DemoApiGateway"
RUN dotnet build "DemoApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DemoApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DemoApiGateway.dll"]