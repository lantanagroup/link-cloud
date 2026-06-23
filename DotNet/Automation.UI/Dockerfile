#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM lantanagroup/aspnet8-run AS base
WORKDIR /app
EXPOSE 5257

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
WORKDIR "/src/DotNet/Automation.UI"
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "Automation.UI.csproj"
RUN dotnet build "Automation.UI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "Automation.UI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Automation.UI.dll"]
