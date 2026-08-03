using System.Text;

namespace PersonalFinanceManager.Core.Imports
{
    public sealed class CsvRecordReader
    {
        public IReadOnlyList<CsvRecord> Read(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            List<CsvRecord> records = new();
            List<string> fields = new();
            StringBuilder field = new();
            bool inQuotes = false;
            int line = 1;
            int startLine = 1;

            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];

                if (inQuotes)
                {
                    if (current == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
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
                        if (current == '\n')
                        {
                            line++;
                        }

                        field.Append(current);
                    }

                    continue;
                }

                if (current == '"')
                {
                    inQuotes = true;
                    continue;
                }

                if (current == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    continue;
                }

                if (current == '\r' || current == '\n')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add(new CsvRecord(startLine, line, fields.ToArray()));
                    fields.Clear();

                    if (current == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    line++;
                    startLine = line;
                    continue;
                }

                field.Append(current);
            }

            if (inQuotes)
            {
                throw new FormatException($"CSV contains an unterminated quoted field starting near line {startLine}.");
            }

            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                records.Add(new CsvRecord(startLine, line, fields.ToArray()));
            }

            return records;
        }
    }
}
