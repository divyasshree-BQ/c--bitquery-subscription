using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static ClientWebSocket _client = new ClientWebSocket();
    private static readonly Uri ServerUri = new Uri("wss://streaming.bitquery.io/graphql?token=ory_at_...");

    public static async Task Main()
    {
        await ConnectAndSubscribeAsync();
    }

    private static async Task ConnectAndSubscribeAsync()
    {
        try
        {
            _client.Options.AddSubProtocol("graphql-ws");
            Console.WriteLine("Connecting to Bitquery WebSocket...");
            await _client.ConnectAsync(ServerUri, CancellationToken.None);
            Console.WriteLine("Connected!");

            // Send connection initialization message
            string initMessage = @"{ ""type"": ""connection_init"" }";
            await SendMessageAsync(initMessage);
            Console.WriteLine("Sent connection_init message.");


            await Task.Delay(100); // Short delay to allow server to process init

            // Send subscription message
            string subscriptionMessage = @"
            {
                ""type"": ""subscribe"",
                ""payload"": {
                    ""query"": ""subscription ($network: evm_network) { EVM(network: $network) { DEXTrades(where: {Trade: {Buy: {AmountInUSD: {gt: \""10\""}}}}) { Trade { Buy { AmountInUSD Buyer Currency { Name Symbol SmartContract } Amount } Dex { OwnerAddress SmartContract ProtocolFamily ProtocolVersion ProtocolName Pair { SmartContract } } Sell { AmountInUSD Currency { Name Symbol SmartContract } Amount } } Block { Time } Transaction { Hash Cost CostInUSD ValueInUSD Value Type To } } } }"",
                    ""variables"": { ""network"": ""eth"" }
                }
            }";

            await SendMessageAsync(subscriptionMessage);
            Console.WriteLine("Subscription message sent.");

            // Keep receiving messages
            await ReceiveMessagesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static async Task SendMessageAsync(string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task ReceiveMessagesAsync()
    {
        byte[] buffer = new byte[8192]; // Increased buffer size

        while (_client.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);

            Console.WriteLine($"Received: {receivedMessage}");

            if (receivedMessage.Contains("\"type\":\"connection_ack\""))
            {
                Console.WriteLine(" Connection acknowledged by server. Now subscribing...");
            }
            else if (receivedMessage.Contains("\"type\":\"ka\""))
            {
                Console.WriteLine("Keep-alive message received.");
            }
            else if (receivedMessage.Contains("\"type\":\"connection_error\""))
            {
                Console.WriteLine(" Connection error! Check your token and request.");
                break;
            }
        }
    }
}
