# syntax=docker/dockerfile:1
# Build context is the repository root (see docker-compose.yml).
# We need access to specs/manifest/* outside apps/api.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY apps/api/Directory.Build.props apps/api/Directory.Packages.props apps/api/.editorconfig ./apps/api/
COPY apps/api/src ./apps/api/src
COPY specs/manifest ./specs/manifest

WORKDIR /src/apps/api
RUN dotnet restore src/Throne.Api/Throne.Api.csproj
RUN dotnet publish src/Throne.Api/Throne.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 5008
ENTRYPOINT ["dotnet", "Throne.Api.dll"]
