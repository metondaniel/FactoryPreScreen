using Sales.Domain;

namespace Sales.Tests;

public sealed class SalesAnalyzerTests
{
    private readonly SalesAnalyzer analyzer = new();

    [Fact]
    public void Computes_all_metrics_including_returned_records()
    {
        SalesRecord[] records =
        [
            new("Alpha", "CA", 100, -10, .1m, true),
            new("Alpha", "CA", 200, -20, .3m, false),
            new("Beta", "NY", 300, -5, .5m, true),
            new("Beta", "NY", 400, -5, .3m, true)
        ];
        var result = analyzer.Analyze(records);
        Assert.Equal("Alpha", result.LeastProfitableProduct);
        Assert.Equal("Beta", result.ProductWithHighestAverageDiscount);
        Assert.Equal(.4m, result.HighestAverageDiscount);
        Assert.Equal("Beta", result.ProductWithMostReturnedOrders);
        Assert.Equal("CA", result.StateWithLowestTotalSales);
        Assert.Equal(Math.Sqrt(12500), result.SalesStandardDeviation, 10);
    }

    [Fact]
    public void Profit_is_signed_total_not_absolute_magnitude_or_average()
    {
        var result = analyzer.Analyze([
            new("A", "CA", 1, -6, 0, false),
            new("B", "CA", 1, -10, 0, false),
            new("A", "CA", 1, -6, 0, false),
            new("C", "CA", 1, 0, 0, false)]);
        Assert.Equal("A", result.LeastProfitableProduct);
    }

    [Fact]
    public void Average_discount_is_per_record_not_total_or_weighted_by_sales()
    {
        var result = analyzer.Analyze([
            new("A", "CA", 10000, .1m, .9m, false),
            new("A", "CA", 1, .1m, .1m, false),
            new("B", "CA", 1, .1m, .6m, false)]);
        Assert.Equal("B", result.ProductWithHighestAverageDiscount);
        Assert.Equal(.6m, result.HighestAverageDiscount);
    }

    [Fact]
    public void Ties_use_first_occurrence_even_when_another_product_reaches_the_final_total_first()
    {
        var result = analyzer.Analyze([
            new("Z", "Z", 10, 2, .2m, false),
            new("A", "A", 20, 4, .2m, true),
            new("Z", "Z", 10, 2, .2m, true)]);
        Assert.Equal("Z", result.LeastProfitableProduct);
        Assert.Equal("Z", result.ProductWithHighestAverageDiscount);
        Assert.Equal("Z", result.ProductWithMostReturnedOrders);
        Assert.Equal("Z", result.StateWithLowestTotalSales);
    }

    [Fact]
    public void No_returns_uses_first_product_in_zero_count_tie()
    {
        var result = analyzer.Analyze([
            new("First", "CA", 1, 1, 0, false),
            new("Second", "CA", 1, 1, 0, false)]);
        Assert.Equal("First", result.ProductWithMostReturnedOrders);
    }

    [Fact]
    public void Repeated_returned_rows_count_separately()
    {
        var record = new SalesRecord("A", "CA", 1, 1, 0, true);
        var result = analyzer.Analyze([new("B", "CA", 1, 1, 0, true), record, record]);
        Assert.Equal("A", result.ProductWithMostReturnedOrders);
    }

    [Fact]
    public void Returned_sales_contribute_to_state_totals_and_standard_deviation()
    {
        var result = analyzer.Analyze([
            new("A", "CA", 10, 10, .1m, false),
            new("B", "NY", 20, -100, .9m, true),
            new("A", "CA", 100, 10, .1m, true)]);
        Assert.Equal("NY", result.StateWithLowestTotalSales);
        Assert.Equal("B", result.LeastProfitableProduct);
        Assert.Equal("B", result.ProductWithHighestAverageDiscount);
        Assert.Equal(Math.Sqrt(14600d / 9), result.SalesStandardDeviation, 10);
    }

    [Fact]
    public void Single_record_has_zero_standard_deviation()
    {
        Assert.Equal(0, analyzer.Analyze([new("A", "CA", 12.5m, 0, 0, false)]).SalesStandardDeviation);
    }

    [Fact]
    public void Population_deviation_divides_by_n_instead_of_n_minus_one()
    {
        var result = analyzer.Analyze([
            new("A", "CA", 2, 0, 0, false),
            new("A", "CA", 4, 0, 0, false)]);
        Assert.Equal(1, result.SalesStandardDeviation);
    }

    [Fact]
    public void Large_nearby_sales_remain_numerically_stable()
    {
        var result = analyzer.Analyze([
            new("A", "CA", 1000000000000m, 0, 0, false),
            new("A", "CA", 1000000000002m, 0, 0, false)]);
        Assert.Equal(1, result.SalesStandardDeviation);
    }

    [Fact]
    public void Empty_records_are_rejected() =>
        Assert.Throws<ArgumentException>(() => analyzer.Analyze([]));

    [Fact]
    public void Cancellation_stops_processing()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            analyzer.Analyze([new("A", "CA", 1, 1, 0, false)], cancellation.Token));
    }
}
