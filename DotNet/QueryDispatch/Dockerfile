#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM lantanagroup/aspnet8-run AS base
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
