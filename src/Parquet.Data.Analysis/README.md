# Parquet.Net.Data.Analysis

Adds [Microsoft.Data.Analysis](https://www.nuget.org/packages/Microsoft.Data.Analysis) `DataFrame` support to [Parquet.Net](https://www.nuget.org/packages/Parquet.Net): read a Parquet file into a `DataFrame`, and write a `DataFrame` to a Parquet file.

```csharp
using Microsoft.Data.Analysis;
using Parquet;

// read
using Stream input = File.OpenRead("data.parquet");
DataFrame df = await input.ReadParquetAsDataFrameAsync();

// write
using Stream output = File.Create("copy.parquet");
await df.WriteAsync(output);
```

Each top-level primitive column of the file becomes a `DataFrame` column. Nested columns (structs, lists and maps) are skipped.

This package is part of a fork of Parquet.Net maintained at [mukunku/parquet-dotnet](https://github.com/mukunku/parquet-dotnet).
