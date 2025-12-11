# Переменные окружения CopyTrading

## 🔍 DOTNET_RUNNING_IN_CONTAINER

Эта переменная используется для автоматического определения, запущено ли приложение в Docker контейнере или локально.

### Как это работает в коде

```csharp
// Startup.cs
private static string GetSqliteLogsPath()
{
    var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

    if (isDocker)
    {
        return "/app/data/sqlite/logs.db";  // Docker path
    }
    else
    {
        return Path.GetFullPath("./SQLliteBD/logs.db");  // Local path
    }
}
```

---

## 📝 Способы установки DOTNET_RUNNING_IN_CONTAINER

### 1. В Docker (автоматически)

#### Dockerfile
```dockerfile
ENV DOTNET_RUNNING_IN_CONTAINER=true
```

Уже настроено в `Dockerfile:38`

#### docker-compose.yml
```yaml
services:
  copytrading-app:
    environment:
      DOTNET_RUNNING_IN_CONTAINER: "true"
```

Уже настроено в `docker-compose.yml:39`

---

### 2. Локально (для тестирования Docker режима)

Если хотите протестировать Docker логику **без Docker**:

#### Windows (PowerShell)

```powershell
# Установить для текущей сессии
$env:DOTNET_RUNNING_IN_CONTAINER = "true"

# Запустить приложение
dotnet run --project CopyTrading/CopyTrading.csproj

# Сбросить
$env:DOTNET_RUNNING_IN_CONTAINER = ""
```

#### Windows (CMD)

```cmd
# Установить
set DOTNET_RUNNING_IN_CONTAINER=true

# Запустить приложение
dotnet run --project CopyTrading/CopyTrading.csproj

# Сбросить
set DOTNET_RUNNING_IN_CONTAINER=
```

#### Linux / macOS

```bash
# Установить для текущей сессии
export DOTNET_RUNNING_IN_CONTAINER=true

# Запустить приложение
dotnet run --project CopyTrading/CopyTrading.csproj

# Сбросить
unset DOTNET_RUNNING_IN_CONTAINER
```

#### Одной командой

```bash
# Windows (PowerShell)
$env:DOTNET_RUNNING_IN_CONTAINER="true"; dotnet run --project CopyTrading/CopyTrading.csproj

# Linux/macOS
DOTNET_RUNNING_IN_CONTAINER=true dotnet run --project CopyTrading/CopyTrading.csproj
```

---

### 3. В IDE

#### Visual Studio

1. **Через Properties > Debug > Environment Variables:**
   - Правый клик на проект `CopyTrading` → Properties
   - Debug → General → Open debug launch profiles UI
   - Добавить: `DOTNET_RUNNING_IN_CONTAINER=true`

2. **Через launchSettings.json:**

```json
{
  "profiles": {
    "CopyTrading": {
      "environmentVariables": {
        "DOTNET_RUNNING_IN_CONTAINER": "true",
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

#### Visual Studio Code

Создать/изменить `.vscode/launch.json`:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "CopyTrading (Docker mode)",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/CopyTrading/bin/Debug/net8.0/CopyTrading.dll",
      "args": [],
      "cwd": "${workspaceFolder}/CopyTrading",
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_RUNNING_IN_CONTAINER": "true"
      }
    },
    {
      "name": "CopyTrading (Local mode)",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/CopyTrading/bin/Debug/net8.0/CopyTrading.dll",
      "args": [],
      "cwd": "${workspaceFolder}/CopyTrading",
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

#### Rider

1. **Run/Debug Configurations:**
   - Run → Edit Configurations
   - Выбрать конфигурацию
   - Environment variables → Добавить `DOTNET_RUNNING_IN_CONTAINER=true`

2. **Через .run файлы:**

Создать `.run/CopyTrading-Docker.run.xml`:

```xml
<component name="ProjectRunConfigurationManager">
  <configuration default="false" name="CopyTrading (Docker mode)" type="DotNetProject" factoryName=".NET Project">
    <option name="EXE_PATH" value="$PROJECT_DIR$/CopyTrading/bin/Debug/net8.0/CopyTrading.dll" />
    <option name="WORKING_DIRECTORY" value="$PROJECT_DIR$/CopyTrading" />
    <envs>
      <env name="DOTNET_RUNNING_IN_CONTAINER" value="true" />
      <env name="ASPNETCORE_ENVIRONMENT" value="Development" />
    </envs>
  </configuration>
