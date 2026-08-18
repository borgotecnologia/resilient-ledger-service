using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Injeta o mecanismo de Cache em Memória do .NET para aguentar os picos de 50 req/s
builder.Services.AddMemoryCache();

// Conexão com o MongoDB do Docker (Camada Cloud de Leitura Rápida)
var mongoClient = new MongoClient("mongodb://localhost:27017");
var mongoDatabase = mongoClient.GetDatabase("CashFlowConsolidadoDb");
builder.Services.AddSingleton(mongoDatabase);

// Registra o Consumidor Resiliente em segundo plano
builder.Services.AddHostedService<LancamentoQueueConsumer>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// ENDPOINT: Consulta rápida de saldo diário com Cache e Resiliência (GET /api/consolidado/{data})
app.MapGet("/api/consolidado/{data}", async (string data, IMongoDatabase db, IMemoryCache cache) =>
{
    if (!DateTime.TryParse(data, out var parsedDate))
    {
        return Results.BadRequest("Formato de data inválido. Use o padrão AAAA-MM-DD.");
    }

    string chaveDia = parsedDate.ToString("yyyy-MM-dd");
    string cacheKey = $"saldo_{chaveDia}";

    // Tática de Alta Performance: Tenta ler o saldo do cache local primeiro
    if (!cache.TryGetValue(cacheKey, out decimal saldo))
    {
        var collection = db.GetCollection<BsonDocument>("SaldosDiarios");
        var filter = Builders<BsonDocument>.Filter.Eq("_id", chaveDia);
        var doc = await collection.Find(filter).FirstOrDefaultAsync();

        saldo = doc != null ? doc["saldo"].AsDecimal : 0.00m;

        // Salva no cache por 3 segundos para amortecer picos massivos de requisições de leitura
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(3));
        
        cache.Set(cacheKey, saldo, cacheOptions);
        Console.WriteLine($"[CACHE MISS] Saldo do dia {chaveDia} lido do MongoDB e cacheado.");
    }
    else
    {
        Console.WriteLine($"[CACHE HIT] Saldo do dia {chaveDia} entregue instantaneamente via memória.");
    }

    return Results.Ok(new { Data = data, Saldo = saldo, Provedor = cache.TryGetValue(cacheKey, out _) ? "MemoryCache" : "MongoDB" });
});

app.Run();

// --- CONSUMIDOR DA FILA COM REGRA DE IDEMPOTÊNCIA (INBOX PATTERN) ---
public class LancamentoQueueConsumer : BackgroundService
{
    private readonly IMongoDatabase _mongoDatabase;
    private IConnection? _connection;
    private IModel? _channel;

    public LancamentoQueueConsumer(IMongoDatabase mongoDatabase)
    {
        _mongoDatabase = mongoDatabase;
        InitializeRabbitMQ();
    }

    private void InitializeRabbitMQ()
    {
        try
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: "lancamentos_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO INICIALIZAÇÃO] Falha de conexão com o broker: {ex.Message}");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null) return Task.CompletedTask;

        stoppingToken.ThrowIfCancellationRequested();

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                var lancamento = JsonSerializer.Deserialize<LancamentoEvent>(message);
                if (lancamento != null)
                {
                    // 1. CHECAGEM DE IDEMPOTÊNCIA: Tenta marcar a transação como processada
                    bool transacaoNova = await MarcarComoProcessada(lancamento.Id);

                    if (transacaoNova)
                    {
                        // 2. Só atualiza o saldo se a transação nunca tiver sido computada antes!
                        await AtualizarSaldoConsolidadoNoSQL(lancamento);
                        _channel.BasicAck(ea.DeliveryTag, false); // Confirmação de processamento com segurança
                    }
                    else
                    {
                        Console.WriteLine($"[DEDUPLICAÇÃO] Evento duplicado detectado para transação {lancamento.Id}. Descartando sem reprocessar.");
                        _channel.BasicAck(ea.DeliveryTag, false); // Dá Ack para limpar da fila, pois já foi tratada
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FALHA PROCESSAMENTO] Enviando para fila de erro: {ex.Message}");
                _channel.BasicNack(ea.DeliveryTag, false, false); // Descarta sem re-enfileirar para evitar loops infinitos
            }
        };

        _channel.BasicConsume(queue: "lancamentos_queue", autoAck: false, consumer: consumer);
        return Task.CompletedTask;
    }

    // Inbox Pattern: Insere o ID da transação em uma tabela de controle com chave primária única no MongoDB
    private async Task<bool> MarcarComoProcessada(Guid transactionId)
    {
        var inboxCollection = _mongoDatabase.GetCollection<BsonDocument>("InboxTransacoes");
        try
        {
            var doc = new BsonDocument { { "_id", transactionId.ToString() }, { "processadoEm", DateTime.UtcNow } };
            await inboxCollection.InsertOneAsync(doc);
            return true; // Transação processada pela primeira vez com sucesso!
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false; // Chave duplicada encontrada. A transação já foi processada anteriormente!
        }
    }

    private async Task AtualizarSaldoConsolidadoNoSQL(LancamentoEvent lancamento)
    {
        var collection = _mongoDatabase.GetCollection<BsonDocument>("SaldosDiarios");
        string chaveDia = lancamento.Data.ToString("yyyy-MM-dd");
        decimal valorAlteracao = lancamento.Tipo == "C" ? lancamento.Valor : -lancamento.Valor;

        var filter = Builders<BsonDocument>.Filter.Eq("_id", chaveDia);
        
        // Operação Atômica do Mongo ($inc): Garante consistência em concorrência extrema
        var update = Builders<BsonDocument>.Update
            .Inc("saldo", valorAlteracao)
            .Set("ultimaAtualizacao", DateTime.UtcNow);

        await collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        Console.WriteLine($"[NOSQL SYNC] Saldo do dia {chaveDia} atualizado em {valorAlteracao:C} (ID: {lancamento.Id}).");
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}

public class LancamentoEvent
{
    public Guid Id { get; set; }
    public decimal Valor { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public DateTime Data { get; set; }
}