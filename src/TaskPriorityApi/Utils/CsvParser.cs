using System.Text;

namespace TaskPriorityApi.Utils;

public static class CsvParser
{
    public static List<string[]> Parse(string content)
    {
        var rows = new List<string[]>();
        var field = new StringBuilder();
        var row = new List<string>();
        var inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                row.Add(field.ToString().Trim());
                field.Clear();
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                row.Add(field.ToString().Trim());
                field.Clear();
                if (row.Any(f => f.Length > 0)) rows.Add(row.ToArray());
                row.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString().Trim());
            if (row.Any(f => f.Length > 0)) rows.Add(row.ToArray());
        }

        return rows;
    }
}
