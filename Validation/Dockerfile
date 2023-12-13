#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /src
COPY ["Validation/Validation.csproj", "Validation/"]
COPY ["Validation/nuget.config", "Validation/"]
RUN dotnet restore "Validation/Validation.csproj"  --configfile "Validation/nuget.config"
COPY . .
WORKDIR "/src/Validation"
RUN dotnet build "Validation.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Validation.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
RUN apk add --no-cache aspnetcore6-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Validation.dll"]
