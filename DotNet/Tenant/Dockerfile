#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM lantanagroup/aspnet8-run AS base
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/Tenant/Tenant.csproj", "DotNet/Tenant/"]
RUN dotnet restore "DotNet/Tenant/Tenant.csproj"
COPY . .
WORKDIR "/src/DotNet/Tenant"
RUN dotnet build "Tenant.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "Tenant.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tenant.dll"]
