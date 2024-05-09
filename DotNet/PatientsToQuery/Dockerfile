#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-amd64 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/PatientsToQuery/PatientsToQuery.csproj", "DotNet/PatientsToQuery/"]
RUN dotnet restore "DotNet/PatientsToQuery/PatientsToQuery.csproj"
COPY . .
WORKDIR "/src/DotNet/PatientsToQuery"
RUN dotnet build "PatientsToQuery.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PatientsToQuery.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apt update && apt install -y libgflags-dev libsnappy-dev
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PatientsToQuery.dll"]
