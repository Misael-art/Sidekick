using System.Text;

namespace Ajudante.Nodes.Common;

internal static class CsvUtility
{
    public static List<Dictionary<string, string>> ReadRows(string content, char delimiter, bool hasHeaders)
    {
        var rows = ParseRecords(content, delimiter);
        if (rows.Count == 0)
            return new List<Dictionary<string, string>>();

        var headers = hasHeaders
            ? rows[0]
            : Enumerable.Range(1, rows.Max(r => r.Count)).Select(i => $"Column{i}").ToList();

        var startIndex = hasHeaders ? 1 : 0;
        var results = new List<Dictionary<string, string>>();
        for (var rowIndex = startIndex; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var item = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                item[headers[i]] = i < row.Count ? row[i] : string.Empty;
            }

            results.Add(item);
        }

        return results;
    }

    public static string WriteRows(IEnumerable<IDictionary<string, string>> rows, char delimiter, bool includeHeaders)
    {
        var rowList = rows.ToList();
        if (rowList.Count == 0)
            return string.Empty;

        var headers = rowList.SelectMany(r => r.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var lines = new List<string>();
        if (includeHeaders)
            lines.Add(string.Join(delimiter, headers.Select(value => Escape(value, delimiter))));

        foreach (var row in rowList)
        {
            lines.Add(string.Join(delimiter, headers.Select(header => Escape(row.TryGetValue(header, out var value) ? value : string.Empty, delimiter))));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static List<List<string>> ParseRecords(string content, char delimiter)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentCell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < content.Length && content[i + 1] == '"')
                {
                    currentCell.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    currentCell.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                continue;
            }

            if (c == delimiter)
            {
                currentRow.Add(currentCell.ToString());
                currentCell.Clear();
                continue;
            }

            if (c == '\r')
                continue;

            if (c == '\n')
            {
                currentRow.Add(currentCell.ToString());
                currentCell.Clear();
                rows.Add(currentRow);
                currentRow = new List<string>();
                continue;
            }

            currentCell.Append(c);
        }

        currentRow.Add(currentCell.ToString());
        if (currentRow.Count > 1 || currentRow[0].Length > 0)
            rows.Add(currentRow);

        return rows;
    }

    private static string Escape(string value, char delimiter)
    {
        if (value.Contains(delimiter) || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
