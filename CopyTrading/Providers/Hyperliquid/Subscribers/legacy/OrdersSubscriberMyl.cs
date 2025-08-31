using CopyTrading.Mappers;
using CopyTrading.Models.Orders;
using CopyTrading.ProviderModels.FetchOrder;
using CopyTrading.ProviderModels.Suscribe;
using CopyTrading.ProviderModels.WsOrder;
using System.Data;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WatsonWebsocket;

namespace CopyTrading.Providers.Hyperliquid.Subscribers.legacy;

public class OrdersSubscriberMyl(ILogger<OrdersSubscriberMyl> logger)
{
    private TimeSpan _delayBetwenReconnect = TimeSpan.FromSeconds(1);
    private TimeSpan _pingPongTimeDelay = TimeSpan.FromSeconds(30);

    private readonly Uri _wsOrderUri = new("wss://api.hyperliquid.xyz/ws");

    private Dictionary<string, SubscribeInfo> _subscribes = [];
    private readonly ILogger<OrdersSubscriberMyl> _logger = logger;

    public event EventHandler<OriginalOrder> NewOrder;

    public async Task<OriginalOrder[]> GetHistoricalOrdersForWallet(string wallet)
    {
        string apiUrl = "https://api.hyperliquid.xyz/info";
        using var httpClient = new HttpClient();

        var requestBody = new
        {
            type = "historicalOrders",
            user = wallet
        };
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        var response = await httpClient.PostAsync(apiUrl, jsonContent);
        if (response.IsSuccessStatusCode)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();

            var dto = JsonSerializer.Deserialize<FetchOrderRootModel[]>(jsonResponse);

            var orders = dto.Select(x => x.ToBll(wallet)).ToArray();

            return orders;
        }
        else
        {
            Console.WriteLine($"Ошибка при получении истории ордеров: {response.StatusCode}");
        }

        return [];
    }

    public async Task SubscribeToNewOrders(string wallet)
    {
        if (_subscribes.ContainsKey(wallet)) return;

        await SubscribeAndRetryToNewOrders(wallet);
    }

    #region SubscribeToNewOrders

    private async Task SubscribeAndRetryToNewOrders(string wallet)
    {
        var tokenSource = new CancellationTokenSource();
        var client = InitializeClient();

        if (_subscribes.TryGetValue(wallet, out SubscribeInfo? value))
        {
            value.Client.Dispose();
            value.TokenSource.Cancel();

            value.RecconnectCount++;

            value.Status = SubscribeStatus.Reconnect;
            value.Client = client;
            value.TokenSource = tokenSource;
        }
        else
        {
            _subscribes.Add(wallet, new SubscribeInfo()
            {
                Status = SubscribeStatus.None,
                Client = client,
                TokenSource = tokenSource
            });
        }

        var res = await SendSubscribeMessage(wallet);

        if (res)
        {
            client.ServerDisconnected += async (s, arg) => await NewOrderDisconnected(s, arg, wallet, tokenSource.Token);
            client.MessageReceived += (s, arg) => NewOrderMessageReceived(s, arg, wallet);

            SendHeartBeat(client, wallet, tokenSource.Token);
        }
        else
        {
            await SubscribeAndRetryToNewOrders(wallet);
        }
    }

    private WatsonWsClient InitializeClient()
    {
        var client = new WatsonWsClient(_wsOrderUri);

        return client;
    }

    private async Task NewOrderDisconnected(object s, EventArgs arg, string wallet, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        Console.WriteLine($"{DateTime.Now} Server disconnected! RecconnectCount: {_subscribes[wallet].RecconnectCount}");

        _subscribes[wallet].Status = SubscribeStatus.Disconnected;

        await SubscribeAndRetryToNewOrders(wallet);
    }

    private async Task<bool> SendSubscribeMessage(string wallet)
    {
        var client = _subscribes[wallet].Client;

        try
        {
            Console.WriteLine($"Подключаемся...");
            client.StartWithTimeout(timeout: _delayBetwenReconnect.Seconds);
            var res = await client.SendAsync(GetSubscribeMessage(wallet), WebSocketMessageType.Text);
            Console.WriteLine($"Подключение {res}");

            return res;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка ...{ex.Message}");
            return false;
        }
    }

    private string GetSubscribeMessage(string wallet)
    {
        return $$"""
            {
              "method": "subscribe",
              "subscription": {
                "type": "orderUpdates",
                "user": "{{wallet}}"
              }
            }
            """;
    }

    private async Task SendHeartBeat(WatsonWsClient client, string wallet, CancellationToken token)
    {
        string heartbeatMessage = """
            {
              "method": "ping"
            }
            """;

        while (!token.IsCancellationRequested)
        {
            await Task.Delay(_pingPongTimeDelay, token);
            try
            {
                if (_subscribes[wallet].Status == SubscribeStatus.Connected)
                {
                    await client.SendAsync(heartbeatMessage, WebSocketMessageType.Text, token);
                    Console.WriteLine($"{DateTime.Now} Heartbeat sent");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки heartbeat: {ex.Message}");
            }
        }
    }

    private void NewOrderMessageReceived(object? sender, MessageReceivedEventArgs e, string wallet)
    {
        var message = string.Empty;

        try
        {
            message = Encoding.UTF8.GetString(e.Data);

            using var doc = JsonDocument.Parse(message);
            string? method = doc.RootElement.GetProperty("channel").GetString();

            switch (method)
            {
                case "subscriptionResponse":
                    var subscriptionResponse = JsonSerializer.Deserialize<WsSubscriptionResponse.Rootobject>(message);
                    Console.WriteLine($"Подписались на {subscriptionResponse.data.subscription.user}");
                    _subscribes[wallet].Status = SubscribeStatus.Connected;
                    break;
                case "pong":
                    break;
                case "orderUpdates":
                    var wsOrder = JsonSerializer.Deserialize<WsOrderRootModel>(message);
                    var orders = wsOrder.data.Select(x => x.ToBll(wallet)).ToArray();
                    foreach (var order in orders)
                    {
                        NewOrder?.Invoke(this, order);
                    }
                    break;
                default:
                    _logger.LogWarning($"MessageReceived Неизвестный тип сообщения: {method}. Сообщение: {message}", method, message);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при декодировании сообщения WebSocket: {message}");
        }
    }

    #endregion
}