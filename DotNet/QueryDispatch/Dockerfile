#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-amd64 AS base
RUN apt-get update && apt-get install -y curl
USER 1001
WORKDIR /app
EXPOSE 80
EXPOSE 443

ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/QueryDispatch/QueryDispatch.csproj", "DotNet/QueryDispatch/"]
RUN dotnet restore "DotNet/QueryDispatch/QueryDispatch.csproj"
COPY . .
WORKDIR "/src/DotNet/QueryDispatch"
RUN dotnet build "QueryDispatch.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "QueryDispatch.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QueryDispatch.dll"]
