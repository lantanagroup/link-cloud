#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/Report/Report.csproj", "DotNet/Report/"]
RUN dotnet restore "DotNet/Report/Report.csproj"
COPY . .
WORKDIR "/src/DotNet/Report"
RUN dotnet build "Report.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Report.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Report.dll"]
