# Архитектура Blazor UI для Copy Trading System

## 🎯 Цель
Создать интерактивный веб-интерфейс для мониторинга и управления системой копитрейдинга с real-time обновлениями.

---

## 📐 Рекомендуемая архитектура

### Вариант 1: **Blazor Server** (Рекомендуется для вашего случая)

**Почему Blazor Server:**
- ✅ Real-time обновления через SignalR (уже встроен)
- ✅ Прямой доступ к Singleton сервисам (OrderService, CopyOrderService, etc.)
- ✅ Низкая задержка обновлений позиций и ордеров
- ✅ Не нужно создавать дополнительные API endpoints
- ✅ Меньше кода - используем существующие сервисы напрямую
- ❌ Требует постоянное соединение с сервером

**Подходит если:**
- Используете приложение ЛОКАЛЬНО или в пределах одной сети
- Нужны real-time обновления (ордера, позиции, трейды)
- Не планируете 1000+ одновременных пользователей

### Вариант 2: **Blazor WebAssembly + API**

**Почему Blazor WASM:**
- ✅ Работает в браузере, меньше нагрузка на сервер
- ✅ Можно деплоить отдельно (CDN)
- ❌ Нужно создавать API endpoints для всех операций
- ❌ Real-time через WebSocket или SignalR (дополнительная работа)
- ❌ Сложнее интеграция с существующими Singleton сервисами

---

## 🏗️ Структура проекта (Blazor Server)

```
CopyTrtade/
├── CopyTrading/                    # ← Существующий проект (Backend)
│   ├── Services/
│   ├── Providers/
│   ├── DataEvents/
│   └── ...
│
├── CopyTrading.BlazorUI/           # ← НОВЫЙ проект (Frontend)
│   ├── Pages/                      # Blazor страницы (.razor)
│   │   ├── Index.razor             # Главная страница - дашборд
│   │   ├── Positions.razor         # Позиции трейдеров и копируемые
│   │   ├── Orders.razor            # Активные ордера
│   │   ├── TradeHistory.razor      # История трейдов
│   │   ├── Traders.razor           # Список копируемых трейдеров
│   │   ├── Settings.razor          # Настройки (баланс, пропорции)
│   │   └── Logs.razor              # Логи и ошибки
│   │
│   ├── Components/                 # Переиспользуемые компоненты
│   │   ├── PositionCard.razor      # Карточка позиции
│   │   ├── OrderRow.razor          # Строка ордера
│   │   ├── TraderCard.razor        # Карточка трейдера
│   │   ├── RealTimeChart.razor     # График P&L
│   │   └── StatusBadge.razor       # Бейдж статуса (Open, Filled, etc.)
│   │
│   ├── Services/                   # UI-специфичные сервисы
│   │   ├── UIStateService.cs       # Состояние UI (фильтры, сортировка)
│   │   ├── NotificationService.cs  # Уведомления (toast)
│   │   └── RealtimeUpdateService.cs # Подписки на DataBusEvents
│   │
│   ├── ViewModels/                 # ViewModel для UI
│   │   ├── PositionViewModel.cs    # Позиция + UI состояние
│   │   ├── OrderViewModel.cs       # Ордер + UI состояние
│   │   └── DashboardViewModel.cs   # Статистика дашборда
│   │
│   ├── Shared/
│   │   ├── MainLayout.razor        # Главный layout
│   │   ├── NavMenu.razor           # Навигационное меню
│   │   └── ErrorBoundary.razor     # Обработка ошибок
│   │
│   ├── wwwroot/                    # Статические файлы
│   │   ├── css/
│   │   ├── js/
│   │   └── favicon.ico
│   │
│   ├── _Imports.razor              # Глобальные using
│   ├── App.razor                   # Корневой компонент
│   └── Program.cs                  # Entry point
│
└── CopyTrading.Models/             # ← Общие модели (уже есть)
    └── ...
```

---

## 🔗 Интеграция с существующим Backend

### Подход 1: **Объединить Blazor Server в CopyTrading проект** (Проще)

**Шаги:**
1. Добавить Blazor Server в существующий `CopyTrading.csproj`
2. Создать папку `Pages/` и `Components/` внутри CopyTrading
3. Настроить Razor Pages в `Startup.cs`

