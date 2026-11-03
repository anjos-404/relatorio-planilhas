using System.Globalization;
using System.Text;

namespace RelatorioPlanilhas.Vendas;

public record Venda(DateOnly Data, string Vendedor, string Produto, string Categoria, int Quantidade, decimal PrecoUnitario)
{
    public decimal Total => Quantidade * PrecoUnitario;
}

public record Problema(int Linha, string Motivo);

public record ResultadoImportacao(List<Venda> Vendas, List<Problema> Problemas);

/// <summary>
/// Transforma as linhas da aba de vendas em objetos, achando as colunas pelo nome
/// do cabeçalho (sem depender da ordem nem de acento/maiúscula) e anotando as
/// linhas com problema em vez de parar tudo.
/// </summary>
public static class ImportadorVendas
{
    private static readonly string[] Colunas = ["data", "vendedor", "produto", "categoria", "quantidade", "preco unitario"];

    public static ResultadoImportacao Importar(List<string[]> linhas)
    {
        var vendas = new List<Venda>();
        var problemas = new List<Problema>();

        var indiceCabecalho = linhas.FindIndex(l => l.Any(c => !string.IsNullOrWhiteSpace(c)));
        if (indiceCabecalho < 0) return new ResultadoImportacao(vendas, [new Problema(1, "Aba vazia.")]);

        var cabecalho = linhas[indiceCabecalho].Select(Normalizar).ToList();
        var posicao = new Dictionary<string, int>();
        foreach (var coluna in Colunas)
        {
            var i = cabecalho.IndexOf(coluna);
            if (i < 0)
                return new ResultadoImportacao(vendas, [new Problema(indiceCabecalho + 1, $"Coluna \"{coluna}\" não encontrada no cabeçalho.")]);
            posicao[coluna] = i;
        }

        for (var i = indiceCabecalho + 1; i < linhas.Count; i++)
        {
            var linha = linhas[i];
            var numeroExcel = i + 1;
            if (linha.All(string.IsNullOrWhiteSpace)) continue;

            string Celula(string coluna) => posicao[coluna] < linha.Length ? linha[posicao[coluna]].Trim() : string.Empty;

            var erros = new List<string>();
            if (!TentarLerData(Celula("data"), out var data)) erros.Add("data inválida");
            var vendedor = Celula("vendedor");
            if (vendedor.Length == 0) erros.Add("vendedor em branco");
            var produto = Celula("produto");
            if (produto.Length == 0) erros.Add("produto em branco");
            if (!int.TryParse(Celula("quantidade"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantidade) || quantidade <= 0)
                erros.Add($"quantidade inválida (\"{Celula("quantidade")}\")");
            if (!decimal.TryParse(Celula("preco unitario"), NumberStyles.Number, CultureInfo.InvariantCulture, out var preco) || preco < 0)
                erros.Add("preço unitário inválido ou em branco");

            if (erros.Count > 0)
                problemas.Add(new Problema(numeroExcel, string.Join("; ", erros)));
            else
                vendas.Add(new Venda(data, vendedor, produto, Celula("categoria"), quantidade, preco));
        }

        return new ResultadoImportacao(vendas, problemas);
    }

    /// <summary>O Excel guarda datas como número de dias desde 1899-12-30; também aceita texto dd/mm/aaaa.</summary>
    public static bool TentarLerData(string valor, out DateOnly data)
    {
        if (double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial > 0)
        {
            data = DateOnly.FromDateTime(DateTime.FromOADate(serial));
            return true;
        }
        return DateOnly.TryParseExact(valor, ["dd/MM/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out data);
    }

    public static string Normalizar(string texto)
    {
        var decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
