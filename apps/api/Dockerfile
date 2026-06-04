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

# Тулчейн встроенного агент-терминала и clone'а репозиториев. Всё исполняется
# ВНУТРИ контейнера, но поверх хостового состояния, которое docker-compose пробрасывает
# bind-mount'ами (workspaces, авторизация gh/ssh, ~/.claude). Бинарники Linux-нативные и
# не могут быть «хостовыми»: на macOS-хосте это Mach-O, который тут не запустится. Поэтому
# «хостовый тулчейн» = хостовое состояние/креды поверх контейнерного тулчейна, а не хостовые
# бинарники.
# - gh приходит из официального apt-репозитория GitHub (в Debian main его нет).
#   Источник: https://github.com/cli/cli/blob/trunk/docs/install_linux.md
# - tmux держит PTY вокруг `claude` для embedded-terminal'а (ADR-0026 § 2).
# - claude — агент, которого поднимает терминал (`tmux new -ADs … claude "…"`); ставим
#   официальным скриптом Anthropic (glibc-нативный бинарь в ~/.local/bin, без Node).
#   Авторизация берётся из примонтированного ~/.claude/.credentials.json — на macOS его
#   надо выгрузить из Keychain (см. readme, тот же кейс, что и с gh).
# VS Code (`code`) намеренно не ставим: это desktop-GUI хоста, в контейнере бесполезен —
# capability `vscode` отрапортует `detected=false`, фронт скроет кнопки.
ENV PATH="/root/.local/bin:${PATH}"
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends ca-certificates curl gnupg git openssh-client tmux; \
    install -d -m 0755 /etc/apt/keyrings; \
    curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
        | gpg --dearmor -o /etc/apt/keyrings/githubcli-archive-keyring.gpg; \
    chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg; \
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" \
        > /etc/apt/sources.list.d/github-cli.list; \
    apt-get update; \
    apt-get install -y --no-install-recommends gh; \
    curl -fsSL https://claude.ai/install.sh | bash; \
    apt-get purge -y --auto-remove curl gnupg; \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app .
EXPOSE 5008
ENTRYPOINT ["dotnet", "Throne.Api.dll"]
