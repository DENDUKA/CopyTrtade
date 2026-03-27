# =============================================================================
# Multi-stage Dockerfile — CopyTrading ASP.NET Core 8.0
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: Restore — кэширует NuGet пакеты отдельным слоем
# Пересборка только при изменении .csproj файлов
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

COPY ["CopyTrading/CopyTrading.csproj", "CopyTrading/"]
COPY ["CopyTrading.Models/CopyTrading.Models.csproj", "CopyTrading.Models/"]
COPY ["CopyTriding.Test/CopyTrading.Test.csproj", "CopyTriding.Test/"]
COPY ["CopyTrading.sln", "./"]
RUN dotnet restore "CopyTrading.sln"

# -----------------------------------------------------------------------------
# Stage 2: Build — компиляция
# -----------------------------------------------------------------------------
FROM restore AS build
ARG BUILD_CONFIGURATION=Release

COPY . .
WORKDIR "/src/CopyTrading"
RUN dotnet build "CopyTrading.csproj" \
    -c ${BUILD_CONFIGURATION} \
    -o /app/build \
    --no-restore

# -----------------------------------------------------------------------------
# Stage 3: Test — запуск тестов (опционально, можно пропустить через --target build)
# -----------------------------------------------------------------------------
FROM build AS test
WORKDIR "/src"
RUN dotnet test "CopyTriding.Test/CopyTrading.Test.csproj" \
    -c Release \
    --no-restore \
    --verbosity minimal

# -----------------------------------------------------------------------------
# Stage 4: Publish — публикация артефакта
# -----------------------------------------------------------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "CopyTrading.csproj" \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Stage 5: Runtime — финальный образ (минимальный aspnet runtime)
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

WORKDIR /app

RUN mkdir -p /app/data/sqlite && chown -R appuser:appgroup /app/data

# Копируем опубликованные файлы
COPY --from=publish --chown=appuser:appgroup /app/publish .

# Переключаемся на не-root пользователя
USER appuser

EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_GCHeapHardLimit=536870912
ENV CopyTrading__AutoStartDependencies=false

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -fsS http://localhost:5000/HealthCheck || exit 1

ENTRYPOINT ["dotnet", "CopyTrading.dll"]
