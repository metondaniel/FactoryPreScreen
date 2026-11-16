using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using Sales.Application;
using Sales.Domain;

namespace Sales.Infrastructure;

public sealed class CsvSalesRecordReader : ISalesRecordReader
{
    private static readonly string[] RequiredHeaders =
        ["Product Name", "State", "Sales", "Profit", "Discount", "Returned"];

    public IEnumerable<SalesRecord> Read(Stream stream)
    {
        using var records = ReadCore(stream).GetEnumerator();
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = records.MoveNext();
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidSalesFileException("The CSV must use valid text encoding.");
            }

            if (!hasNext)
                yield break;
            yield return records.Current;
        }
    }

    private static IEnumerable<SalesRecord> ReadCore(Stream stream)
    {
        using var parser = new TextFieldParser(stream, new UTF8Encoding(false, true), true, true)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");
        var headers = ReadFields(parser);
        if (headers is null)
            throw new InvalidSalesFileException("The CSV file is empty.");

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Length; index++)
        {
            var header = headers[index].Trim();
            // The supplied file ends with two unnamed, unused columns.
            if (header.Length == 0)
                continue;
            if (!columns.TryAdd(header, index))
                throw new InvalidSalesFileException("The CSV contains duplicate column names.");
        }

        foreach (var header in RequiredHeaders)
            if (!columns.ContainsKey(header))
                throw new InvalidSalesFileException($"Missing required column: {header}.");

        var hasRecords = false;
        while (!parser.EndOfData)
        {
            var line = parser.LineNumber;
            var fields = ReadFields(parser);
            if (fields is null)
                break;
            if (fields.Length != headers.Length)
                throw new InvalidSalesFileException($"Line {line}: column count does not match the header.");

            string Field(string header)
            {
                var value = fields[columns[header]].Trim();
                if (value.Length == 0)
                    throw new InvalidSalesFileException($"Line {line}: {header} is required.");
                return value;
            }

            decimal Number(string header)
            {
                if (!decimal.TryParse(Field(header), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var value))
                    throw new InvalidSalesFileException($"Line {line}: {header} must be a decimal number using a dot.");
                return value;
            }

            var returned = Field("Returned");
            var isReturned = returned.Equals("Yes", StringComparison.OrdinalIgnoreCase);
            if (!isReturned && !returned.Equals("Not", StringComparison.OrdinalIgnoreCase))
                throw new InvalidSalesFileException($"Line {line}: Returned must be Yes or Not.");

            hasRecords = true;
            yield return new SalesRecord(
                Field("Product Name"), Field("State"),
                Number("Sales"), Number("Profit"), Number("Discount"), isReturned);
        }

        if (!hasRecords)
            throw new InvalidSalesFileException("The CSV must contain at least one sales record.");
    }

    private static string[]? ReadFields(TextFieldParser parser)
    {
        try
        {
            return parser.ReadFields();
        }
        catch (MalformedLineException)
        {
            throw new InvalidSalesFileException($"Line {parser.ErrorLineNumber}: malformed CSV.");
        }
    }
}
