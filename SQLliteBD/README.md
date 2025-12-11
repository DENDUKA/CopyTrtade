# SQLite базы данных CopyTrading

Эта папка содержит SQLite файлы, доступные как локально, так и в Docker контейнере.

## 📁 Файлы

| Файл | Описание | Размер (примерно) |
|------|----------|-------------------|
| `logs.db` | Логи Serilog | Растет со временем |
| `CopyTradingDB.db` | Основные данные (если используется) | Зависит от данных |
| `*.db-shm` | Shared memory (временный) | Создается автоматически |
| `*.db-wal` | Write-Ahead Log (временный) | Создается автоматически |

## 🔧 Подключение DBeaver

### Быстрый старт

1. **Открыть DBeaver**
2. **Создать новое подключение** (`Ctrl+Shift+N`)
3. **Выбрать SQLite**
4. **Указать путь:**
   ```
   C:\Program\CopyTrtade\SQLliteBD\logs.db
   ```
5. **Test Connection** → **Finish**

📖 **Подробная инструкция:** [DBEAVER_SQLITE.md](../DBEAVER_SQLITE.md)

## 🚀 Альтернативы DBeaver

### 1. Serilog UI (для логов)
```
http://localhost:5000/serilog-ui
```
✅ Самый простой способ

### 2. VS Code SQLite Extension
- Установить: `SQLite` by alexcvzz
- Открыть файл `logs.db` в VS Code
- Нажать `Ctrl+Shift+P` → "SQLite: Open Database"

### 3. DB Browser for SQLite
- Скачать: https://sqlitebrowser.org/
- Open Database → Выбрать `logs.db`

### 4. SQLite CLI
```bash
# В Docker
docker exec -it copytrading-app sqlite3 /app/data/sqlite/logs.db

# Локально (если установлен sqlite3)
sqlite3 logs.db
```

## 📊 Примеры SQL запросов

### Логи (logs.db)

```sql
-- Последние 10 логов
SELECT Timestamp, Level, Message
FROM logs
ORDER BY Timestamp DESC
LIMIT 10;

-- Только ошибки
SELECT * FROM logs
WHERE Level = 'Error'
ORDER BY Timestamp DESC;

-- Статистика по уровням
SELECT Level, COUNT(*) as Count
FROM logs
GROUP BY Level;
```

## ⚠️ Важно

1. **Не удаляйте** файлы `*.db-shm` и `*.db-wal` вручную - они нужны SQLite
2. **Бэкапы:** Копируйте только файлы `.db`, остальные пересоздадутся
3. **Размер:** Логи растут со временем, периодически архивируйте старые данные
4. **Конкурентный доступ:** Для чтения используйте read-only режим в DBeaver

## 🧹 Очистка старых логов

```sql
-- Удалить логи старше 30 дней
DELETE FROM logs
WHERE Timestamp < datetime('now', '-30 days');

-- Оптимизировать БД после удаления
VACUUM;
```

⚠️ Выполняйте только когда приложение остановлено!

## 🔗 Дополнительная информация

- [DBEAVER_SQLITE.md](../DBEAVER_SQLITE.md) - Подробная инструкция по подключению
- [DOCKER.md](../DOCKER.md) - Docker setup
- [DEVELOPMENT.md](../DEVELOPMENT.md) - Локальная разработка
