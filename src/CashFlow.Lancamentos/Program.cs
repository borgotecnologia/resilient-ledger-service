using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Connection String apontando para o SQL Server no Docker
var connectionString = "Server=localhost;Database=CashFlowDb;User Id=sa;Password=S3cur3P@ssw0rd!;TrustServerCertificate=True";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Inicializa e cria o banco automaticamente ao rodar a aplicação
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

// ENDPOINT: Criar Lançamento (POST /api/lancamentos)
app.MapPost("/api/lancamentos", async (LancamentoInput input, AppDbContext db) =>
{
    if (input.Valor <= 0 || (input.Tipo != "C" && input.Tipo != "D"))
    {
        return Results.BadRequest("Dados inválidos. Tipo deve ser 'C' (Crédito) ou 'D' (Débito) e o valor maior que zero.");
    }

    var lancamento = new Lancamento
    {
        Id = Guid.NewGuid(),
        Valor = input.Valor,
        Tipo = input.Tipo,
        Data = DateTime.UtcNow
    };

    // 1. Persistência Soberana e Síncrona (SQL Server) - Garantia ACID
    db.Lancamentos.Add(lancamento);
    await db.SaveChangesAsync();

    // 2. Publicação Assíncrona (RabbitMQ) com Tratamento de Erro - Resiliência de Receita!
    try
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "lancamentos_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

        var messageJson = JsonSerializer.Serialize(lancamento);
        var body = Encoding.UTF8.GetBytes(messageJson);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true; // Garante persistência da mensagem física no broker

        channel.BasicPublish(exchange: "", routingKey: "lancamentos_queue", basicProperties: properties, body: body);
    }
    catch (Exception ex)
    {
        // Se a mensageria cair, a API não quebra. O lançamento já está salvo de forma segura no SQL Server local!
        Console.WriteLine($"[AVISO - RESILIÊNCIA] Falha ao publicar no RabbitMQ, mas o dado foi salvo localmente: {ex.Message}");
    }

    return Results.Created($"/api/lancamentos/{lancamento.Id}", lancamento);
});

app.Run();

public class Lancamento
{
    public Guid Id { get; set; }
    public decimal Valor { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public DateTime Data { get; set; }
}

public record LancamentoInput(decimal Valor, string Tipo);

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
}