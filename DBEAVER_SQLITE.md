# Подключение DBeaver к SQLite базам данных

## 📊 Архитектура после изменений

```
┌─────────────────────────────────────────────────────┐
│   Ваш компьютер (Windows)                          │
│                                                      │
│   📁 C:\Program\CopyTrtade\SQLliteBD\              │
│      ├── logs.db              ← Логи Serilog       │
│      └── CopyTradingDB.db     ← Другие данные      │
│             ↕                                        │
│      (bind mount)                                   │
│             ↕                                        │
│   ┌─────────────────────────────────────┐          │
│   │  Docker Container                    │          │
│   │  copytrading-app                    │          │
│   │                                      │          │
│   │  /app/data/sqlite/                  │          │
│   │   ├── logs.db         (тот же файл) │          │
│   │   └── CopyTradingDB.db (тот же файл)│          │
│   └──────────────────────────────────────┘          │
│                                                      │
│   💾 DBeaver может читать файлы напрямую из:       │
│      C:\Program\CopyTrtade\SQLliteBD\              │
└─────────────────────────────────────────────────────┘
```

## ✅ Что было изменено

В `docker-compose.yml` заменили Docker volume на bind mount:

**Было:**
```yaml
volumes:
  - sqlite_logs:/app/data/sqlite  # Docker volume (недоступен напрямую)
```

**Стало:**
```yaml
volumes:
  - ./SQLliteBD:/app/data/sqlite  # Bind mount (доступен локально)
```

Теперь файлы SQLite находятся в папке `C:\Program\CopyTrtade\SQLliteBD\` и доступны как из Docker, так и локально!

---

## 🚀 Подключение DBeaver

### Шаг 1: Создать папку SQLliteBD (если не существует)

```powershell
# Windows PowerShell
New-Item -Path "C:\Program\CopyTrtade\SQLliteBD" -ItemType Directory -Force
```

### Шаг 2: Запустить приложение

#### Вариант A: В Docker (рекомендуется для тестирования)

```bash
# Остановить старые контейнеры (если есть)
docker-compose down

# Запустить с новой конфигурацией
docker-compose up -d

# Подождать 10 секунд, чтобы БД создались
```

#### Вариант B: Локально

```bash
docker-compose -f docker-compose.dev.yml up -d
dotnet run --project CopyTrading/CopyTrading.csproj
```

### Шаг 3: Проверить что файлы созданы

```powershell
# Посмотреть файлы
Get-ChildItem C:\Program\CopyTrtade\SQLliteBD\

# Должны увидеть:
# logs.db              (Serilog логи)
# CopyTradingDB.db     (если используется)
```

### Шаг 4: Открыть DBeaver

1. **Запустить DBeaver**

2. **Создать новое подключение:**
   - `Database` → `New Database Connection`
   - Или нажать `Ctrl+Shift+N`

3. **Выбрать SQLite:**
   - В списке найти `SQLite`
   - Нажать `Next`

### Шаг 5: Настроить подключение к **logs.db** (логи Serilog)

1. **Path:**
   ```
   C:\Program\CopyTrtade\SQLliteBD\logs.db
   ```

   Или нажать `Browse` и выбрать файл

2. **Connection name:** (опционально)
   ```
   CopyTrading Logs
   ```

3. **Нажать `Test Connection`**
   - Если первый раз подключаетесь, DBeaver предложит скачать SQLite драйвер
   - Нажать `Download` и подождать

4. **Нажать `Finish`**

### Шаг 6: Настроить подключение к **CopyTradingDB.db** (основные данные)

Повторить шаги, но использовать путь:
```
C:\Program\CopyTrtade\SQLliteBD\CopyTradingDB.db
```

Connection name:
```
CopyTrading Main DB
```

---

## 📊 Структура базы логов (logs.db)

После подключения вы увидите таблицу `logs` с полями:

| Поле | Тип | Описание |
|------|-----|----------|
| `Timestamp` | TEXT | Время лога (ISO 8601) |
| `Level` | TEXT | Уровень: Information, Warning, Error |
| `Message` | TEXT | Сообщение лога |
| `Exception` | TEXT | Stack trace (если есть ошибка) |
| `Properties` | TEXT | JSON с дополнительными данными |

### Примеры SQL запросов

```sql
-- Посмотреть последние 10 логов
SELECT Timestamp, Level, Message
FROM logs
ORDER BY Timestamp DESC
LIMIT 10;

-- Посмотреть только ошибки
SELECT Timestamp, Level, Message, Exception
FROM logs
WHERE Level = 'Error'
ORDER BY Timestamp DESC;

-- Логи за последний час
SELECT Timestamp, Level, Message
FROM logs
WHERE Timestamp >= datetime('now', '-1 hour')
ORDER BY Timestamp DESC;

-- Количество логов по уровням
SELECT Level, COUNT(*) as Count
FROM logs
GROUP BY Level
ORDER BY Count DESC;

-- Поиск по тексту
SELECT Timestamp, Level, Message
FROM logs
WHERE Message LIKE '%OrderId%'
ORDER BY Timestamp DESC
LIMIT 50;
```

---

## 🔧 Альтернативные способы

### Способ 1: Копировать файл из Docker (если не используете bind mount)

```bash
# Скопировать из контейнера на хост
docker cp copytrading-app:/app/data/sqlite/logs.db ./logs.db

