using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Core.Rules
{
    public sealed class RuleWorkbookParser
    {
        public RuleImportResult Parse(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            RuleImportResult result = new();

            try
            {
                XlsxTable table = XlsxTableReader.ReadFirstWorksheet(path);
                if (table.Rows.Count == 0)
                {
                    result.Errors.Add("The workbook does not contain any rows.");
                    return result;
                }

                int headerIndex = table.Rows.FindIndex(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)));

                if (headerIndex < 0)
                {
                    result.Errors.Add("The workbook does not contain a header row.");
                    return result;
                }

                Dictionary<string, int> headers = BuildHeaderMap(table.Rows[headerIndex]);

                if (!headers.ContainsKey("description"))
                {
                    result.Errors.Add("The rules workbook must contain a Description column.");
                    return result;
                }

                if (!headers.ContainsKey("type"))
                {
                    result.Errors.Add("The rules workbook must contain a Type column.");
                    return result;
                }

                for (int rowIndex = headerIndex + 1; rowIndex < table.Rows.Count; rowIndex++)
                {
                    IReadOnlyList<string> row = table.Rows[rowIndex];
                    string description = Get(row, headers, "description");
                    string type = Get(row, headers, "type");

                    if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(type))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(description))
                    {
                        result.Errors.Add($"Row {rowIndex + 1}: Description is required.");
                        continue;
                    }

                    RuleImportRow rule = new()
                    {
                        RowNumber = rowIndex + 1,
                        RuleName = FirstNonWhite(Get(row, headers, "rule name"), description),
                        Description = description,
                        Type = type,
                        DescriptionMatchMode = ParseMatchMode(FirstNonWhite(Get(row, headers, "description matching"), Get(row, headers, "description match")), RuleMatchModes.Exact),
                        TypeMatchMode = ParseMatchMode(FirstNonWhite(Get(row, headers, "type matching"), Get(row, headers, "type match")), RuleMatchModes.Exact),
                        Category = NullIfWhite(Get(row, headers, "category")),
                        SpendingClass = FirstNonWhiteOrNull(Get(row, headers, "category type"), Get(row, headers, "spending class"), Get(row, headers, "class")),
                        Merchant = NullIfWhite(Get(row, headers, "merchant")),
                        Priority = ParseInt(Get(row, headers, "priority"), 100),
                        IsIncome = ParseBoolOrNull(Get(row, headers, "is income")),
                        IsTransfer = ParseBoolOrNull(Get(row, headers, "is transfer")),
                        Active = ParseBool(Get(row, headers, "active"), true),
                        Notes = NullIfWhite(Get(row, headers, "notes"))
                    };

                    result.Rows.Add(rule);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException)
            {
                result.Errors.Add($"Unable to read rules workbook: {ex.Message}");
            }

            return result;
        }

        private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> headerRow)
        {
            Dictionary<string, int> headers = new(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < headerRow.Count; index++)
            {
                string normalized = Normalize(headerRow[index]);

                if (!string.IsNullOrWhiteSpace(normalized) && !headers.ContainsKey(normalized))
                {
                    headers.Add(normalized, index);
                }
            }

            return headers;
        }

        private static string Get(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> headers, string header)
        {
            if (!headers.TryGetValue(header, out int index) || index >= row.Count)
            {
                return string.Empty;
            }

            return row[index].Trim();
        }

        private static string Normalize(string value)
        {
            return value.Trim().Replace("_", " ", StringComparison.Ordinal).ToLowerInvariant();
        }

        private static string FirstNonWhite(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string? FirstNonWhiteOrNull(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private static string? NullIfWhite(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
        }

        private static string ParseMatchMode(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string normalized = value.Trim().Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);

            return normalized.ToLowerInvariant() switch
            {
                "exact" => RuleMatchModes.Exact,
                "contains" => RuleMatchModes.Contains,
                "startswith" => RuleMatchModes.StartsWith,
                _ => fallback
            };
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool? parsed = ParseBoolOrNull(value);
            return parsed ?? fallback;
        }

        private static bool? ParseBoolOrNull(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string normalized = value.Trim().ToLowerInvariant();

            return normalized switch
            {
                "true" or "yes" or "y" or "1" or "x" => true,
                "false" or "no" or "n" or "0" => false,
                _ => null
            };
        }
    }

    internal sealed class XlsxTable
    {
        public List<List<string>> Rows { get; } = new();
    }

    internal static class XlsxTableReader
    {
        private static readonly XNamespace _spreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace _relationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace _officeRelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static XlsxTable ReadFirstWorksheet(string path)
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            List<string> sharedStrings = LoadSharedStrings(archive);
            string worksheetPath = ResolveFirstWorksheetPath(archive);
            ZipArchiveEntry sheetEntry = archive.GetEntry(worksheetPath) ?? throw new InvalidDataException($"Worksheet '{worksheetPath}' was not found.");
            using Stream stream = sheetEntry.Open();
            XDocument document = XDocument.Load(stream);
            XlsxTable table = new();

            foreach (XElement rowElement in document.Descendants(_spreadsheetNs + "row"))
            {
                Dictionary<int, string> values = new();

                foreach (XElement cell in rowElement.Elements(_spreadsheetNs + "c"))
                {
                    string reference = (string?)cell.Attribute("r") ?? string.Empty;
                    int column = GetColumnIndex(reference);

                    if (column < 0)
                    {
                        column = values.Count;
                    }

                    values[column] = ReadCellValue(cell, sharedStrings);
                }

                int maxColumn = values.Count == 0 ? -1 : values.Keys.Max();
                List<string> row = new();

                for (int index = 0; index <= maxColumn; index++)
                {
                    row.Add(values.TryGetValue(index, out string? value) ? value : string.Empty);
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private static List<string> LoadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");

            if (entry is null)
            {
                return new List<string>();
            }

            using Stream stream = entry.Open();
            XDocument document = XDocument.Load(stream);
            return document.Descendants(_spreadsheetNs + "si").Select(si => string.Concat(si.Descendants(_spreadsheetNs + "t").Select(t => t.Value))).ToList();
        }

        private static string ResolveFirstWorksheetPath(ZipArchive archive)
        {
            ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException("xl/workbook.xml was not found.");
            ZipArchiveEntry relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidDataException("xl/_rels/workbook.xml.rels was not found.");
            XDocument workbook;

            using (Stream stream = workbookEntry.Open())
            {
                workbook = XDocument.Load(stream);
            }

            string relationshipId = workbook.Descendants(_spreadsheetNs + "sheet")
                .Select(sheet => (string?)sheet.Attribute(_officeRelationshipNs + "id"))
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? throw new InvalidDataException("No worksheets were found in the workbook.");

            XDocument relationships;

            using (Stream stream = relsEntry.Open())
            {
                relationships = XDocument.Load(stream);
            }

            string target = relationships.Descendants(_relationshipNs + "Relationship")
                .Where(rel => string.Equals((string?)rel.Attribute("Id"), relationshipId, StringComparison.OrdinalIgnoreCase))
                .Select(rel => (string?)rel.Attribute("Target"))
                .FirstOrDefault(targetValue => !string.IsNullOrWhiteSpace(targetValue))
                ?? throw new InvalidDataException("Unable to resolve first worksheet relationship.");

            string normalized = target.Replace('\\', '/');

            if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.TrimStart('/');
            }
            else if (!normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "xl/" + normalized;
            }

            return normalized;
        }

        private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
        {
            string type = (string?)cell.Attribute("t") ?? string.Empty;

            if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
            {
                string indexText = cell.Element(_spreadsheetNs + "v")?.Value ?? string.Empty;
                return int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : string.Empty;
            }

            if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(cell.Descendants(_spreadsheetNs + "t").Select(t => t.Value));
            }

            return cell.Element(_spreadsheetNs + "v")?.Value ?? string.Empty;
        }

        private static int GetColumnIndex(string reference)
        {
            int result = 0;
            int letters = 0;

            foreach (char character in reference)
            {
                if (!char.IsLetter(character))
                {
                    break;
                }

                result = (result * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
                letters++;
            }

            return letters == 0 ? -1 : result - 1;
        }
    }
}
