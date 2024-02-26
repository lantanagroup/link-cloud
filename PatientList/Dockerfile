#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PatientList/PatientList.csproj", "PatientList/"]
COPY ["PatientList/nuget.config", "PatientList/"]
RUN dotnet restore "PatientList/PatientList.csproj" --configfile "PatientList/nuget.config"
COPY . .
WORKDIR "/src/PatientList"
RUN dotnet build "PatientList.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PatientList.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
RUN apk add --no-cache aspnetcore6-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PatientList.dll"]