**Плюсы:**
- ✅ Один проект, проще деплой
- ✅ Прямой доступ ко всем сервисам (уже Singleton)
- ✅ Не нужно создавать API

**Минусы:**
- ❌ Смешение Backend и Frontend кода
- ❌ Сложнее поддерживать при росте UI

**Когда использовать:** Для быстрого прототипа, небольшого UI

### Подход 2: **Отдельный Blazor Server проект** (Чище)

**Шаги:**
1. Создать новый проект `CopyTrading.BlazorUI`
2. Добавить ссылку на `CopyTrading` как на Library
3. Переиспользовать сервисы через DI

**Плюсы:**
- ✅ Чистое разделение Backend/Frontend
- ✅ Легче тестировать UI отдельно
- ✅ Можно использовать разные порты

**Минусы:**
- ❌ Нужно решить вопрос с Singleton сервисами
- ❌ DataBusEvents нужно пробросить через SignalR Hub

**Когда использовать:** Для production приложения

---

## 🔥 Real-time обновления через DataBusEvents

### Архитектура подписок

```
┌─────────────────────────────────────────────────┐
│ HyperLiquid Exchange                            │
│ (OrdersTradesSubscriber)                        │
└──────────────┬──────────────────────────────────┘
               │ WebSocket
               ▼
┌─────────────────────────────────────────────────┐
│ DataBusEvents (статические события)            │
│ • NewOrders                                     │
│ • NewTrades                                     │
└──────────────┬──────────────────────────────────┘
               │
    ┌──────────┴──────────┬─────────────────────┐
    ▼                     ▼                     ▼
┌──────────┐      ┌──────────────┐      ┌──────────────────┐
│CopyOrder │      │ FillsOrder   │      │ RealtimeUpdate   │
│Service   │      │ Service      │      │ Service (NEW)    │
└──────────┘      └──────────────┘      └────────┬─────────┘
                                                  │
                                                  ▼
                                        ┌──────────────────┐
                                        │ SignalR Hub      │
                                        │ (UI Notifications)│
                                        └────────┬─────────┘
                                                 │
                                                 ▼
                                        ┌──────────────────┐
                                        │ Blazor Components│
                                        │ • Positions.razor│
                                        │ • Orders.razor   │
                                        └──────────────────┘
```

### Реализация RealtimeUpdateService

**Создаем новый сервис для UI:**

```csharp
// CopyTrading.BlazorUI/Services/RealtimeUpdateService.cs

public class RealtimeUpdateService : IDisposable
{
    private readonly IHubContext<CopyTradingHub> _hubContext;
    private readonly ILogger<RealtimeUpdateService> _logger;

    public RealtimeUpdateService(
        IHubContext<CopyTradingHub> hubContext,
        ILogger<RealtimeUpdateService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;

        // Подписываемся на события из DataBusEvents
        DataBusEvents.NewOrders += OnNewOrders;
        DataBusEvents.NewTrades += OnNewTrades;
    }

    private async void OnNewOrders(OriginalOrder[] orders)
    {
        _logger.LogInformation($"Отправка {orders.Length} ордеров в UI");

        // Отправляем обновления всем подключенным клиентам
        await _hubContext.Clients.All.SendAsync("ReceiveOrders", orders);
    }

    private async void OnNewTrades(OriginalTrade[] trades, bool isSnapshot)
    {
        if (!isSnapshot)
        {
            _logger.LogInformation($"Отправка {trades.Length} трейдов в UI");
            await _hubContext.Clients.All.SendAsync("ReceiveTrades", trades);
        }
    }

    public void Dispose()
    {
        DataBusEvents.NewOrders -= OnNewOrders;
        DataBusEvents.NewTrades -= OnNewTrades;
    }
}
```

### SignalR Hub

```csharp
// CopyTrading.BlazorUI/Hubs/CopyTradingHub.cs

public class CopyTradingHub : Hub
{
    private readonly PositionMappingService _positionService;
    private readonly CurrentWalletPositionService _walletPositionService;

    public CopyTradingHub(
        PositionMappingService positionService,
        CurrentWalletPositionService walletPositionService)
    {
        _positionService = positionService;
        _walletPositionService = walletPositionService;
    }

    // Клиент может запросить текущее состояние при подключении
    public async Task<PositionMapping[]> GetAllPositions()
    {
        return _positionService.GetAllMappings();
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
}
```

