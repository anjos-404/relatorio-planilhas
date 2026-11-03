using RelatorioPlanilhas.Vendas;

namespace RelatorioPlanilhas.Relatorio;

public record Linha(string Nome, int Quantidade, decimal Total, decimal Percentual);

public record LinhaMeta(string Vendedor, string Mes, decimal Vendido, decimal Meta)
{
    public decimal Atingido => Meta == 0 ? 0 : Vendido / Meta;
}

public record Resumo(
    decimal TotalGeral,
    int Itens,
    int Pedidos,
    decimal TicketMedio,
    DateOnly? Inicio,
    DateOnly? Fim,
    List<Linha> PorVendedor,
    List<Linha> PorCategoria,
    List<Linha> PorMes,
    List<Linha> TopProdutos,
    List<LinhaMeta> Metas,
    List<Problema> Problemas);

public static class CalculadoraResumo
{
    public static Resumo Calcular(ResultadoImportacao importacao, Dictionary<string, decimal>? metasMensais = null)
    {
        var vendas = importacao.Vendas;
        var total = vendas.Sum(v => v.Total);

        List<Linha> Agrupar(Func<Venda, string> chave, int? limite = null)
        {
            var linhas = vendas
                .GroupBy(chave)
                .Select(g => new Linha(g.Key, g.Sum(v => v.Quantidade), g.Sum(v => v.Total), total == 0 ? 0 : g.Sum(v => v.Total) / total))
                .OrderByDescending(l => l.Total)
                .ToList();
            return limite is null ? linhas : linhas.Take(limite.Value).ToList();
        }

        var porMes = vendas
            .GroupBy(v => new DateOnly(v.Data.Year, v.Data.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new Linha(g.Key.ToString("MM/yyyy"), g.Sum(v => v.Quantidade), g.Sum(v => v.Total), total == 0 ? 0 : g.Sum(v => v.Total) / total))
            .ToList();

        var metas = new List<LinhaMeta>();
        if (metasMensais is { Count: > 0 })
        {
            metas = vendas
                .GroupBy(v => (v.Vendedor, Mes: new DateOnly(v.Data.Year, v.Data.Month, 1)))
                .Where(g => metasMensais.ContainsKey(g.Key.Vendedor))
                .OrderBy(g => g.Key.Mes).ThenBy(g => g.Key.Vendedor)
                .Select(g => new LinhaMeta(g.Key.Vendedor, g.Key.Mes.ToString("MM/yyyy"), g.Sum(v => v.Total), metasMensais[g.Key.Vendedor]))
                .ToList();
        }

        return new Resumo(
            TotalGeral: total,
            Itens: vendas.Sum(v => v.Quantidade),
            Pedidos: vendas.Count,
            TicketMedio: vendas.Count == 0 ? 0 : total / vendas.Count,
            Inicio: vendas.Count == 0 ? null : vendas.Min(v => v.Data),
            Fim: vendas.Count == 0 ? null : vendas.Max(v => v.Data),
            PorVendedor: Agrupar(v => v.Vendedor),
            PorCategoria: Agrupar(v => string.IsNullOrEmpty(v.Categoria) ? "(sem categoria)" : v.Categoria),
            PorMes: porMes,
            TopProdutos: Agrupar(v => v.Produto, limite: 5),
            Metas: metas,
            Problemas: importacao.Problemas);
    }
}
