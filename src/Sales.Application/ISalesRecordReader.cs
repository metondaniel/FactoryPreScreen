using Sales.Domain;

namespace Sales.Application;

public interface ISalesRecordReader
{
    IEnumerable<SalesRecord> Read(Stream stream);
}
