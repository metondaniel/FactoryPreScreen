# Impinj Coding Assessment

C# / ASP.NET Core Web API that parses the supplied sales CSV and returns the six requested metrics.

## Run locally and use Swagger

Prerequisite: .NET 10 SDK. From this directory, restore and run the tests:

```sh
dotnet restore ImpinjAssessment.sln
dotnet test ImpinjAssessment.sln
```

Start the API:

```powershell
dotnet run --project src/Sales.Api
```

Open **http://localhost:58556/swagger**, expand `POST /api/sales/analysis` and select
**Try it out**. Choose `Input/SalesOrders.csv` in the `file` field and click **Execute**.
The endpoint accepts requests without an API key.

For a command-line upload:

```powershell
curl.exe -X POST http://localhost:58556/api/sales/analysis -F "file=@Input/SalesOrders.csv"
```

## Contract

`POST /api/sales/analysis` accepts `multipart/form-data` with one field named `file`.
The successful response contains exactly these six fields:

| JSON field | Calculation |
| --- | --- |
| leastProfitableProduct | Product with the lowest signed sum of Profit |
| productWithHighestAverageDiscount | Product with the largest arithmetic mean of Discount |
| highestAverageDiscount | That mean, in the same units as the CSV (0.8 means 80%) |
| productWithMostReturnedOrders | Product with the most rows marked Yes |
| stateWithLowestTotalSales | State with the lowest sum of Sales |
| salesStandardDeviation | Population standard deviation of Sales over all rows |

Response for the included 3,024-row sample:

```json
{
  "leastProfitableProduct": "GBC DocuBind P400 Electric Binding System",
  "productWithHighestAverageDiscount": "Avery Hidden Tab Dividers for Binding Systems",
  "highestAverageDiscount": 0.8,
  "productWithMostReturnedOrders": "Advantus Push Pins",
  "stateWithLowestTotalSales": "Kansas",
  "salesStandardDeviation": 726.2126422410771
}
```

The last few digits of the standard deviation may vary with floating-point arithmetic.
Missing uploads, empty files, missing required columns, malformed rows and invalid required values
return HTTP 400 with Problem Details. A non-multipart request returns HTTP 415.
Oversized uploads return HTTP 413; exhausted rate/concurrency limits return HTTP 429.
Malformed multipart requests can be rejected by the framework with HTTP 400.
A file is limited to 5 MiB; the whole request allows an additional 64 KiB for multipart headers.

## Interpretations of the requirements

- Profit means signed total profit, not profit margin, average profit, or the absolute value of profit.
- All rows, including returns, contribute to profit, average discount, state sales and standard deviation.
- Discounts are averaged per record; neither Sales nor Quantity weights the average.
- Sales already represents the total per record and is never multiplied by Quantity.
- Ties retain the product's first occurrence in the input, including for returns. State ties follow the same rule.
- With no returns, every product has zero returned orders, so the first product wins the tie.
- The sample has no order identifier. Consequently, each returned row counts once; repeated rows remain distinct.
- Products are grouped by the exact trimmed Product Name. The sample has Product IDs associated with different
  names, so grouping solely by ID would merge distinct named products. Conversely, repeated names are one product.
  Name and state comparisons are ordinal and case-sensitive.
- The assessment does not specify sample versus population standard deviation. This solution uses the complete
  uploaded dataset as the population: sqrt(sum((Sales - mean)^2) / N). One row yields zero.
- Empty datasets have no meaningful winning product or state and are rejected.

## CSV handling

The required named columns are Product Name, State, Sales, Profit, Discount and Returned.
Their order does not matter; other columns are ignored.
The sample's two trailing unnamed columns are accepted.
Named duplicate headers and inconsistent row widths are rejected.
Header matching is case-insensitive; surrounding field whitespace is trimmed.
Returned accepts Yes and Not, case-insensitively, matching all four spellings in the sample.
Numeric fields use a dot decimal separator independently of machine culture.
Quoted commas, escaped quotes and multiline fields are handled by the .NET TextFieldParser.
Blank physical lines are ignored by that parser. Invalid records are never silently skipped.
The included CSV retains all 3,024 records; its text was saved as UTF-8 with LF line endings.

## Design

| Project | Responsibility |
| --- | --- |
| Sales.Domain | Immutable sales records, analysis result and aggregation rules |
| Sales.Application | AnalyzeSalesFile use case and the ISalesRecordReader port |
| Sales.Infrastructure | CSV adapter implementing the reader port |
| Sales.Api | HTTP contract, dependency injection and error responses |
| Sales.Tests | Domain, parser and in-process HTTP integration tests |

Dependencies point inward: Application depends on Domain, Infrastructure implements the Application port,
and the API composes them. Domain code has no HTTP, file-system or third-party dependencies.
The reader is replaceable through a narrow interface; concrete domain calculations do not need a redundant interface.
This applies DDD through explicit domain language and a domain service. There is no persistence or aggregate lifecycle
in the problem, so repositories, aggregate roots, databases, CQRS infrastructure and messaging are unnecessary.

Records are streamed into the calculation without materializing the full file.
Aggregation is O(N) time and O(P + S) memory for P products and S states.
HTTP multipart buffering is separate and bounded by the configured upload limit.
Explicit encounter-order lists and strict comparisons make tie-breaking independent of dictionary ordering.
Decimal arithmetic is used for financial totals and discounts.
Welford's online algorithm calculates variance in double precision, avoiding cancellation from subtracting large sums.
Each request owns its aggregation state. Cancellation is checked before opening the parser and for each record.

## Verification

Tests cover the supplied CSV, its 3,024 records and 260 returns, all six results, signed profit sums,
unweighted average discounts, returned records in every applicable metric, encounter-order ties,
repeated rows, zero returns, empty input, population deviation, single records and numerical stability.
Parser tests cover the actual return values, CSV quoting, reordered columns, BOM, caller stream ownership,
machine culture and invalid input. HTTP integration tests exercise multipart upload and the response contract.

Expected sample results were also calculated independently using Python's csv, Decimal and statistics.pstdev.

## Security and documentation

- **Transport:** outside Development, HTTP is rejected before reading the upload; HTTPS responses use HSTS.
  Bind production to HTTPS only. Configure trusted reverse proxies explicitly if terminating TLS upstream;
  arbitrary X-Forwarded-Proto headers are not trusted by this application.
- **Resource limits:** 30 requests per one-minute fixed window across each API process; at most two simultaneous analysis requests; no queue. These are per-instance
  limits, not a distributed limiter or a DDoS protection service. A gateway must enforce shared limits if scaled out.
- **Uploads:** one non-empty .csv file, maximum 5 MiB. Kestrel and IIS hosting body limits, a Content-Length
  check and multipart limits bound requests. Multipart buffers up to the request limit stay in memory.
  Filenames are never used as disk paths and files are not saved. File extension is only a preliminary
  check; the CSV parser validates structure and required values regardless of the claimed MIME type.
- **Responses:** no-store, nosniff, frame denial, a restrictive API Content-Security-Policy and no-referrer.
  Unexpected errors return generic Problem Details instead of stack traces. No permissive CORS policy is enabled.
- **Swagger:** UI and the OpenAPI document are enabled only in Development. The upload uses inferred
  IFormFile binding, which lets Swagger generate the binary multipart field correctly.

Security tests cover Swagger schema/UI, production behavior, untrusted
forwarded headers, upload sizes, duplicate uploads, invalid extensions, rate/concurrency limits and error redaction.

Implementation references:
- https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-10.0
- https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0
- https://github.com/domaindrivendev/Swashbuckle.AspNetCore
