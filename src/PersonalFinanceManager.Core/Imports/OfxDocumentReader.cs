using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PersonalFinanceManager.Core.Imports;

internal sealed class OfxFormatException(int line, string message) : FormatException(message)
{
    public int Line { get; } = line;
}

internal sealed record OfxSourceLine(int Line);

internal static class OfxDocumentReader
{
    public const int MaximumFileSize = 16 * 1024 * 1024;
    private const int _maximumDepth = 48;
    private const int _maximumElements = 250_000;
    private static readonly Regex _tagName = new(@"^[A-Za-z][A-Za-z0-9_.]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex _entity = new(@"&(?<name>[^;\s<&>]{1,32});", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly HashSet<string> _scalarTags = new(StringComparer.Ordinal)
    {
        "CODE", "SEVERITY", "MESSAGE", "DTSERVER", "LANGUAGE", "DTPROFUP", "DTACCTUP", "DTUSERUP", "FIID", "FID", "ORG",
        "USERKEY", "TSKEYEXPIRE", "SESSCOOKIE", "ACCESSKEY", "INTU.BID", "INTU.USERID", "INTU.BRANDNAME", "TRNUID", "CLTCOOKIE",
        "CURDEF", "BANKID", "BRANCHID", "ACCTID", "ACCTTYPE", "ACCTKEY", "DTSTART", "DTEND", "TRNTYPE", "DTPOSTED", "DTUSER",
        "DTAVAIL", "TRNAMT", "FITID", "CORRECTFITID", "CORRECTACTION", "REVERSALFITID", "SRVRTID", "CHECKNUM", "REFNUM", "SIC",
        "NAME", "EXTDNAME", "MEMO", "PAYEEID", "ADDR1", "ADDR2", "ADDR3", "CITY", "STATE", "POSTALCODE", "COUNTRY", "PHONE",
        "BALAMT", "DTASOF", "MKTGINFO", "CURSYM", "CURRATE", "DTTRAN", "DTEXPIRE", "TOKEN", "MORE", "INTRATE", "INTRATEPURCH",
        "INTRATECASH", "INTRATEXFER", "REWARDNAME", "REWARDBAL", "AVAILCASH", "CASHADVBALAMT", "DESC", "BALTYPE", "VALUE"
    };

    private static readonly HashSet<string> AggregateTags = new(StringComparer.Ordinal)
    {
        "OFX", "SIGNONMSGSRSV1", "SONRS", "STATUS", "FI", "BANKMSGSRSV1", "STMTTRNRS", "STMTRS", "BANKACCTFROM",
        "CREDITCARDMSGSRSV1", "CCSTMTTRNRS", "CCSTMTRS", "CCACCTFROM", "BANKTRANLIST", "STMTTRN", "LEDGERBAL", "AVAILBAL",
        "BANKTRANLISTP", "STMTTRNP", "CURRENCY", "ORIGCURRENCY", "BANKACCTTO", "CCACCTTO", "PAYEE", "BALLIST", "BAL", "REWARDINFO"
    };

    static OfxDocumentReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static XElement Read(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new OfxFormatException(0, "The file is empty.");
        }

        int offset = bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
        string prefix = Encoding.ASCII.GetString(bytes, offset, Math.Min(bytes.Length - offset, 8192));

        if (prefix.TrimStart().StartsWith("OFXHEADER:", StringComparison.OrdinalIgnoreCase))
        {
            (Dictionary<string, string> header, _) = ReadSgmlHeader(prefix);
            Encoding encoding = SgmlEncoding(header);
            return Read(encoding.GetString(bytes, offset, bytes.Length - offset));
        }

        return ReadXml(() => XmlReader.Create(new MemoryStream(bytes, false), Settings()));
    }

    public static XElement Read(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new OfxFormatException(0, "The file is empty.");
        }

        if (text.Length > MaximumFileSize)
        {
            throw new OfxFormatException(0, "The OFX/QFX file exceeds the 16 MiB import limit.");
        }

        text = text.TrimStart('\uFEFF');

        if (text.TrimStart().StartsWith("OFXHEADER:", StringComparison.OrdinalIgnoreCase))
        {
            (Dictionary<string, string> header, int start) = ReadSgmlHeader(text);
            _ = SgmlEncoding(header);
            return ReadSgml(text, start);
        }

