using Sales.Domain;

namespace Sales.Application;

public sealed class AnalyzeSalesFile(ISalesRecordReader reader, SalesAnalyzer analyzer)
{
    public SalesAnalysis Execute(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        return analyzer.Analyze(reader.Read(stream), cancellationToken);
    }
}