### Blazor компонент с подпиской

```razor
@* Pages/Positions.razor *@

@page "/positions"
@using Microsoft.AspNetCore.SignalR.Client
@inject NavigationManager Navigation
@inject PositionMappingService PositionService
@implements IAsyncDisposable

<h3>Активные позиции</h3>

<table class="table">
    <thead>
        <tr>
            <th>Трейдер</th>
            <th>Symbol</th>
            <th>Direction</th>
            <th>Трейдер Qty</th>
            <th>Моя Qty</th>
            <th>Ratio</th>
            <th>Обновлено</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var position in positions)
        {
            <tr class="@GetRowClass(position)">
                <td>@position.TraderWallet</td>
                <td>@position.Symbol</td>
                <td><StatusBadge Direction="@position.Direction" /></td>
                <td>@position.TraderQuantity.ToString("F4")</td>
                <td>@position.MyQuantity.ToString("F4")</td>
                <td>@position.PositionRatio.ToString("P2")</td>
                <td>@position.LastUpdate.ToLocalTime().ToString("HH:mm:ss")</td>
            </tr>
        }
    </tbody>
</table>

@code {
    private HubConnection? hubConnection;
    private List<PositionMapping> positions = new();

    protected override async Task OnInitializedAsync()
    {
        // Загружаем начальные данные
        positions = PositionService.GetAllMappings().ToList();

        // Подключаемся к SignalR Hub
        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/copytradinghub"))
            .WithAutomaticReconnect()
            .Build();

        // Подписываемся на события
        hubConnection.On<OriginalOrder[]>("ReceiveOrders", (orders) =>
        {
            // Обновляем UI при новых ордерах
            positions = PositionService.GetAllMappings().ToList();
            InvokeAsync(StateHasChanged); // Перерисовываем компонент
        });

        await hubConnection.StartAsync();
    }

    private string GetRowClass(PositionMapping position)
    {
        var timeSinceUpdate = DateTime.UtcNow - position.LastUpdate;
        return timeSinceUpdate.TotalMinutes > 5 ? "table-warning" : "";
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }
}
```

---

## 📋 Страницы UI (Рекомендуемые)

### 1. **Dashboard** (`/`)
- Сводка по всем позициям
- Total P&L (Profit & Loss)
- Количество активных позиций
- Количество активных ордеров
- График баланса за день/неделю

### 2. **Positions** (`/positions`)
- Таблица всех активных позиций
- Фильтр по трейдеру, символу, направлению
- Колонки:
  - Трейдер
  - Symbol
  - Direction (Long/Short)
  - Трейдер Qty
  - Моя Qty
  - Position Ratio
  - Entry Price
  - Current Price
  - Unrealized P&L
  - Leverage
  - Last Update

### 3. **Orders** (`/orders`)
- Таблица активных ордеров
- Статус (Open, Filled, Canceled)
- Фильтр по статусу, трейдеру
- Колонки:
  - Order ID (Трейдера)
  - My Order ID
  - Symbol
  - Direction
  - Type (Market/Limit)
  - Quantity
  - Price
  - Status
  - Created At
  - Action (Отменить вручную)

### 4. **Trade History** (`/trades`)
- История всех исполненных трейдов
- Пагинация
- Фильтр по дате, трейдеру, символу
- Колонки:
  - Trade ID
  - Order ID
  - Symbol
  - Direction
  - Quantity
  - Price
  - Volume USD
  - Fee
  - Timestamp

### 5. **Traders** (`/traders`)
- Список копируемых трейдеров
- Добавить/удалить трейдера
- Статистика по трейдеру:
  - Total Volume
  - Total P&L
  - Win Rate
  - Active Positions
  - Колонки:
    - Wallet Address
    - Active Positions
    - Total Volume (24h)
    - P&L (Today)
    - Status (Active/Paused)
    - Actions (Пауза/Удалить)

### 6. **Settings** (`/settings`)
- Мой кошелек
- Баланс
- Настройки копирования:
  - Max % от баланса на одну позицию
  - Blacklist символов
  - Min/Max Leverage
- API ключи (зашифрованные)

