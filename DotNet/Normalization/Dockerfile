#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM lantanagroup/aspnet8-run AS base
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/Normalization/Normalization.csproj", "DotNet/Normalization/"]
RUN dotnet restore "DotNet/Normalization/Normalization.csproj"
COPY . .
WORKDIR "/src/DotNet/Normalization"
RUN dotnet build "Normalization.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Normalization.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Normalization.dll"]
#
