namespace Sales.Application;

public sealed class InvalidSalesFileException(string message) : Exception(message);
