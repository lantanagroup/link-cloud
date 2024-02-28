#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PatientsToQuery/PatientsToQuery.csproj", "PatientsToQuery/"]
COPY ["PatientsToQuery/nuget.config", "PatientsToQuery/"]
RUN dotnet restore "PatientsToQuery/PatientsToQuery.csproj" --configfile "PatientsToQuery/nuget.config"
COPY . .
WORKDIR "/src/PatientsToQuery"
RUN dotnet build "PatientsToQuery.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PatientsToQuery.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
#RUN apk add --no-cache rocksdb libstdc++ aspnetcore6-runtime
RUN ln -s /usr/lib/librocksdb.so.7 /usr/lib/librocksdb.so
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PatientsToQuery.dll"]