# Открыть в DBeaver
# Path: C:\Program\CopyTrtade\logs.db
```

⚠️ **Недостаток:** Файл нужно копировать каждый раз для получения свежих данных.

### Способ 2: SQLite CLI в Docker

```bash
# Войти в контейнер
docker exec -it copytrading-app /bin/bash

# Установить sqlite3 (если нет)
apt-get update && apt-get install -y sqlite3

# Открыть БД
sqlite3 /app/data/sqlite/logs.db

# Примеры команд
.tables                           # Список таблиц
.schema logs                      # Структура таблицы
SELECT * FROM logs LIMIT 10;      # Запрос
.quit                             # Выход
```

### Способ 3: Serilog UI (веб-интерфейс)

Самый простой способ для просмотра логов:

```
http://localhost:5000/serilog-ui
```

✅ Не требует настройки
✅ Поиск и фильтрация встроены
✅ Красивый UI

---

## 🐛 Решение проблем

### Проблема 1: Файл logs.db не создается

**Решение:**

1. Проверьте что папка существует:
   ```powershell
   Test-Path C:\Program\CopyTrtade\SQLliteBD
   ```

2. Проверьте права доступа (Docker должен иметь доступ к папке):
   ```powershell
   # Windows: Убедитесь что папка не в защищенной области
   # Или дайте Docker Desktop доступ через Settings → Resources → File Sharing
   ```

3. Посмотрите логи контейнера:
   ```bash
   docker-compose logs copytrading-app | grep -i sqlite
   ```

### Проблема 2: DBeaver не может открыть файл (locked)

**Причина:** Файл SQLite открыт приложением для записи.

**Решение A:** Открыть в read-only режиме в DBeaver:
- В настройках подключения → вкладка `Connection Settings`
- Добавить в `JDBC URL` параметр: `?mode=ro`
- Полный путь будет: `jdbc:sqlite:C:\Program\CopyTrtade\SQLliteBD\logs.db?mode=ro`

**Решение B:** Остановить приложение:
```bash
docker-compose down
# Теперь можно открыть в DBeaver с полным доступом
```

### Проблема 3: Файл существует но пустой

**Решение:**

1. Подождите немного - логи пишутся асинхронно
2. Сделайте действия в приложении (откройте страницы, API)
3. Проверьте уровень логирования в `appsettings.json`

### Проблема 4: В Docker путь работает, но локально нет

**Проверьте переменную окружения:**

```powershell
# Проверить
[Environment]::GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")

# Должно быть пусто для локального запуска
# Если установлено "true", сбросить:
$env:DOTNET_RUNNING_IN_CONTAINER = $null
```

---

## 📁 Структура файлов

```
C:\Program\CopyTrtade\
├── SQLliteBD\                          ← Папка с SQLite файлами
│   ├── logs.db                         ← Serilog логи (доступен в DBeaver)
│   ├── logs.db-shm                     ← SQLite shared memory (временный)
│   ├── logs.db-wal                     ← SQLite write-ahead log (временный)
│   └── CopyTradingDB.db                ← Основные данные (если используется)
│
├── docker-compose.yml                  ← Настройка: ./SQLliteBD:/app/data/sqlite
└── DBEAVER_SQLITE.md                   ← Этот файл
```

### Временные файлы SQLite

- `.db-shm` - Shared memory файл (для конкурентного доступа)
- `.db-wal` - Write-Ahead Log (для транзакций)

Эти файлы создаются автоматически и не нужно их открывать в DBeaver.

---

## ✅ Проверка что всё работает

### 1. Запустить Docker

```bash
docker-compose down && docker-compose up -d
```

### 2. Проверить файлы

```powershell
Get-ChildItem C:\Program\CopyTrtade\SQLliteBD\ | Format-Table Name, Length, LastWriteTime
```

Должно быть что-то вроде:
```
Name              Length  LastWriteTime
----              ------  -------------
logs.db           28672   11.12.2025 15:30:45
logs.db-shm       32768   11.12.2025 15:30:45
logs.db-wal       16384   11.12.2025 15:30:45
```

### 3. Подключиться в DBeaver

Path: `C:\Program\CopyTrtade\SQLliteBD\logs.db`

### 4. Выполнить тестовый запрос

```sql
SELECT COUNT(*) as TotalLogs FROM logs;
```

Если видите число > 0, всё работает! 🎉

---

## 🔗 Дополнительные ресурсы

- [SQLite Documentation](https://www.sqlite.org/docs.html)
- [DBeaver SQLite Guide](https://dbeaver.com/docs/wiki/database-driver-sqlite/)
- [Serilog SQLite Sink](https://github.com/saleem-mirza/serilog-sinks-sqlite)

---

## 💡 Полезные советы

1. **Read-only режим** для безопасности:
   - Используйте `?mode=ro` в JDBC URL
   - Так приложение и DBeaver не будут конфликтовать

2. **Бэкап перед экспериментами:**
   ```powershell
   Copy-Item C:\Program\CopyTrtade\SQLliteBD\logs.db C:\Program\CopyTrtade\SQLliteBD\logs.db.backup
   ```

3. **Экспорт данных из DBeaver:**
   - Правый клик на таблице → Export Data
   - Выбрать формат: CSV, JSON, SQL, Excel

4. **Автоматическое обновление:**
   - В DBeaver: Connection settings → Enable auto-refresh
   - Или нажимать `F5` для обновления

5. **Использовать Serilog UI** для повседневного просмотра:
   - Проще и быстрее чем DBeaver
   - Специально оптимизирован для логов
   - Доступен: http://localhost:5000/serilog-ui