</component>
```

---

### 4. Через appsettings.json (не рекомендуется)

**Не используйте этот способ** - переменные окружения лучше для этого:

```json
// ❌ Плохо - не делайте так
{
  "Environment": {
    "IsDocker": true
  }
}
```

---

## 🔍 Как проверить что переменная установлена

### В коде (для отладки)

Добавьте в `Startup.cs`:

```csharp
public Startup(IConfiguration configuration, IHostEnvironment environment)
{
    var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
    Console.WriteLine($"DOTNET_RUNNING_IN_CONTAINER = {isDocker ?? "(not set)"}");

    // ... rest of code
}
```

### В запущенном приложении

Создайте endpoint для проверки:

```csharp
// HealthCheckController.cs
[HttpGet("environment")]
public IActionResult GetEnvironment()
{
    return Ok(new
    {
        IsDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true",
        Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        AllVariables = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(e => e.Key.ToString(), e => e.Value?.ToString())
    });
}
```

Проверить: http://localhost:5000/api/health/environment

### Через Docker

```bash
# Посмотреть все переменные в контейнере
docker exec copytrading-app printenv

# Посмотреть конкретную переменную
docker exec copytrading-app printenv DOTNET_RUNNING_IN_CONTAINER
```

---

## 🎯 Когда нужно устанавливать вручную

| Сценарий | Нужно ли устанавливать? |
|----------|-------------------------|
| **Запуск через Docker** | ❌ Нет - уже установлено автоматически |
| **Локальная разработка** | ❌ Нет - не нужно (будет использован локальный путь) |
| **Тестирование Docker логики локально** | ✅ Да - установите вручную для тестирования |
| **CI/CD вне Docker** | ⚠️ Зависит от требований |

---

## 📋 Другие важные переменные окружения

### PostgreSQL подключение

```bash
# Хост PostgreSQL
PostgreSQL__Host=localhost              # Локально
PostgreSQL__Host=postgres                # В Docker

# Порт
PostgreSQL__Port=5432

# База данных
PostgreSQL__Database=copytrading

# Credentials
PostgreSQL__Username=copytrading_user
PostgreSQL__Password=copytrading_password
```

### ASP.NET Core

```bash
# Окружение
ASPNETCORE_ENVIRONMENT=Development      # Локально
ASPNETCORE_ENVIRONMENT=Production       # В Docker

# URLs
ASPNETCORE_URLS=http://+:5000;https://+:5001
```

### Пример полной конфигурации

**Windows (PowerShell):**
```powershell
$env:DOTNET_RUNNING_IN_CONTAINER="true"
$env:PostgreSQL__Host="localhost"
$env:PostgreSQL__Port="5432"
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project CopyTrading/CopyTrading.csproj
```

**Linux/macOS:**
```bash
export DOTNET_RUNNING_IN_CONTAINER=true
export PostgreSQL__Host=localhost
export PostgreSQL__Port=5432
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project CopyTrading/CopyTrading.csproj
```

**Docker Compose:**
```yaml
environment:
  DOTNET_RUNNING_IN_CONTAINER: "true"
  PostgreSQL__Host: postgres
  PostgreSQL__Port: 5432
  ASPNETCORE_ENVIRONMENT: Production
```

---

## 🐛 Отладка проблем с переменными окружения

### Проверить какие пути используются

```bash
# В логах приложения должно быть:
# "SQLite logs path: /app/data/sqlite/logs.db" (Docker)
# "SQLite logs path: C:\Program\CopyTrtade\SQLliteBD\logs.db" (Local)
```

### Если пути неправильные

1. Проверьте переменную окружения
2. Проверьте что нет опечаток: `DOTNET_RUNNING_IN_CONTAINER` (не `DOTNET_RUNNING_IN_DOCKER`)
3. Значение должно быть **строго** `"true"` (lowercase)

### Логирование для отладки

Добавьте в `Startup.cs`:

```csharp
private static string GetSqliteLogsPath()
{
    var isDockerEnv = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
    var isDocker = isDockerEnv == "true";

    Console.WriteLine($"[DEBUG] DOTNET_RUNNING_IN_CONTAINER = '{isDockerEnv ?? "(null)"}'");
    Console.WriteLine($"[DEBUG] IsDocker = {isDocker}");

    if (isDocker)
    {
        var path = "/app/data/sqlite/logs.db";
        Console.WriteLine($"[DEBUG] Using Docker path: {path}");
        return path;
    }
    else
    {
        var logsDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "SQLliteBD", "logs.db");
        var fullPath = Path.GetFullPath(logsDbPath);
        Console.WriteLine($"[DEBUG] Using local path: {fullPath}");
        return fullPath;
    }
}
```
