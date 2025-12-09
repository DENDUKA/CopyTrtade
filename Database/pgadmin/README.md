# pgAdmin Configuration

Эта директория содержит файлы конфигурации для автоматической настройки pgAdmin.

## Файлы

### servers.json
Файл конфигурации серверов PostgreSQL для pgAdmin. При запуске контейнера pgAdmin автоматически импортирует этот файл и добавляет сервер "CopyTrading PostgreSQL" в список доступных серверов.

**Структура:**
```json
{
  "Servers": {
    "1": {
      "Name": "CopyTrading PostgreSQL",
      "Group": "Servers",
      "Host": "postgres",
      "Port": 5432,
      "MaintenanceDB": "copytrading",
      "Username": "copytrading_user",
      "SSLMode": "prefer",
      "PassFile": "/pgpass"
    }
  }
}
```

### pgpass
Файл с учетными данными для автоматической аутентификации в PostgreSQL. **Этот файл не должен коммититься в Git!**

**Формат:** `hostname:port:database:username:password`

Пример см. в `pgpass.example`

## Использование

Файлы автоматически монтируются в контейнер pgAdmin через docker-compose.yml:
- `servers.json` → `/pgadmin4/servers.json`
- `pgpass` → `/pgpass`

При первом запуске pgAdmin сервер "CopyTrading PostgreSQL" будет автоматически добавлен в список, и вы сможете подключиться к нему без ввода пароля.

## Доступ к pgAdmin

- URL: http://localhost:5050
- Email: `admin@admin.com`
- Password: `admin`

После входа в левой панели вы увидите уже настроенный сервер "CopyTrading PostgreSQL" в группе "Servers".
