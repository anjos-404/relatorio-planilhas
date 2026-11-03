using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace RelatorioPlanilhas.Excel;

/// <summary>
/// Lê arquivos .xlsx sem biblioteca externa. Um .xlsx é um ZIP com arquivos XML:
/// workbook.xml lista as abas, sharedStrings.xml guarda os textos e cada
/// sheetN.xml tem as células.
/// </summary>
public static class LeitorXlsx
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace NsPkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>Devolve cada aba como uma lista de linhas; cada célula vem como texto (ou vazio).</summary>
    public static Dictionary<string, List<string[]>> Ler(string caminho)
    {
        using var zip = ZipFile.OpenRead(caminho);
        var textos = LerTextosCompartilhados(zip);
        var abas = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);

        var workbook = CarregarXml(zip, "xl/workbook.xml")
            ?? throw new InvalidDataException("Arquivo não parece um .xlsx válido (sem xl/workbook.xml).");
        var relacoes = CarregarXml(zip, "xl/_rels/workbook.xml.rels")?
            .Descendants(NsPkgRel + "Relationship")
            .ToDictionary(r => (string)r.Attribute("Id")!, r => (string)r.Attribute("Target")!)
            ?? new Dictionary<string, string>();

        foreach (var aba in workbook.Descendants(Ns + "sheet"))
        {
            var nome = (string)aba.Attribute("name")!;
            var idRelacao = (string?)aba.Attribute(NsRel + "id");
            if (idRelacao is null || !relacoes.TryGetValue(idRelacao, out var alvo)) continue;

            var caminhoAba = alvo.StartsWith('/') ? alvo.TrimStart('/') : "xl/" + alvo;
            var xmlAba = CarregarXml(zip, caminhoAba);
            if (xmlAba is not null) abas[nome] = LerLinhas(xmlAba, textos);
        }

        return abas;
    }

    private static List<string[]> LerLinhas(XDocument aba, List<string> textos)
    {
        var linhas = new List<string[]>();
        foreach (var linha in aba.Descendants(Ns + "row"))
        {
            var numeroLinha = int.Parse((string)linha.Attribute("r")!, CultureInfo.InvariantCulture);
            // Linhas vazias não aparecem no XML: completa para manter a numeração do Excel.
            while (linhas.Count < numeroLinha - 1) linhas.Add(Array.Empty<string>());

            var celulas = new List<string>();
            foreach (var celula in linha.Elements(Ns + "c"))
            {
                var coluna = IndiceDaColuna((string)celula.Attribute("r")!);
                while (celulas.Count < coluna) celulas.Add(string.Empty);
                celulas.Add(ValorDaCelula(celula, textos));
            }
            linhas.Add(celulas.ToArray());
        }
        return linhas;
    }

    private static string ValorDaCelula(XElement celula, List<string> textos)
    {
        var tipo = (string?)celula.Attribute("t");
        return tipo switch
        {
            "s" => textos[int.Parse(celula.Element(Ns + "v")!.Value, CultureInfo.InvariantCulture)],
            "inlineStr" => string.Concat(celula.Descendants(Ns + "t").Select(t => t.Value)),
            "b" => celula.Element(Ns + "v")?.Value == "1" ? "VERDADEIRO" : "FALSO",
            _ => celula.Element(Ns + "v")?.Value ?? string.Empty,
        };
    }

    /// <summary>"A1" → 0, "B7" → 1, "AA3" → 26.</summary>
    public static int IndiceDaColuna(string referencia)
    {
        var indice = 0;
        foreach (var letra in referencia.TakeWhile(char.IsLetter))
            indice = indice * 26 + (char.ToUpperInvariant(letra) - 'A' + 1);
        return indice - 1;
    }

    private static List<string> LerTextosCompartilhados(ZipArchive zip)
    {
        var xml = CarregarXml(zip, "xl/sharedStrings.xml");
        if (xml is null) return new List<string>();
        // Um texto pode vir em vários pedaços com formatação diferente (<r><t>): junta todos.
        return xml.Descendants(Ns + "si")
            .Select(si => string.Concat(si.Descendants(Ns + "t").Select(t => t.Value)))
            .ToList();
    }

    private static XDocument? CarregarXml(ZipArchive zip, string caminho)
    {
        var entrada = zip.GetEntry(caminho);
        if (entrada is null) return null;
        using var fluxo = entrada.Open();
        return XDocument.Load(fluxo);
    }
}
