namespace Sales.Domain;

public sealed record SalesRecord(
    string ProductName,
    string State,
    decimal Sales,
    decimal Profit,
    decimal Discount,
    bool Returned);
