#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Submission/Submission.csproj", "Submission/"]
COPY ["Submission/nuget.config", "Submission/"]
RUN dotnet restore "Submission/Submission.csproj"  --configfile "Submission/nuget.config"
COPY . .
WORKDIR "/src/Submission"
RUN dotnet build "Submission.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Submission.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
#RUN apk add --no-cache aspnetcore7-runtime
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Submission.dll"]
