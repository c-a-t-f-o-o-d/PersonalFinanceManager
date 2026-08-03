using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace PersonalFinanceManager.Core.Imports
{
    public sealed class MinimalPdfTextExtractor
    {
        private static readonly Regex _streamRegex = new(@"<<(.*?)>>\s*stream\r?\n(?<data>.*?)\r?\nendstream", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _stringRegex = new(@"\((?<text>(?:\\.|[^\\)])*)\)\s*Tj", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _rangeRegex = new(@"<(?<from>[0-9A-Fa-f]{4})>\s*<(?<to>[0-9A-Fa-f]{4})>\s*<(?<target>[0-9A-Fa-f]{4})>", RegexOptions.Compiled);

        public string ExtractText(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            byte[] bytes = File.ReadAllBytes(filePath);
            return ExtractText(bytes);
        }

        public string ExtractText(byte[] pdfBytes)
        {
            ArgumentNullException.ThrowIfNull(pdfBytes);
            string pdfText = Encoding.Latin1.GetString(pdfBytes);
            List<byte[]> decodedStreams = new();
            Dictionary<int, char> toUnicode = new();

            foreach (Match match in _streamRegex.Matches(pdfText))
            {
                string dictionary = match.Groups[1].Value;

                if (!dictionary.Contains("FlateDecode", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                byte[] compressed = Encoding.Latin1.GetBytes(match.Groups["data"].Value);
                byte[]? decoded = TryDecompress(compressed);

                if (decoded is null || decoded.Length == 0)
                {
                    continue;
                }

                decodedStreams.Add(decoded);
                string decodedText = Encoding.Latin1.GetString(decoded);

                if (decodedText.Contains("begincmap", StringComparison.OrdinalIgnoreCase))
                {
                    foreach ((int source, char target) in ParseToUnicodeMap(decodedText))
                    {
                        toUnicode[source] = target;
                    }
                }
            }

            StringBuilder builder = new();

            foreach (byte[] stream in decodedStreams)
            {
                string content = Encoding.Latin1.GetString(stream);

                if (!content.Contains("Tj", StringComparison.Ordinal) || content.Contains("begincmap", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (Match textMatch in _stringRegex.Matches(content))
                {
                    byte[] textBytes = DecodePdfLiteralString(textMatch.Groups["text"].Value);
                    string value = DecodeTextBytes(textBytes, toUnicode);

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        builder.AppendLine(value.Trim());
                    }
                }
            }

            return builder.ToString();
        }

        private static byte[]? TryDecompress(byte[] compressed)
        {
            try
            {
                using MemoryStream source = new(compressed);
                using ZLibStream zlib = new(source, CompressionMode.Decompress);
                using MemoryStream target = new();
                zlib.CopyTo(target);
                return target.ToArray();
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        private static IEnumerable<(int Source, char Target)> ParseToUnicodeMap(string text)
        {
            foreach (Match match in _rangeRegex.Matches(text))
            {
                int from = int.Parse(match.Groups["from"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int to = int.Parse(match.Groups["to"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int target = int.Parse(match.Groups["target"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                for (int code = from; code <= to; code++)
                {
                    yield return (code, (char)(target + code - from));
                }
            }
        }

        private static byte[] DecodePdfLiteralString(string text)
        {
            List<byte> bytes = new();

            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];

                if (current != '\\')
                {
                    bytes.Add((byte)current);
                    continue;
                }

                if (index + 1 >= text.Length)
                {
                    break;
                }

                char escaped = text[++index];

                switch (escaped)
                {
                    case 'n':
                        bytes.Add((byte)'\n');
                        break;
                    case 'r':
                        bytes.Add((byte)'\r');
                        break;
                    case 't':
                        bytes.Add((byte)'\t');
                        break;
                    case 'b':
                        bytes.Add((byte)'\b');
                        break;
                    case 'f':
                        bytes.Add((byte)'\f');
                        break;
                    case '(':
                    case ')':
                    case '\\':
                        bytes.Add((byte)escaped);
                        break;
                    default:
                        if (escaped is >= '0' and <= '7')
                        {
                            string octal = escaped.ToString();

                            for (int count = 0; count < 2 && index + 1 < text.Length && text[index + 1] is >= '0' and <= '7'; count++)
                            {
                                octal += text[++index];
                            }

                            bytes.Add(Convert.ToByte(octal, 8));
                        }
                        else
                        {
                            bytes.Add((byte)escaped);
                        }

                        break;
                }
            }

            return bytes.ToArray();
        }

        private static string DecodeTextBytes(byte[] bytes, IReadOnlyDictionary<int, char> toUnicode)
        {
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new();

            if (bytes.Length >= 2 && bytes[0] == 0)
            {
                for (int index = 0; index + 1 < bytes.Length; index += 2)
                {
                    int code = bytes[index] << 8 | bytes[index + 1];

                    if (toUnicode.TryGetValue(code, out char mapped))
                    {
                        builder.Append(mapped);
                    }
                    else if (code is >= 32 and <= 126)
                    {
                        builder.Append((char)code);
                    }
                }

                return builder.ToString();
            }

            return Encoding.Latin1.GetString(bytes);
        }
    }
}