### 7. **Logs** (`/logs`)
- Интеграция с Serilog UI (уже есть `/serilog-ui`)
- Фильтр по уровню (Info, Warning, Error)
- Поиск по тексту
- Экспорт логов

---

## 🎨 UI Components (Переиспользуемые)

### PositionCard.razor
```razor
@* Карточка позиции с цветовым кодированием *@

<div class="card position-card @DirectionClass">
    <div class="card-body">
        <h5 class="card-title">@Symbol <span class="badge @DirectionBadge">@Direction</span></h5>
        <p class="card-text">
            <strong>Трейдер:</strong> @TraderQuantity<br/>
            <strong>Я:</strong> @MyQuantity<br/>
            <strong>Ratio:</strong> @PositionRatio.ToString("P2")<br/>
            <strong>P&L:</strong> <span class="@PnlClass">@UnrealizedPnl.ToString("C2")</span>
        </p>
    </div>
</div>

@code {
    [Parameter] public string Symbol { get; set; } = "";
    [Parameter] public Direction Direction { get; set; }
    [Parameter] public decimal TraderQuantity { get; set; }
    [Parameter] public decimal MyQuantity { get; set; }
    [Parameter] public decimal PositionRatio { get; set; }
    [Parameter] public decimal UnrealizedPnl { get; set; }

    private string DirectionClass => Direction == Direction.Long ? "border-success" : "border-danger";
    private string DirectionBadge => Direction == Direction.Long ? "badge-success" : "badge-danger";
    private string PnlClass => UnrealizedPnl >= 0 ? "text-success" : "text-danger";
}
```

### StatusBadge.razor
```razor
@* Бейдж статуса ордера *@

<span class="badge @BadgeClass">@Status</span>

@code {
    [Parameter] public OrderStatus Status { get; set; }

    private string BadgeClass => Status switch
    {
        OrderStatus.Open => "badge-primary",
        OrderStatus.Filled => "badge-success",
        OrderStatus.Canceled => "badge-secondary",
        OrderStatus.Rejected => "badge-danger",
        OrderStatus.Triggered => "badge-info",
        _ => "badge-dark"
    };
}
```

### RealTimeChart.razor
```razor
@* График P&L с использованием Chart.js *@
@inject IJSRuntime JS

<canvas id="pnlChart" width="400" height="200"></canvas>

@code {
    [Parameter] public List<decimal> DataPoints { get; set; } = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("renderChart", "pnlChart", DataPoints);
        }
    }
}
```

---

## 🔧 Настройка Startup.cs

### Добавить Blazor Server + SignalR

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Существующие сервисы...

    // НОВЫЕ: Blazor Server
    services.AddRazorPages();
    services.AddServerSideBlazor();

    // UI сервисы
    services.AddSingleton<RealtimeUpdateService>();
    services.AddScoped<NotificationService>();
    services.AddScoped<UIStateService>();

    // SignalR настройки
    services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = true;
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    });
}

public void Configure(IApplicationBuilder app, IHostEnvironment env, IServiceProvider serviceProvider)
{
    // Существующие настройки...

    app.UseStaticFiles();

    app.UseRouting();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();

        // НОВЫЕ: Blazor endpoints
        endpoints.MapBlazorHub();
        endpoints.MapFallbackToPage("/_Host");

        // SignalR Hub
        endpoints.MapHub<CopyTradingHub>("/copytradinghub");
    });

    // Инициализируем RealtimeUpdateService для подписки на события
    serviceProvider.GetRequiredService<RealtimeUpdateService>();

    // Существующие сервисы...
    serviceProvider.GetService<FillsOrderService>();
    serviceProvider.GetService<CopyOrderService>();
}
```

---

## 📦 NuGet пакеты

Добавить в `CopyTrading.csproj`:

```xml
<ItemGroup>
  <!-- Blazor Server -->
  <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="8.0.0" />

  <!-- Bootstrap (для UI) -->
  <PackageReference Include="Blazorise.Bootstrap5" Version="1.5.0" />
  <PackageReference Include="Blazorise.Icons.FontAwesome" Version="1.5.0" />

  <!-- Charts (опционально) -->
  <PackageReference Include="ChartJs.Blazor" Version="2.0.2" />

  <!-- Notifications (опционально) -->
  <PackageReference Include="Blazored.Toast" Version="4.2.1" />
