# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src ./src

RUN dotnet restore src/Throne.Api/Throne.Api.csproj
RUN dotnet publish src/Throne.Api/Throne.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 5008
ENTRYPOINT ["dotnet", "Throne.Api.dll"]
