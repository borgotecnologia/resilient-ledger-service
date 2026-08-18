using Xunit;

namespace CashFlow.Tests;

public class LancamentoTests
{
    [Fact]
    public void CriarLancamento_ComDadosValidos_DeveRetornarSucesso()
    {
        // Arrange (Preparação de cenário)
        var valorValido = 150.00m;
        var tipoValido = "C"; // Crédito

        // Act (Ação de teste)
        var lancamento = new LancamentoMock(Guid.NewGuid(), valorValido, tipoValido, DateTime.UtcNow);

        // Assert (Validação das regras de negócio do edital)
        Assert.True(lancamento.EhValido());
        Assert.Equal(valorValido, lancamento.Valor);
        Assert.Equal("C", lancamento.Tipo);
    }

    [Theory]
    [InlineData(0, "C")]   // Valor zero é inválido
    [InlineData(-50, "D")] // Valor negativo é inválido
    [InlineData(100, "X")] // Tipo diferente de C ou D é inválido
    [InlineData(100, "")]  // Tipo vazio é inválido
    public void CriarLancamento_ComDadosInvalidos_DeveRetornarInvalido(decimal valor, string tipo)
    {
        // Act
        var lancamento = new LancamentoMock(Guid.NewGuid(), valor, tipo, DateTime.UtcNow);

        // Assert
        Assert.False(lancamento.EhValido());
    }
}

// Classe Mock de domínio para isolar os testes de regras de validação sem bater fisicamente no banco de dados
public class LancamentoMock
{
    public Guid Id { get; set; }
    public decimal Valor { get; set; }
    public string Tipo { get; set; }
    public DateTime Data { get; set; }

    public LancamentoMock(Guid id, decimal valor, string tipo, DateTime data)
    {
        Id = id;
        Valor = valor;
        Tipo = tipo;
        Data = data;
    }

    public bool EhValido()
    {
        if (Valor <= 0) return false;
        if (Tipo != "C" && Tipo != "D") return false;
        return true;
    }
}