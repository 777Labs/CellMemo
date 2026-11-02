using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CellMemo.Models;

namespace CellMemo.Services
{
    public class ParseResult
    {
        public Dictionary<CellAddress, CellData> Cells { get; } = new();
        public Encoding Encoding { get; set; } = new UTF8Encoding(false);
        public string LineEnding { get; set; } = "CRLF";
        public bool HasBom { get; set; }
        public int MaxRow { get; set; }
        public int MaxCol { get; set; }
    }

    public static class CsvTsvSerializer
    {
        static CsvTsvSerializer()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static async Task<ParseResult> ReadFileAsync(string filePath)
        {
            char delimiter = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".tsv" or ".txt" => '\t',
                _ => ','
            };

            var (encoding, hasBom, lineEnding) = await DetectEncodingAndLineEndingAsync(filePath);

            const int bufferSize = 65536;
            using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var reader = new StreamReader(fileStream, encoding, detectEncodingFromByteOrderMarks: true, bufferSize);
            string content = await reader.ReadToEndAsync();

            var result = ParseDelimitedContent(content, delimiter);
            result.Encoding = encoding;
            result.HasBom = hasBom;
            result.LineEnding = lineEnding;

            return result;
        }

        public static async Task<(Encoding encoding, bool hasBom, string lineEnding)> DetectEncodingAndLineEndingAsync(string filePath)
        {
            byte[] buffer = new byte[65536];
            int bytesRead;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);
            }

            // 1. BOM判定
            if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                return (new UTF8Encoding(true), true, DetectLineEnding(buffer, bytesRead, Encoding.UTF8));
            }
            if (bytesRead >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
            {
                return (Encoding.Unicode, true, DetectLineEnding(buffer, bytesRead, Encoding.Unicode));
            }
            if (bytesRead >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
            {
                return (Encoding.BigEndianUnicode, true, DetectLineEnding(buffer, bytesRead, Encoding.BigEndianUnicode));
            }

            // 2. BOMなし UTF-8 vs Shift_JIS判定
            bool isUtf8 = IsValidUtf8(buffer, bytesRead);
            Encoding enc = isUtf8 ? new UTF8Encoding(false) : Encoding.GetEncoding(932);

            string lineEnding = DetectLineEnding(buffer, bytesRead, enc);
            return (enc, false, lineEnding);
        }

        private static bool IsValidUtf8(byte[] bytes, int length)
        {
            int i = 0;
            while (i < length)
            {
                byte b = bytes[i];
                if ((b & 0x80) == 0) // ASCII
                {
                    i++;
                }
                else if ((b & 0xE0) == 0xC0) // 2-byte
                {
                    if (i + 1 >= length || (bytes[i + 1] & 0xC0) != 0x80)
                        return false;
                    i += 2;
                }
                else if ((b & 0xF0) == 0xE0) // 3-byte
                {
                    if (i + 2 >= length || (bytes[i + 1] & 0xC0) != 0x80 || (bytes[i + 2] & 0xC0) != 0x80)
                        return false;
                    i += 3;
                }
                else if ((b & 0xF8) == 0xF0) // 4-byte
                {
                    if (i + 3 >= length || (bytes[i + 1] & 0xC0) != 0x80 || (bytes[i + 2] & 0xC0) != 0x80 || (bytes[i + 3] & 0xC0) != 0x80)
                        return false;
                    i += 4;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private static string DetectLineEnding(byte[] buffer, int length, Encoding enc)
        {
            string sample = enc.GetString(buffer, 0, Math.Min(length, 4096));
            if (sample.Contains("\r\n")) return "CRLF";
            if (sample.Contains("\n")) return "LF";
            if (sample.Contains("\r")) return "CR";
            return "CRLF";
        }

        public static ParseResult ParseDelimitedContent(string content, char delimiter)
        {
            var result = new ParseResult();
            if (string.IsNullOrEmpty(content))
                return result;

            int row = 1;
            int col = 1;
            int maxRow = 1;
            int maxCol = 1;

            var currentField = new StringBuilder();
            bool insideQuote = false;
            int len = content.Length;

            for (int i = 0; i < len; i++)
            {
                char c = content[i];

                if (insideQuote)
                {
                    if (c == '"')
                    {
                        if (i + 1 < len && content[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++; // escape
                        }
                        else
                        {
                            insideQuote = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        insideQuote = true;
                    }
                    else if (c == delimiter)
                    {
                        AddField(result, row, col, currentField.ToString());
                        currentField.Clear();
                        col++;
                        if (col > maxCol) maxCol = col;
                    }
                    else if (c == '\r')
                    {
                        if (i + 1 < len && content[i + 1] == '\n')
                        {
                            i++;
                        }
                        AddField(result, row, col, currentField.ToString());
                        currentField.Clear();
                        if (row > maxRow) maxRow = row;
                        row++;
                        col = 1;
                    }
                    else if (c == '\n')
                    {
                        AddField(result, row, col, currentField.ToString());
                        currentField.Clear();
                        if (row > maxRow) maxRow = row;
                        row++;
                        col = 1;
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            if (currentField.Length > 0 || col > 1)
            {
                AddField(result, row, col, currentField.ToString());
                if (row > maxRow) maxRow = row;
                if (col > maxCol) maxCol = col;
            }

            result.MaxRow = maxRow;
            result.MaxCol = maxCol;
            return result;
        }

        private static void AddField(ParseResult result, int row, int col, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                var addr = new CellAddress(row, col);
                result.Cells[addr] = new CellData(value);
            }
        }

        public static async Task SaveFileAsync(
            SheetDocument doc,
            string filePath,
            bool saveFormulasAsRawText)
        {
            char delimiter = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".tsv" or ".txt" => '\t',
                _ => ','
            };

            string lineEndingSeq = doc.LineEnding switch
            {
                "LF" => "\n",
                "CR" => "\r",
                _ => "\r\n"
            };

            var (maxRow, maxCol) = doc.GetDimensions();
            maxRow = Math.Max(1, maxRow);
            maxCol = Math.Max(1, maxCol);

            var sb = new StringBuilder();

            for (int r = 1; r <= maxRow; r++)
            {
                for (int c = 1; c <= maxCol; c++)
                {
                    var addr = new CellAddress(r, c);
                    var cell = doc.GetCell(addr);

                    string val = string.Empty;
                    if (cell != null)
                    {
                        val = saveFormulasAsRawText ? cell.RawText : cell.EvaluatedText;
                    }

                    // RFC 4180 エスケープ
                    sb.Append(EscapeField(val, delimiter));

                    if (c < maxCol)
                    {
                        sb.Append(delimiter);
                    }
                }
                sb.Append(lineEndingSeq);
            }

            // アトミック保存 (.tmp に書き出し Flush 後、File.Move(..., overwrite: true))
            string dir = Path.GetDirectoryName(filePath) ?? ".";
            Directory.CreateDirectory(dir);
            string tempFile = Path.Combine(dir, $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

            using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            using (var writer = new StreamWriter(stream, doc.Encoding))
            {
                await writer.WriteAsync(sb.ToString());
                await writer.FlushAsync();
                await stream.FlushAsync();
            }

            File.Move(tempFile, filePath, overwrite: true);
        }

        public static string EscapeField(string field, char delimiter)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            bool mustQuote = field.Contains(delimiter) ||
                             field.Contains('"') ||
                             field.Contains('\n') ||
                             field.Contains('\r');

            if (!mustQuote)
                return field;

            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
    }
}