        return ReadXml(() => XmlReader.Create(new StringReader(text), Settings()));
    }

    private static XmlReaderSettings Settings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = MaximumFileSize,
        MaxCharactersFromEntities = 1024,
        IgnoreComments = true,
        CloseInput = true
    };

    private static XElement ReadXml(Func<XmlReader> createReader)
    {
        using (XmlReader validation = createReader())
        {
            int count = 0;

            while (validation.Read())
            {
                if (validation.Depth > _maximumDepth || (validation.NodeType == XmlNodeType.Element && ++count > _maximumElements))
                {
                    throw new OfxFormatException(0, "The OFX document is too deeply nested or contains too many elements.");
                }
            }
        }

        using XmlReader reader = createReader();
        XDocument document = XDocument.Load(reader, LoadOptions.SetLineInfo);
        XProcessingInstruction[] declarations = document.Nodes().OfType<XProcessingInstruction>().Where(node => node.Target == "OFX").ToArray();

        if (declarations.Length != 1)
        {
            throw new OfxFormatException(0, "An OFX 2.x XML declaration is required. Export the original OFX/QFX file.");
        }

        using XmlReader headerReader = XmlReader.Create(new StringReader("<HEADER " + declarations[0].Data + "/>"), Settings());
        XElement header = XElement.Load(headerReader);
        string? version = header.Attribute("VERSION")?.Value;

        if (header.Attribute("OFXHEADER")?.Value != "200" || version is not ("200" or "201" or "202" or "203" or "210" or "211" or "220" or "221" or "230"))
        {
            throw new OfxFormatException(0, "The file declares an unsupported OFX XML version.");
        }

        if (header.Attribute("SECURITY")?.Value != "NONE")
        {
            throw new OfxFormatException(0, "Encrypted OFX files are not supported.");
        }

        if (header.Attribute("OLDFILEUID") is null || header.Attribute("NEWFILEUID") is null)
        {
            throw new OfxFormatException(0, "The OFX XML declaration is incomplete.");
        }

        return document.Root ?? throw new OfxFormatException(0, "The file has no OFX statement root.");
    }

    private static (Dictionary<string, string>, int) ReadSgmlHeader(string text)
    {
        int start = text.IndexOf('<');

        if (start < 0 || start > 8192)
        {
            throw new OfxFormatException(0, "The OFX SGML header is incomplete.");
        }

        Dictionary<string, string> header = new(StringComparer.OrdinalIgnoreCase);

        foreach (string line in text[..start].Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int colon = line.IndexOf(':');

            if (colon < 1 || !header.TryAdd(line[..colon].Trim(), line[(colon + 1)..].Trim().ToUpperInvariant()))
            {
                throw new OfxFormatException(0, "The OFX SGML header contains an invalid or duplicate field.");
            }
        }

        if (Get(header, "OFXHEADER") != "100" ||
            Get(header, "DATA") != "OFXSGML" ||
            Get(header, "VERSION") is not ("100" or "101" or "102" or "103" or "151" or "160"))
        {
            throw new OfxFormatException(0, "The file declares an unsupported OFX SGML version.");
        }

        if (Get(header, "SECURITY") != "NONE" || Get(header, "COMPRESSION") != "NONE")
        {
            throw new OfxFormatException(0, "Encrypted or compressed OFX files are not supported.");
        }

        return (header, start);
    }

    private static Encoding SgmlEncoding(IReadOnlyDictionary<string, string> header)
    {
        string encoding = Get(header, "ENCODING");
        string charset = Get(header, "CHARSET");

        int codePage = (encoding, charset) switch
        {
            ("UTF-8" or "UTF8", "NONE" or "65001") => 65001,
            ("USASCII", "1252") => 1252,
            ("USASCII", "8859-1" or "ISO-8859-1" or "28591") => 28591,
            ("USASCII", "NONE" or "USASCII" or "ASCII" or "20127") => 20127,
            _ => throw new OfxFormatException(0, "The OFX SGML encoding is unsupported. Use UTF-8, ASCII, Windows-1252, or ISO-8859-1.")
        };

        return Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    private static XElement ReadSgml(string text, int start)
    {
        Stack<XElement> stack = new();
        XElement? root = null;
        string? leaf = null;
        int line = 1 + text.AsSpan(0, start).Count('\n');
        int position = start;
        int elements = 0;

        while (position < text.Length)
        {
            int next = text.IndexOf('<', position);
            if (next < 0)
            {
                next = text.Length;
            }

            if (!string.IsNullOrWhiteSpace(text[position..next]))
            {
                throw new OfxFormatException(line, "Unexpected text outside an OFX field.");
            }

            line += text.AsSpan(position, next - position).Count('\n');
            position = next;

            if (position == text.Length)
            {
                break;
            }

            if (text.AsSpan(position).StartsWith("<!--", StringComparison.Ordinal))
            {
                int commentEnd = text.IndexOf("-->", position + 4, StringComparison.Ordinal);

                if (commentEnd < 0)
                {
                    throw new OfxFormatException(line, "An OFX comment is not closed.");
                }

                line += text.AsSpan(position, commentEnd + 3 - position).Count('\n');
                position = commentEnd + 3;
                continue;
            }

            int close = text.IndexOf('>', position + 1);

            if (close < 0)
            {
                throw new OfxFormatException(line, "An OFX tag is not closed.");
            }

            string tag = text[(position + 1)..close].Trim();
            bool ending = tag.StartsWith('/');
            bool empty = tag.EndsWith('/');
            string name = (ending ? tag[1..] : empty ? tag[..^1] : tag).Trim().ToUpperInvariant();

            if (!_tagName.IsMatch(name))
            {
                throw new OfxFormatException(line, "Unsupported OFX markup. DTDs, entities, and attributes are not permitted in SGML files.");
            }

            if (ending)
            {
                if (leaf == name)
                {
                    leaf = null;
                }
                else
                {
                    leaf = null;

                    if (stack.Count == 0 || stack.Peek().Name.LocalName != name)
                    {
                        throw new OfxFormatException(line, "The OFX aggregate closing tags do not match.");
                    }

                    stack.Pop();
                }

                line += text.AsSpan(position, close + 1 - position).Count('\n');
                position = close + 1;
                continue;
            }

            leaf = null;

            if (++elements > _maximumElements || stack.Count >= _maximumDepth)
            {
                throw new OfxFormatException(line, "The OFX document is too deeply nested or contains too many elements.");
            }

            XElement element = new(name);
            element.AddAnnotation(new OfxSourceLine(line));

            if (stack.Count > 0)
            {
                stack.Peek().Add(element);
            }
            else if (root is null)
            {
                root = element;
            }
            else
            {
                throw new OfxFormatException(line, "The file contains multiple OFX documents.");
            }

            int valueEnd = text.IndexOf('<', close + 1);

            if (valueEnd < 0)
            {
                valueEnd = text.Length;
            }

            string value = text[(close + 1)..valueEnd];

            if (empty)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    throw new OfxFormatException(line, "Unexpected content after an empty OFX field.");
                }
            }
            else if (_scalarTags.Contains(name) || !string.IsNullOrWhiteSpace(value))
            {
                if (AggregateTags.Contains(name))
                {
                    throw new OfxFormatException(line, "An OFX aggregate contains unexpected text.");
                }

                element.Value = Decode(value.Trim(), line);
                leaf = name;
            }
            else
            {
                stack.Push(element);
            }

            line += text.AsSpan(position, valueEnd - position).Count('\n');
            position = valueEnd;
        }

        if (stack.Count != 0 || root is null)
        {
            throw new OfxFormatException(line, "The OFX document is incomplete; every aggregate must have its closing tag.");
        }

        return root;
    }

    private static string Decode(string value, int line)
    {
        string decoded = _entity.Replace(value, match => match.Groups["name"].Value switch
        {
            "amp" => "&",
            "lt" => "<",
            "gt" => ">",
            "quot" => "\"",
            "apos" => "'",
            "nbsp" => " ",
            string name when name.StartsWith('#') => DecodeNumericEntity(name, line),
            _ => throw new OfxFormatException(line, "An OFX field contains an unsupported entity reference.")
        });

        return XmlConvert.VerifyXmlChars(decoded);
    }

    private static string DecodeNumericEntity(string name, int line)
    {
        bool hex = name.StartsWith("#x", StringComparison.OrdinalIgnoreCase);

        if (!int.TryParse(name[(hex ? 2 : 1)..], hex
            ? NumberStyles.AllowHexSpecifier
            : NumberStyles.None, CultureInfo.InvariantCulture, out int value) || !Rune.IsValid(value))
        {
            throw new OfxFormatException(line, "An OFX field contains an invalid numeric entity.");
        }

        return char.ConvertFromUtf32(value);
    }

    private static string Get(IReadOnlyDictionary<string, string> header, string key)
    {
        return header.TryGetValue(key, out string? value) ? value : throw new OfxFormatException(0, "Missing OFX header field " + key + ".");
    }
}
