#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/Submission/Submission.csproj", "DotNet/Submission/"]
RUN dotnet restore "DotNet/Submission/Submission.csproj"
COPY . .
WORKDIR "/src/DotNet/Submission"
RUN dotnet build "Submission.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Submission.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Submission.dll"]
