using System.Globalization;
using System.Net;
using System.Text;

namespace RelatorioPlanilhas.Relatorio;

public static class Geradores
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Moeda(decimal valor) => valor.ToString("C2", PtBr);
    public static string Percentual(decimal valor) => valor.ToString("P1", PtBr);

    /// <summary>Resumo para o terminal.</summary>
    public static string Texto(Resumo r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Período: {r.Inicio:dd/MM/yyyy} a {r.Fim:dd/MM/yyyy}");
        sb.AppendLine($"Faturamento: {Moeda(r.TotalGeral)} em {r.Pedidos} vendas ({r.Itens} itens) · ticket médio {Moeda(r.TicketMedio)}");
        sb.AppendLine();
        sb.AppendLine("Por vendedor:");
        foreach (var l in r.PorVendedor) sb.AppendLine($"  {l.Nome,-18} {Moeda(l.Total),15}  {Percentual(l.Percentual),7}");
        sb.AppendLine();
        sb.AppendLine("Top produtos:");
        foreach (var l in r.TopProdutos) sb.AppendLine($"  {l.Nome,-18} {Moeda(l.Total),15}  {l.Quantidade,4} un.");
        if (r.Problemas.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{r.Problemas.Count} linha(s) ignorada(s):");
            foreach (var p in r.Problemas) sb.AppendLine($"  linha {p.Linha}: {p.Motivo}");
        }
        return sb.ToString();
    }

    /// <summary>CSV (separado por ponto e vírgula, abre direto no Excel em português).</summary>
    public static string Csv(Resumo r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Grupo;Nome;Quantidade;Total;Percentual");
        void Bloco(string grupo, IEnumerable<Linha> linhas)
        {
            foreach (var l in linhas)
                sb.AppendLine($"{grupo};{Escapar(l.Nome)};{l.Quantidade};{l.Total.ToString("0.00", PtBr)};{(l.Percentual * 100).ToString("0.0", PtBr)}");
        }
        Bloco("Vendedor", r.PorVendedor);
        Bloco("Categoria", r.PorCategoria);
        Bloco("Mês", r.PorMes);
        Bloco("Produto (top 5)", r.TopProdutos);
        return sb.ToString();
    }

    private static string Escapar(string valor) =>
        valor.Contains(';') || valor.Contains('"') ? $"\"{valor.Replace("\"", "\"\"")}\"" : valor;

    /// <summary>Relatório em HTML, para abrir no navegador ou mandar por e-mail.</summary>
    public static string Html(Resumo r, string arquivoOrigem)
    {
        static string E(string s) => WebUtility.HtmlEncode(s);

        string Tabela(string titulo, List<Linha> linhas) =>
            $"""
            <section><h2>{E(titulo)}</h2><table>
            <thead><tr><th>Nome</th><th class="n">Qtd.</th><th class="n">Total</th><th class="n">%</th><th></th></tr></thead>
            <tbody>{string.Concat(linhas.Select(l => $"<tr><td>{E(l.Nome)}</td><td class=\"n\">{l.Quantidade}</td><td class=\"n\">{Moeda(l.Total)}</td><td class=\"n\">{Percentual(l.Percentual)}</td><td class=\"barra\"><span style=\"width:{(l.Percentual * 100).ToString("0.#", CultureInfo.InvariantCulture)}%\"></span></td></tr>"))}</tbody>
            </table></section>
            """;

        var metas = r.Metas.Count == 0 ? "" :
            $"""
            <section><h2>Metas mensais</h2><table>
            <thead><tr><th>Mês</th><th>Vendedor</th><th class="n">Vendido</th><th class="n">Meta</th><th class="n">Atingido</th></tr></thead>
            <tbody>{string.Concat(r.Metas.Select(m => $"<tr><td>{m.Mes}</td><td>{E(m.Vendedor)}</td><td class=\"n\">{Moeda(m.Vendido)}</td><td class=\"n\">{Moeda(m.Meta)}</td><td class=\"n {(m.Atingido >= 1 ? "ok" : "abaixo")}\">{Percentual(m.Atingido)}</td></tr>"))}</tbody>
            </table></section>
            """;

        var problemas = r.Problemas.Count == 0 ? "" :
            $"""
            <section class="alerta"><h2>{r.Problemas.Count} linha(s) ignorada(s)</h2><ul>
            {string.Concat(r.Problemas.Select(p => $"<li>Linha {p.Linha}: {E(p.Motivo)}</li>"))}
            </ul></section>
            """;

        return $$"""
            <!doctype html>
            <html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Relatório de vendas</title>
            <style>
              body { font-family: system-ui, sans-serif; margin: 2rem auto; max-width: 900px; padding: 0 1rem; color: #1c2430; }
              h1 { margin-bottom: .2rem } .sub { color: #5b6573; margin-top: 0 }
              .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .75rem; margin: 1.5rem 0 }
              .card { border: 1px solid #dde2e8; border-radius: 8px; padding: .8rem 1rem } .card b { display: block; font-size: 1.4rem }
              table { width: 100%; border-collapse: collapse; margin-bottom: 1.5rem } th, td { padding: .4rem .5rem; border-bottom: 1px solid #e6e9ee; text-align: left }
              .n { text-align: right; font-variant-numeric: tabular-nums } .barra { width: 25% } .barra span { display: block; height: .6rem; background: #2563eb; border-radius: 3px }
              .ok { color: #15803d; font-weight: 600 } .abaixo { color: #b45309 } .alerta { background: #fff7ed; border: 1px solid #fed7aa; border-radius: 8px; padding: .5rem 1rem }
            </style></head><body>
            <h1>Relatório de vendas</h1>
            <p class="sub">{{E(Path.GetFileName(arquivoOrigem))}} · {{r.Inicio:dd/MM/yyyy}} a {{r.Fim:dd/MM/yyyy}} · gerado em {{DateTime.Now:dd/MM/yyyy HH:mm}}</p>
            <div class="cards">
              <div class="card">Faturamento<b>{{Moeda(r.TotalGeral)}}</b></div>
              <div class="card">Vendas<b>{{r.Pedidos}}</b></div>
              <div class="card">Itens vendidos<b>{{r.Itens}}</b></div>
              <div class="card">Ticket médio<b>{{Moeda(r.TicketMedio)}}</b></div>
            </div>
            {{Tabela("Por vendedor", r.PorVendedor)}}
            {{Tabela("Por categoria", r.PorCategoria)}}
            {{Tabela("Por mês", r.PorMes)}}
            {{Tabela("Top 5 produtos", r.TopProdutos)}}
            {{metas}}
            {{problemas}}
            </body></html>
            """;
    }
}
