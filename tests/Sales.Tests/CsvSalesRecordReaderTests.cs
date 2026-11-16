using System.Globalization;
using System.Text;
using Sales.Application;
using Sales.Infrastructure;

namespace Sales.Tests;

public sealed class CsvSalesRecordReaderTests
{
    private const string Header = "Product Name,State,Sales,Profit,Discount,Returned";

    [Theory]
    [InlineData("Yes", true)]
    [InlineData("yes", true)]
    [InlineData("Not", false)]
    [InlineData("not", false)]
    public void Reads_return_values_from_supplied_file(string returned, bool expected)
    {
        using var stream = Csv(Header + "\nA,CA,12.5,-4.2,0.3," + returned);
        var row = Assert.Single(new CsvSalesRecordReader().Read(stream));
        Assert.Equal(expected, row.Returned);
        Assert.Equal(12.5m, row.Sales);
        Assert.Equal(-4.2m, row.Profit);
    }

    [Fact]
    public void Supports_quoted_commas_escaped_quotes_multiline_fields_and_unnamed_columns()
    {
        using var stream = Csv(Header + ",,\r\n\"A, \"\"special\"\"\r\nproduct\",CA,10,-2,0.5,yes,,");
        var row = Assert.Single(new CsvSalesRecordReader().Read(stream));
        Assert.Equal("A, \"special\"\r\nproduct", row.ProductName);
    }

    [Fact]
    public void Decimal_parsing_does_not_depend_on_machine_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            using var stream = Csv(Header + "\nA,CA,12.50,-2.25,0.80,Not");
            var row = Assert.Single(new CsvSalesRecordReader().Read(stream));
            Assert.Equal(12.50m, row.Sales);
            Assert.Equal(.80m, row.Discount);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void Header_order_and_unused_fields_do_not_change_mapping()
    {
        using var stream = Csv("Returned,Discount,Profit,Sales,State,Product Name,Quantity\nyes,0.2,-5,40,NY,A,999");
        var row = Assert.Single(new CsvSalesRecordReader().Read(stream));
        Assert.Equal("A", row.ProductName);
        Assert.Equal(40, row.Sales);
        Assert.True(row.Returned);
    }

    [Fact]
    public void Supports_utf8_bom_and_preserves_caller_stream()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Header + "\nA,CA,1,0,0,Not")).ToArray();
        using var stream = new MemoryStream(bytes);
        Assert.Single(new CsvSalesRecordReader().Read(stream));
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Product Name,Sales")]
    [InlineData(Header)]
    [InlineData(Header + "\nA,CA,1,0,0,maybe")]
    [InlineData(Header + "\nA,CA,NaN,0,0,Not")]
    [InlineData(Header + "\nA,CA,1,0,0")]
    [InlineData(Header + "\n,CA,1,0,0,Not")]
    [InlineData(Header + "\nA,CA,1,0,0,Not,extra")]
    [InlineData(Header + "\n\"unterminated,CA,1,0,0,Not")]
    [InlineData(Header + ",Sales\nA,CA,1,0,0,Not,1")]
    public void Rejects_invalid_files_without_silently_skipping_records(string csv)
    {
        using var stream = Csv(csv);
        Assert.Throws<InvalidSalesFileException>(() => new CsvSalesRecordReader().Read(stream).ToArray());
    }

    private static MemoryStream Csv(string value) => new(Encoding.UTF8.GetBytes(value));
}
