namespace Sales.Domain;

public sealed class SalesAnalyzer
{
    public SalesAnalysis Analyze(IEnumerable<SalesRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        var products = new Dictionary<string, ProductTotals>(StringComparer.Ordinal);
        var states = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var productOrder = new List<string>();
        var stateOrder = new List<string>();
        long count = 0;
        double mean = 0;
        double sumSquaredDifferences = 0;

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!products.TryGetValue(record.ProductName, out var product))
            {
                product = new ProductTotals();
                products.Add(record.ProductName, product);
                productOrder.Add(record.ProductName);
            }

            product.Profit += record.Profit;
            product.Discount += record.Discount;
            product.Count++;
            if (record.Returned)
                product.ReturnedOrders++;

            if (!states.ContainsKey(record.State))
            {
                states.Add(record.State, 0);
                stateOrder.Add(record.State);
            }
            states[record.State] += record.Sales;

            // Welford's algorithm avoids subtracting two large, nearly equal sums.
            count++;
            var sales = (double)record.Sales;
            var difference = sales - mean;
            mean += difference / count;
            sumSquaredDifferences += difference * (sales - mean);
        }

        if (count == 0)
            throw new ArgumentException("The file must contain at least one sales record.", nameof(records));

        var leastProfitable = productOrder[0];
        var highestDiscount = productOrder[0];
        var mostReturns = productOrder[0];
        foreach (var name in productOrder)
        {
            var product = products[name];
            // Strict comparisons preserve the first occurrence on ties.
            if (product.Profit < products[leastProfitable].Profit)
                leastProfitable = name;
            if (product.AverageDiscount > products[highestDiscount].AverageDiscount)
                highestDiscount = name;
            if (product.ReturnedOrders > products[mostReturns].ReturnedOrders)
                mostReturns = name;
        }

        var lowestSalesState = stateOrder[0];
        foreach (var state in stateOrder)
            if (states[state] < states[lowestSalesState])
                lowestSalesState = state;

        return new SalesAnalysis(
            leastProfitable, highestDiscount, products[highestDiscount].AverageDiscount,
            mostReturns, lowestSalesState, Math.Sqrt(Math.Max(0, sumSquaredDifferences / count)));
    }

    private sealed class ProductTotals
    {
        public decimal Profit { get; set; }
        public decimal Discount { get; set; }
        public long Count { get; set; }
        public decimal AverageDiscount => Discount / Count;
        public long ReturnedOrders { get; set; }
    }
}
