# 🚀 Быстрое подключение DBeaver к SQLite

## Шаг 1: Проверить что файлы существуют

Откройте PowerShell и выполните:

```powershell
Get-ChildItem C:\Program\CopyTrtade\SQLliteBD\*.db | Format-Table Name, Length, LastWriteTime
```

Вы должны увидеть:
```
Name               Length LastWriteTime
----               ------ -------------
CopyTradingDB.db   45MB   ...
logs.db            2.9MB  ...
```

---

## Шаг 2: Открыть DBeaver

1. Запустите **DBeaver**
2. Если это первый запуск, пропустите создание тестового подключения

---

## Шаг 3: Создать новое подключение

### Способ 1: Через меню
- Нажмите **Database** → **New Database Connection**

### Способ 2: Горячая клавиша
- Нажмите **Ctrl+Shift+N**

### Способ 3: Кнопка на панели
- Нажмите значок **вилки с плюсом** в левом верхнем углу

---

## Шаг 4: Выбрать SQLite

В окне "Connect to a database":

1. **В поиске введите**: `sqlite`
2. **Выберите**: `SQLite`
3. **Нажмите**: `Next >`

![SQLite Selection](https://dbeaver.com/wp-content/uploads/wikidocs/wiki/images/database-driver-sqlite.png)

---

## Шаг 5: Настроить подключение к LOGS.DB

### 5.1 Основные настройки

В окне "Connection Settings":

**Path:**
```
C:\Program\CopyTrtade\SQLliteBD\logs.db
```

**ИЛИ** нажмите кнопку **Browse...** и выберите файл `logs.db`

**Connection name:** (опционально)
```
CopyTrading - Logs
```

### 5.2 Скачать драйвер (если первый раз)

1. **Нажмите**: `Test Connection`
2. **Появится окно**: "Download driver files?"
3. **Нажмите**: `Download`
4. Подождите пока драйвер скачается
5. **Появится**: "Connected"

### 5.3 Завершить

**Нажмите**: `Finish`

---

## Шаг 6: Открыть таблицу логов

1. В левой панели найдите подключение: **CopyTrading - Logs**
2. Раскройте: **CopyTrading - Logs** → **main** → **Tables**
3. **Двойной клик** на таблице: **logs**
4. Нажмите вкладку **Data**

Вы увидите все логи! 🎉

---

## Шаг 7: Выполнить SQL запрос

### Последние 10 логов

1. **Нажмите**: `SQL Editor` → `New SQL script` (или **Ctrl+]** или **F3**)
2. **Введите**:

```sql
SELECT Timestamp, Level, Message
FROM logs
ORDER BY Timestamp DESC
LIMIT 10;
```

3. **Нажмите**: **Ctrl+Enter** или кнопку ▶️ (Execute)

### Только ошибки

```sql
SELECT Timestamp, Level, Message, Exception
FROM logs
WHERE Level = 'Error'
ORDER BY Timestamp DESC;
```

### Поиск по тексту

```sql
SELECT Timestamp, Level, Message
FROM logs
WHERE Message LIKE '%OrderId%'
ORDER BY Timestamp DESC
LIMIT 50;
```

### Логи за последний час

```sql
SELECT Timestamp, Level, Message
FROM logs
WHERE Timestamp >= datetime('now', '-1 hour')
ORDER BY Timestamp DESC;
```

---

## 🎯 Подключить вторую БД (CopyTradingDB.db)

Повторите шаги 3-6, но используйте:

**Path:**
```
C:\Program\CopyTrtade\SQLliteBD\CopyTradingDB.db
```

**Connection name:**
```
CopyTrading - Main DB
```

---

## ⚠️ Ошибка: "Database is locked"

Если видите ошибку "database is locked", это значит приложение пишет в БД.

### Решение: Открыть в Read-Only режиме

1. **Правый клик** на подключении → **Edit Connection**
2. Вкладка **Driver properties**
3. Найдите свойство **URL** и измените на:
   ```
   jdbc:sqlite:C:\Program\CopyTrtade\SQLliteBD\logs.db?mode=ro
   ```
4. **Test Connection** → **OK**

Теперь можно читать логи даже когда приложение работает!

---

## 📊 Структура таблицы logs

| Поле | Тип | Описание |
|------|-----|----------|
| `Timestamp` | TEXT | ISO 8601 дата/время |
| `Level` | TEXT | Information, Warning, Error, Debug |
| `Message` | TEXT | Текст лога |
| `Exception` | TEXT | Stack trace (если есть) |
| `Properties` | TEXT | JSON с доп. данными |

---

## 🔍 Полезные фильтры в DBeaver

### Фильтр по уровню (без SQL)

1. Открыть таблицу **logs** → вкладка **Data**
2. Нажать на заголовок колонки **Level**
3. Выбрать фильтр → **Custom filter**
4. Ввести: `Error` → OK

### Фильтр по дате

1. Колонка **Timestamp** → Custom filter
2. Ввести: `2025-12-11%` (все логи за 11 декабря)

### Сортировка

Клик на заголовок колонки для сортировки.

---

## 📁 Где находятся файлы?

```
C:\Program\CopyTrtade\
└── SQLliteBD\
    ├── logs.db              ← Подключить в DBeaver (Serilog логи)
    ├── logs.db-shm          ← Не трогать (временный файл)
    ├── logs.db-wal          ← Не трогать (временный файл)
    ├── CopyTradingDB.db     ← Подключить в DBeaver (основные данные)
    └── README.md
```

---

## 🎨 Альтернативы DBeaver

### 1. Serilog UI (самый простой!)

Откройте в браузере:
```
http://localhost:5000/serilog-ui
```

✅ Не требует настройки
✅ Встроенный поиск
✅ Красивый интерфейс
✅ Автообновление

### 2. DB Browser for SQLite

- Скачать: https://sqlitebrowser.org/
- Открыть файл → `C:\Program\CopyTrtade\SQLliteBD\logs.db`

### 3. VS Code + SQLite Extension

1. Установить расширение: `SQLite` (alexcvzz)
2. Открыть файл `.db` в VS Code
3. Команда: `SQLite: Open Database`

---

## ✅ Проверка подключения

После подключения выполните:

```sql
-- Проверить что таблица существует
SELECT name FROM sqlite_master WHERE type='table';

-- Посчитать количество логов
SELECT COUNT(*) as TotalLogs FROM logs;

-- Первый лог
SELECT * FROM logs ORDER BY Timestamp ASC LIMIT 1;

-- Последний лог
SELECT * FROM logs ORDER BY Timestamp DESC LIMIT 1;
```

Если запросы работают — всё настроено правильно! 🎉

---

## 🆘 Помощь

**Проблемы с подключением?** Смотрите подробную инструкцию:
- [DBEAVER_SQLITE.md](./DBEAVER_SQLITE.md) - Полное руководство с решением проблем

**Вопросы по Docker?**
- [DOCKER.md](./DOCKER.md) - Docker setup
- [DEVELOPMENT.md](./DEVELOPMENT.md) - Локальная разработка
