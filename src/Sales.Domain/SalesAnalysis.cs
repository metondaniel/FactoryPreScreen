namespace Sales.Domain;

public sealed record SalesAnalysis(
    string LeastProfitableProduct,
    string ProductWithHighestAverageDiscount,
    decimal HighestAverageDiscount,
    string ProductWithMostReturnedOrders,
    string StateWithLowestTotalSales,
    double SalesStandardDeviation);
