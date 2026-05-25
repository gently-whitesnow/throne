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

# git + gh CLI runtime dependency.
# gh приходит из официального apt-репозитория GitHub (в Debian main его нет).
# Источник: https://github.com/cli/cli/blob/trunk/docs/install_linux.md
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends ca-certificates curl gnupg git openssh-client; \
    install -d -m 0755 /etc/apt/keyrings; \
    curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
        | gpg --dearmor -o /etc/apt/keyrings/githubcli-archive-keyring.gpg; \
    chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg; \
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" \
        > /etc/apt/sources.list.d/github-cli.list; \
    apt-get update; \
    apt-get install -y --no-install-recommends gh; \
    apt-get purge -y --auto-remove curl gnupg; \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app .
EXPOSE 5008
ENTRYPOINT ["dotnet", "Throne.Api.dll"]
