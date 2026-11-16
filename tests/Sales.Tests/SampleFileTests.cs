using Sales.Application;
using Sales.Domain;
using Sales.Infrastructure;

namespace Sales.Tests;

public sealed class SampleFileTests
{
    [Fact]
    public void Reads_all_3024_records_and_260_returns()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Input", "SalesOrders.csv"));
        var records = new CsvSalesRecordReader().Read(stream).ToArray();
        Assert.Equal(3024, records.Length);
        Assert.Equal(260, records.Count(record => record.Returned));
    }

    [Fact]
    public void Use_case_keeps_first_product_on_ties_and_does_not_multiply_by_quantity()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic.csv"));
        var result = new AnalyzeSalesFile(new CsvSalesRecordReader(), new SalesAnalyzer()).Execute(stream);
        Assert.Equal(.4m, result.HighestAverageDiscount);
        Assert.Equal(Math.Sqrt(12500), result.SalesStandardDeviation, 10);
    }
}