</ItemGroup>
```

---

## 🚀 Пошаговый план внедрения

### Этап 1: Базовая настройка (1-2 дня)
1. ✅ Добавить Blazor Server в `CopyTrading.csproj`
2. ✅ Создать `Pages/_Host.cshtml` и `App.razor`
3. ✅ Настроить `Startup.cs` (AddBlazor, MapBlazorHub)
4. ✅ Создать `MainLayout.razor` и `NavMenu.razor`
5. ✅ Тестовая страница `Index.razor` с приветствием

### Этап 2: Real-time инфраструктура (2-3 дня)
1. ✅ Создать `CopyTradingHub.cs`
2. ✅ Создать `RealtimeUpdateService.cs` с подписками на DataBusEvents
3. ✅ Зарегистрировать сервисы в DI
4. ✅ Тестовая страница с real-time обновлениями

### Этап 3: Positions страница (2-3 дня)
1. ✅ `Positions.razor` с таблицей
2. ✅ `PositionCard.razor` компонент
3. ✅ Real-time обновления через SignalR
4. ✅ Фильтрация и сортировка

### Этап 4: Orders страница (2-3 дня)
1. ✅ `Orders.razor` с таблицей
2. ✅ `OrderRow.razor` компонент
3. ✅ Real-time статусы ордеров
4. ✅ Кнопка отмены ордера

### Этап 5: Dashboard (3-4 дня)
1. ✅ `Dashboard.razor`
2. ✅ Сводные карточки (Total P&L, Active Positions, etc.)
3. ✅ График баланса (Chart.js)
4. ✅ Real-time обновления метрик

### Этап 6: Остальные страницы (5-7 дней)
1. ✅ `Traders.razor`
2. ✅ `TradeHistory.razor`
3. ✅ `Settings.razor`
4. ✅ Интеграция с Serilog UI

---

## 🎯 Альтернатива: Минималистичный подход

Если нужно **быстро** и **просто**:

### Вариант 3: API + любой Frontend фреймворк

1. **Backend:** Создать REST API контроллеры:
   - `GET /api/positions` → Все позиции
   - `GET /api/orders` → Все ордера
   - `GET /api/trades` → История трейдов
   - `POST /api/orders/cancel/{id}` → Отмена ордера

2. **Frontend:** Использовать готовые решения:
   - **React + Material UI**
   - **Vue + Vuetify**
   - **Angular + Angular Material**
   - **Vanilla JS + Bootstrap**

3. **Real-time:** WebSocket endpoint для обновлений

**Плюсы:**
- ✅ Больше гибкости в выборе UI
- ✅ Можно использовать готовые темплейты (Admin dashboards)
- ✅ Проще найти готовые компоненты

**Минусы:**
- ❌ Больше кода (API + Frontend отдельно)
- ❌ Сложнее интеграция с существующими Singleton сервисами

---

## 🎨 Рекомендуемые UI библиотеки для Blazor

### 1. **Blazorise** (Рекомендую)
- Bootstrap 5 компоненты
- DataGrid, Charts, Modals
- https://blazorise.com/

### 2. **MudBlazor**
- Material Design
- Красивые компоненты
- https://mudblazor.com/

### 3. **Radzen Blazor**
- Много компонентов
- Бесплатная версия
- https://blazor.radzen.com/

---

## 📊 Итоговая рекомендация

### Для вашего проекта оптимально:

```
✅ Blazor Server (интеграция в CopyTrading проект)
✅ SignalR для real-time обновлений
✅ RealtimeUpdateService подписывается на DataBusEvents
✅ Blazorise для UI компонентов
✅ Chart.js для графиков
```

### Причины:
1. **Быстрый старт** - не нужно создавать API, используем сервисы напрямую
2. **Real-time из коробки** - SignalR + DataBusEvents
3. **Меньше кода** - переиспользуем существующую логику
4. **Единый проект** - проще деплоить и поддерживать

---

## 🚀 Следующий шаг

Хотите чтобы я:
1. **Создал базовый Blazor проект** с примером страницы Positions?
2. **Реализовал RealtimeUpdateService** и SignalR Hub?
3. **Настроил Startup.cs** для Blazor Server?
4. **Создал отдельный проект** CopyTrading.BlazorUI?

Скажите что предпочитаете и я начну реализацию! 🎯
