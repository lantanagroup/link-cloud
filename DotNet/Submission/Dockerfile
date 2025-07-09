#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM lantanagroup/aspnet8-run AS base
USER root
RUN mkdir -p /data/submission
VOLUME /data/submission
RUN chown -R 1654:1654 /data/submission
USER 1654

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DotNet/Shared/Shared.csproj", "DotNet/Shared/"]
COPY ["DotNet/Submission/Submission.csproj", "DotNet/Submission/"]
COPY ["DotNet/Link.Authorization/Link.Authorization.csproj", "DotNet/Link.Authorization/"]
RUN dotnet restore "DotNet/Submission/Submission.csproj"
COPY . .
WORKDIR "/src/DotNet/Submission"
RUN dotnet build "Submission.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Submission.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Submission.dll"]
