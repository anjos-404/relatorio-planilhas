using System.Globalization;
using System.Text;
using RelatorioPlanilhas.Excel;
using RelatorioPlanilhas.Relatorio;
using RelatorioPlanilhas.Vendas;

// Uso: dotnet run -- <planilha.xlsx> [pasta-de-saida]
if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Uso: RelatorioPlanilhas <planilha.xlsx> [pasta-de-saida]");
    Console.WriteLine("Lê a aba \"Vendas\" (e \"Metas\", se existir) e gera relatorio.html e resumo.csv.");
    return args.Length == 0 ? 1 : 0;
}

var entrada = args[0];
var pastaSaida = args.Length > 1 ? args[1] : "relatorio";

if (!File.Exists(entrada))
{
    Console.Error.WriteLine($"Arquivo não encontrado: {entrada}");
    return 1;
}

Dictionary<string, List<string[]>> abas;
try
{
    abas = LeitorXlsx.Ler(entrada);
}
catch (Exception erro) when (erro is InvalidDataException or System.Xml.XmlException)
{
    Console.Error.WriteLine($"Não consegui ler a planilha: {erro.Message}");
    return 1;
}

// Usa a aba "Vendas"; se não existir, a primeira aba.
var abaVendas = abas.TryGetValue("Vendas", out var linhasVendas) ? linhasVendas : abas.Values.FirstOrDefault();
if (abaVendas is null)
{
    Console.Error.WriteLine("A planilha não tem nenhuma aba.");
    return 1;
}

var importacao = ImportadorVendas.Importar(abaVendas);
var metas = abas.TryGetValue("Metas", out var linhasMetas) ? LerMetas(linhasMetas) : null;
var resumo = CalculadoraResumo.Calcular(importacao, metas);

if (resumo.Pedidos == 0)
{
    Console.Error.WriteLine("Nenhuma venda válida encontrada.");
    foreach (var p in resumo.Problemas) Console.Error.WriteLine($"  linha {p.Linha}: {p.Motivo}");
    return 1;
}

Directory.CreateDirectory(pastaSaida);
var html = Path.Combine(pastaSaida, "relatorio.html");
var csv = Path.Combine(pastaSaida, "resumo.csv");
File.WriteAllText(html, Geradores.Html(resumo, entrada), Encoding.UTF8);
File.WriteAllText(csv, Geradores.Csv(resumo), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)); // BOM: acentos certos no Excel

Console.WriteLine(Geradores.Texto(resumo));
Console.WriteLine($"Relatório salvo em {html}");
Console.WriteLine($"Resumo em CSV salvo em {csv}");
return 0;

static Dictionary<string, decimal> LerMetas(List<string[]> linhas)
{
    var metas = new Dictionary<string, decimal>();
    foreach (var linha in linhas.Skip(1))
    {
        if (linha.Length >= 2 &&
            !string.IsNullOrWhiteSpace(linha[0]) &&
            decimal.TryParse(linha[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var meta))
        {
            metas[linha[0].Trim()] = meta;
        }
    }
    return metas;
}
