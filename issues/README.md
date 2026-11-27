# parquet-dotnet issues

Issues from the deleted GitHub repository aloneguid/parquet-dotnet, recovered from GH Archive (the public record of GitHub events). There is one file per issue, named by issue number.

| | Recovered | Partially recovered | Not recovered |
|---|---:|---:|---:|
| Open (78) | 53 | 7 | 18 |
| Closed (181) | 114 | 61 | 6 |

**Recovered** means the description and all comments were found. **Partially recovered** means some text was found but the description or some comments are missing. **Not recovered** means only the title and metadata survive.

## Open issues

**Fixed in** is the first release containing the fix, for issues still listed as open here although they were fixed later. The version comes from the release notes, or from the maintainer's comment in the thread when the notes don't mention the issue (#548, #563, #583). _(likely)_ means only the title survives, so the match is inferred.

| # | Title | Created | Status | Fixed in |
|---:|---|---|---|---|
| [302](302.md) | Enum serialization | 2023-04-18 | Partially recovered | |
| [367](367.md) | DataColumn sliced buffers | 2023-07-08 | Recovered | |
| [376](376.md) | Deserialize parquet file to JSON | 2023-08-02 | Recovered | 4.16.0 |
| [393](393.md) | [BUG]: ArgumentOutOfRangeException thrown when trying to write column (interop type narrowing) | 2023-09-03 | Recovered | |
| [410](410.md) | [BUG]: ParquetSerializer doesn't support different JsonPropertyName and ClrPropertyName on struct fields | 2023-09-27 | Recovered | 4.17.0 |
| [434](434.md) | Class deserializer should not require ParquetRequired attribute for required strings | 2023-11-21 | Recovered | |
| [456](456.md) | [BUG]: Unable to deserialize class with Array | 2024-01-15 | Recovered | |
| [474](474.md) | support for object datatype | 2024-02-08 | Recovered | |
| [484](484.md) | Feature request: add API for 2 step group writting | 2024-02-29 | Recovered | |
| [488](488.md) | [BUG]: Enum Serialization | 2024-03-16 | Recovered | |
| [507](507.md) | Feature requst: extend DataColumn API to read column values directly into provided Span/Memory/Array | 2024-05-02 | Partially recovered | |
| [513](513.md) | Serialization of interface typed members | 2024-05-30 | Recovered | |
| [525](525.md) | [BUG]: Floor does not handle Dictionary<string, DateTime> | 2024-07-01 | Recovered | |
| [531](531.md) | Support Dictionary encoding for more types. | 2024-07-09 | Recovered | |
| [532](532.md) | Support RLE encoding in data pages for bools | 2024-07-16 | Recovered | |
| [533](533.md) | [BUG]: Incorrect data type for TimeSpan | 2024-07-19 | Recovered | |
| [535](535.md) | [BUG]: non null string fails with InvalidDataException and is wrongly flagged as nullable | 2024-07-22 | Recovered | |
| [544](544.md) | [BUG]: "don't know how to skip type Set" error | 2024-08-26 | Recovered | 6.2.0 |
| [545](545.md) | Support `ParquetTimestampResolution.Microseconds` for Unity (.Net Standard 2.1) | 2024-09-01 | Recovered | |
| [548](548.md) | [BUG]:  | 2024-09-10 | Recovered | 5.0.0 |
| [552](552.md) | [BUG]: Possible multiple enumeration while serializing | 2024-09-13 | Recovered | |
| [557](557.md) | [BUG]: breaking change: class serialization: F# records with mutable fields are duped with @ suffix | 2024-10-01 | Recovered | |
| [563](563.md) | [BUG]: Parquet compression is not working inside a Linux container in Docker Desktop | 2024-10-09 | Recovered | 5.0.1 |
| [570](570.md) | No streaming support, no predicate support, everything is loaded in memory | 2024-11-12 | Recovered | |
| [571](571.md) | Reintegrate "Support for Roslyn nullable annotations" | 2024-11-13 | Recovered | |
| [574](574.md) | [BUG]:  | 2024-11-25 | Recovered | |
| [576](576.md) | [BUG]: Only x out of y bytes are available | 2024-12-02 | Recovered | 5.1.0 |
| [579](579.md) | [BUG]: only 6585 out of 26392 bytes are available | 2024-12-16 | Recovered | |
| [582](582.md) | [BUG]: Handle nested struct serialization | 2024-12-21 | Recovered | 5.4.0 |
| [583](583.md) | [BUG]: Can't Read Parquet Files Created with parquet-mr 1.14.0+ | 2024-12-24 | Recovered | 5.1.0 |
| [584](584.md) | [BUG]:  | 2025-01-03 | Recovered | |
| [586](586.md) | Untyped serializer and complex types (class or struct) | 2025-01-09 | Recovered | |
| [588](588.md) | Feature Request: Full Nanosecond precision support | 2025-01-13 | Recovered | |
| [589](589.md) | DataFrame WriteAsync always creates nullable data types in schema | 2025-01-14 | Recovered | |
| [590](590.md) | Is there a way to create a reader without a seekable stream? | 2025-01-21 | Recovered | |
| [591](591.md) | [BUG]: Incorrect values read for column pathOrInlineDv in version 3.10.0 | 2025-01-30 | Recovered | |
| [594](594.md) | [BUG]: Cannot Serialize List of Dictionary | 2025-02-11 | Recovered | |
| [599](599.md) | DELTA_BINARY_PACKED on short,ushort,ulong,uint | 2025-02-28 | Partially recovered | |
| [600](600.md) | isAdjustedToUTC is not valid. | 2025-03-03 | Recovered | |
| [608](608.md) | Standardize the usage of "serialize" over "serialise" | 2025-04-03 | Recovered | |
| [609](609.md) | [BUG]: Floor - If first column is a boolean it is only partially rendered | 2025-04-07 | Recovered | |
| [612](612.md) | [BUG]: Serialising a list of lists fails | 2025-04-17 | Recovered | 5.4.0 |
| [613](613.md) | Required struct and DL miss match | 2025-04-30 | Recovered | |
| [614](614.md) | How can I declare that a column is sorted ? | 2025-05-14 | Recovered | |
| [618](618.md) | Support an option to have required reference types | 2025-06-05 | Not recovered | |
| [621](621.md) | clarification of "zero dependencies" | 2025-06-16 | Recovered | |
| [623](623.md) | DataPageHeaderV2.IsCompressed is defaulting to false on read even though the spec seems to say it should be true | 2025-06-20 | Recovered | 5.3.0 |
| [624](624.md) | Ability to specify custom deserialization for a specific type | 2025-06-21 | Recovered | |
| [626](626.md) | NAOT Warning: TypeExtensions.GetNullable(Type) | 2025-06-27 | Recovered | |
| [627](627.md) | IAsyncEnumerable serializer | 2025-07-02 | Recovered | |
| [628](628.md) | How to enable dictionary encoding? | 2025-07-14 | Recovered | 5.5.0 |
| [630](630.md) | [BUG]: Serialize doesn't handle empty + null list of nullable type | 2025-07-20 | Recovered | |
| [634](634.md) | [BUG]: Floor has incorrect installer hash when fetched from winget | 2025-08-02 | Recovered | |
| [635](635.md) | [BUG]: Very large parquet files cannot be read | 2025-08-03 | Partially recovered | |
| [638](638.md) | [BUG]: `DivideByZeroException` when reading boolean columns in Parquet.Net 5.x (RLE decoding bug) | 2025-08-12 | Recovered | 6.2.0 |
| [640](640.md) | Possibly faulty field skipping for thrift structs during reading | 2025-08-15 | Recovered | 6.2.0 |
| [642](642.md) | DELTA_BINARY_PACKED on DateTime | 2025-08-31 | Recovered | |
| [643](643.md) | Inconsistent results when encoding Boolean values | 2025-09-05 | Partially recovered | 5.4.0 |
| [648](648.md) | [BUG]: Anything other than a `class` cannot be desrialsed | 2025-09-18 | Recovered | |
| [666](666.md) | [BUG]: Unhandled exception: System.InvalidOperationException: Not all columns were written. | 2025-10-29 | Partially recovered | |
| [721](721.md) | [BUG]: ParquetSerializer.DeserializeAllAsync on file with no rows throws InvalidOperationException | 2026-03-25 | Not recovered | 6.0.0 |
| [723](723.md) | ThriftCompactProtocolReader.SkipField crashes on corrupted footer from interrupted append | 2026-03-26 | Not recovered | 6.2.0 (likely) |
| [758](758.md) | [BUG]: Guid/UUID min/max statistics appear to use .NET Guid byte order | 2026-05-21 | Partially recovered | |
| [764](764.md) | [BUG]: DeserializeAsync(rowGroupIndex:) allocates a result list sized to the whole file, causing OOM on large files | 2026-06-05 | Not recovered | |
| [765](765.md) | gzip write default compression level changed from Optimal to SmallestSize in 6.0 (undocumented) | 2026-06-05 | Not recovered | |
| [766](766.md) | [BUG]: Row group reader failed on string list | 2026-07-05 | Not recovered | |
| [767](767.md) | DATE logical type incorrectly mapped to DateTime instead of DateOnly | 2026-07-12 | Not recovered | |
| [769](769.md) | [BUG]: Wrong stats for Timestamp micros | 2026-07-15 | Not recovered | |
| [771](771.md) | [BUG]: TreatByteArrayAsString being ignored in specific case. | 2026-07-19 | Not recovered | |
| [772](772.md) | [Feature request] Allow definitionLevels / nullablity to be user provided | 2026-07-20 | Not recovered | |
| [773](773.md) | Code examples in README.md do not compile | 2026-07-21 | Not recovered | |
| [775](775.md) | [BUG]: There is no public API to read string values from a list column directly and no example | 2026-07-29 | Not recovered | |
| [778](778.md) | [BUG]: element type System.DateTime is not supported in DELTA_BINARY_PACKED | 2026-08-05 | Not recovered | |
| [793](793.md) | Change Deserialization to use Activator.CreateInstance | 2026-09-18 | Not recovered | |
| [794](794.md) | Add WriteAllPartsAsync() Overload to accept Custom Metadata | 2026-09-22 | Not recovered | |
| [796](796.md) | [BUG]: TimeSpan/TimeOnly output not compatible with AWS Athena | 2026-09-28 | Not recovered | 6.2.0 (likely) |
| [797](797.md) | Read Performance & Allocations Regressed For Strings in V6 vs. V5 | 2026-10-09 | Not recovered | |
| [800](800.md) | [BUG]: Corrupted readings on unknown parquet struct | 2026-11-04 | Not recovered | 6.2.0 (likely) |

## Closed issues

| # | Title | Created | Status |
|---:|---|---|---|
| [97](97.md) | Issue with finding System.Buffers assembly | 2021-01-12 | Recovered |
| [121](121.md) | Incorrect Definition Level for Null | 2021-08-11 | Partially recovered |
| [126](126.md) | Parquet.ParquetException: 'NewField' does not exist in this file (Schema evolution with ParquetConvert.Deserialize) | 2021-10-15 | Partially recovered |
| [133](133.md) | Question: How would I specify CultureInfo.InvariantCulture alongside table.ToString("j") | 2021-11-12 | Partially recovered |
| [151](151.md) | ParquetWriter - Byte[] Impacting File Size Outputs  | 2022-02-15 | Partially recovered |
| [165](165.md) | Allow Scale of 0 for Decimal Types | 2022-06-01 | Recovered |
| [171](171.md) | Writing null to a struct field throws null reference exception | 2022-07-22 | Recovered |
| [174](174.md) | Feature Request: Add possibility to pass ParquetOptions to ParquetReader in ParquetConvert.Deserialize | 2022-07-27 | Partially recovered |
| [183](183.md) | Poor performance when writing to Azure storage | 2022-09-30 | Recovered |
| [185](185.md) | NULL in struct field writer DL=0 instead of 1 | 2022-10-07 | Recovered |
| [198](198.md) | Does not work ParquetReader.CreateAsync() for read files | 2022-11-24 | Recovered |
| [205](205.md) | Is the DELTA_BINARY_PACKED encoding not supported? If it is, could you point out how to use it? | 2022-12-08 | Partially recovered |
| [206](206.md) | ability to write custom column (chunck) data | 2022-12-09 | Partially recovered |
| [207](207.md) | Unable to load shared library nironcompress (Macbook Pro Intel, Azure Function) | 2022-12-15 | Recovered |
| [214](214.md) | IndexOutOfRangeException when trying to read timedelta column written by fastparquet | 2023-01-02 | Partially recovered |
| [221](221.md) | 🈁💌💗 Post your use cases / software / screenshots here | 2023-01-12 | Partially recovered |
| [224](224.md) | ParquetConvert.DeserializeAsync throw Exception System.ArgumentOutOfRangeException  for null DateTime | 2023-01-17 | Recovered |
| [227](227.md) | [feature request] functionality/documentation for merging existing parquet files | 2023-01-18 | Partially recovered |
| [235](235.md) | Add option to decide compression level of for instance gzip compression | 2023-01-24 | Recovered |
| [240](240.md) | Make it configurable to chose native compression over managed implementation | 2023-01-26 | Partially recovered |
| [243](243.md) | File corruption for string columns? | 2023-01-28 | Recovered |
| [244](244.md) | Signed byte vs unsigned byte schema | 2023-01-29 | Recovered |
| [245](245.md) | There is no CreateRowGroupAsync method inside parquetwriter which shown in the sample code | 2023-01-29 | Recovered |
| [251](251.md) | Expose written DataColumns in WriteAsync | 2023-02-01 | Partially recovered |
| [252](252.md) | Can I read DataColumnStatistics of the column only before reading the entire column data ? | 2023-02-06 | Partially recovered |
| [257](257.md) | Not able to mark property as Optional for DeserializeAsync | 2023-02-08 | Partially recovered |
| [258](258.md) | Read schema not equal to written schema | 2023-02-09 | Recovered |
| [260](260.md) | ParquetConvert.SerializeAsync with "append: true" doesn't work | 2023-02-10 | Recovered |
| [261](261.md) | Cannot parse Parquet statistics when trying to read files using Spark | 2023-02-14 | Recovered |
| [263](263.md) | Reading/writing complex types | 2023-02-15 | Recovered |
| [265](265.md) | Class Serialisation - Guid support | 2023-02-15 | Recovered |
| [266](266.md) | WriteColumnAsync throwing exception when serializing Int32? | 2023-02-16 | Recovered |
| [267](267.md) | IndexOutofRangeException when read parquet file | 2023-02-17 | Recovered |
| [268](268.md) | Serialization is loosing properties form base class of base class | 2023-02-17 | Recovered |
| [272](272.md) | WriteColumnAsync throwing exception when serializing Int32 | 2023-02-21 | Recovered |
| [275](275.md) | [BUG]: .netstandard 2.0 compatibility (maybe only when using matlab) | 2023-02-27 | Recovered |
| [277](277.md) | Low-level Parquet write API doesn't consistently / gracefully handle arrays of nullable value types without null values | 2023-03-14 | Partially recovered |
| [278](278.md) | [BUG]: cannot open file with column names ending in periods | 2023-03-21 | Recovered |
| [279](279.md) | Append in JsonSerializer | 2023-03-23 | Partially recovered |
| [281](281.md) | Clarify guidance on list vs arrays | 2023-03-25 | Partially recovered |
| [282](282.md) | [BUG]: Nested nulls can be interpreted at the wrong level  | 2023-03-26 | Partially recovered |
| [283](283.md) | ParquetSerializer 100x slower than ParquetConvert to serialise IEnumerable<T> | 2023-03-27 | Partially recovered |
| [286](286.md) | [BUG]: 4.6.4 returns int arrays as all nulls instead of value | 2023-03-27 | Partially recovered |
| [287](287.md) | Optional values for Row | 2023-04-01 | Recovered |
| [288](288.md) | DataColumn class doesn't expose any info about definition levels | 2023-04-03 | Partially recovered |
| [291](291.md) | [BUG]: Unable to write parquet file with multiple row groups using ParquetRowGroupWriter | 2023-04-07 | Recovered |
| [292](292.md) | Clarification on serialization of dates, times, and decimals | 2023-04-11 | Recovered |
| [293](293.md) | [BUG]: cannot count nulls in type System.Nullable`1[System.DateTimeOffset] | 2023-04-11 | Partially recovered |
| [295](295.md) | Protobuf with parquet | 2023-04-14 | Partially recovered |
| [296](296.md) | [BUG]: can't read parquet files for required fields | 2023-04-14 | Recovered |
| [297](297.md) | Looking for little help | 2023-04-15 | Recovered |
| [300](300.md) | [BUG]: Empty arrays are serialized incorrectly | 2023-04-17 | Partially recovered |
| [305](305.md) | Support for Logical Types | 2023-04-19 | Partially recovered |
| [306](306.md) | Decimal precision/scale wrt whole value... | 2023-04-19 | Partially recovered |
| [309](309.md) | Very slow performance reading wide parquet file using V4 | 2023-04-20 | Partially recovered |
| [311](311.md) | [BUG]: Map Field's 'Key' DataColumn is Empty Starting with v4.8.0 | 2023-04-22 | Recovered |
| [312](312.md) | [BUG]: Loss of precision for DateTime milliseconds | 2023-04-24 | Recovered |
| [315](315.md) | Document migration guide from DateTimeOffset to DateTime | 2023-04-28 | Recovered |
| [316](316.md) | [BUG]: Malformed levels in nested array of structs | 2023-05-02 | Recovered |
| [317](317.md) | Decimal types are serialized as byte array... | 2023-05-04 | Partially recovered |
| [318](318.md) | Issue using DECIMAL data type... | 2023-05-05 | Recovered |
| [320](320.md) | [BUG]: Decimal type failure issue  | 2023-05-05 | Recovered |
| [321](321.md) | [BUG]:  simple array can not be desterilized  | 2023-05-07 | Recovered |
| [323](323.md) | [BUG]: ParquetReader treats decimal columns as byte array | 2023-05-09 | Partially recovered |
| [328](328.md) | [BUG]: Parquet.net 4.9.1 or newer hangs. | 2023-05-10 | Partially recovered |
| [329](329.md) | Deserialize parquet file as RowGroups | 2023-05-11 | Partially recovered |
| [331](331.md) | [BUG]: cannot count nulls in type System.Nullable`1[System.Guid] | 2023-05-16 | Recovered |
| [334](334.md) | [BUG]: Breaking change somewhere between 4.6.2 and 4.11.1: Method not found `ctor ParquetSchema(IReadOnlyCollection)` | 2023-05-19 | Partially recovered |
| [335](335.md) | [BUG]: Files written by Parquet.Net v4 can't be read by ParquetSharp or fastparquet | 2023-05-19 | Partially recovered |
| [337](337.md) | [BUG]: Exception when serializing  | 2023-05-20 | Recovered |
| [343](343.md) | [BUG]: `DataFrameMapper` returns incompatible DataFrameColumn | 2023-05-31 | Recovered |
| [344](344.md) | [BY DESIGN]: ParquetSerializer collection deserialization issue  | 2023-05-31 | Partially recovered |
| [345](345.md) | Json Attribute usage breaks several use cases | 2023-06-04 | Recovered |
| [346](346.md) | Mismatch between ReadMe and Nuget package | 2023-06-07 | Recovered |
| [347](347.md) | [BUG]: Error serializing DateTimeOffset DayOfWeek has no fields | 2023-06-08 | Recovered |
| [350](350.md) | [BUG]: Unable to deserialize Int64 using Class Serialization | 2023-06-13 | Partially recovered |
| [353](353.md) | Read const amount of rows from a column | 2023-06-18 | Recovered |
| [358](358.md) | Support writing UTF8 bytes directly as string | 2023-06-28 | Recovered |
| [360](360.md) | [BUG]: Breaking change in 4.13.0 - string properties no longer allow null by default | 2023-06-29 | Recovered |
| [361](361.md) | Does the library support treating missing columns as nulls? | 2023-06-29 | Partially recovered |
| [362](362.md) | Support Writing Int64 timestamp MICROS unit | 2023-06-29 | Recovered |
| [365](365.md) | [BUG]: ReadParquetAsDataFrameAsync() has a meaningless `if(idf >= columns.Count)` | 2023-06-30 | Recovered |
| [370](370.md) | Class deserialiser does not support Immutable types | 2023-07-17 | Recovered |
| [371](371.md) | System.OutOfMemoryException exception obtained when writing data of ~1 KB. | 2023-07-19 | Recovered |
| [375](375.md) | Add DELTA_BINARY_PACKED encoder For Int32 And Int64 | 2023-08-01 | Partially recovered |
| [377](377.md) | Replace Row API with Cell API | 2023-08-02 | Recovered |
| [386](386.md) | IAsyncEnumerable deserializer | 2023-08-17 | Recovered |
| [389](389.md) | [BUG]: ParquetRowGroupReader.ReadColumnAsync returning wrong values for Int32 columns | 2023-08-18 | Partially recovered |
| [390](390.md) | [Class Serialisation﻿]: is ExtendoObject supported by Class Serialisation﻿ ? Thx. | 2023-08-20 | Recovered |
| [392](392.md) | Way to override the new default encoding DELTA_BINARY_PACKED | 2023-09-03 | Recovered |
| [396](396.md) | [BUG]: Class serializer writes map key as optional | 2023-09-08 | Partially recovered |
| [398](398.md) | Control element name within a list  | 2023-09-13 | Recovered |
| [400](400.md) | [BUG]: Table.WriteAsync does not handle empty lists correctly | 2023-09-19 | Partially recovered |
| [401](401.md) | [BUG]: ColumnMetaData doesn't list PLAIN_DICTIONARY in the encodings list | 2023-09-20 | Recovered |
| [402](402.md) | Arbitrary serialization has vanished | 2023-09-21 | Recovered |
| [405](405.md) | fields not supported | 2023-09-24 | Partially recovered |
| [406](406.md) | unable to read the file | 2023-09-24 | Recovered |
| [408](408.md) | ParquetTimestampAttribute has no effect on nullable DateTime properties | 2023-09-26 | Recovered |
| [409](409.md) | [BUG]: JSON serializer doesn't produce valid json (table) | 2023-09-27 | Recovered |
| [411](411.md) | [BUG]: ParquetSerializer can sometimes fail when populating _typeToAssembler cache in parallel | 2023-10-02 | Partially recovered |
| [414](414.md) | [BUG]: Destination is too short. (Parameter 'destination') Error from 4.0.0 onwards for Specific Parquet Files | 2023-10-10 | Recovered |
| [415](415.md) | [Question] What is the best way to split a parquet file without loading all in memory? | 2023-10-12 | Recovered |
| [416](416.md) | [BUG]: Scale must be less than the precision - should be "cannot be larger than" | 2023-10-16 | Recovered |
| [422](422.md) | ParquetSerializer should allow deserialization of open RowGroupReaders | 2023-10-27 | Recovered |
| [424](424.md) | [Question]  Ergonomically adding columns to an existing pq file | 2023-10-29 | Recovered |
| [425](425.md) | Migrating from v3 to v4 question... | 2023-10-31 | Partially recovered |
| [427](427.md) | ParquetSerializer.SerializeAsync thread safe?? | 2023-11-08 | Partially recovered |
| [429](429.md) | [BUG]:  | 2023-11-13 | Recovered |
| [431](431.md) | Expando Objects | 2023-11-15 | Partially recovered |
| [435](435.md) | Automatic Serialization multiples class with metadata | 2023-11-24 | Recovered |
| [436](436.md) | Possible memory leak in DataColumnWriter | 2023-11-28 | Partially recovered |
| [437](437.md) | [BUG]: ParquetSerializer.DeserializeAsync - "Not a parquet" file when using buffer with fixed size | 2023-12-02 | Recovered |
| [441](441.md) | Modifying and writing back results in a corrupted file | 2023-12-17 | Partially recovered |
| [442](442.md) | [BUG]: Fail to serialize enum | 2023-12-21 | Recovered |
| [445](445.md) | [BUG]: don't know how to skip type Uuid | 2023-12-31 | Recovered |
| [451](451.md) | Exposed public classes under `System` namespace cause conflicts with other third party libraries | 2024-01-08 | Recovered |
| [461](461.md) | [BUG]: Append serialize to file-path actually truncates file and then throws because it's empty | 2024-01-20 | Recovered |
| [463](463.md) | [BUG]: Can't read Parquet file with fixed_len_byte_array_column data type generated by Python | 2024-01-22 | Partially recovered |
| [464](464.md) | string values are written as nulls in parquet file when data is set using Array SetValue Method | 2024-01-24 | Partially recovered |
| [466](466.md) | Allow registering custom types for class serialization | 2024-01-25 | Partially recovered |
| [467](467.md) | [BUG]: Error on deserializing not required DateTime properties | 2024-01-25 | Recovered |
| [469](469.md) | [BUG]: DataFrame loading doesn't support short (int16) columns. | 2024-01-26 | Recovered |
| [470](470.md) | [BUG]: ParquetSerializer.DeserializeAsync | 2024-01-30 | Recovered |
| [472](472.md) | [BUG]: Using Nuget Package with dotnet 8 doesn't support DateOnly type. | 2024-02-02 | Recovered |
| [473](473.md) | [BUG]: ParquetSerializer.DeserializeAllAsync internally causes "Sequence contains no elements" error when empty(or bad) file is provided | 2024-02-05 | Recovered |
| [475](475.md) | Issues converting to CSV | 2024-02-18 | Recovered |
| [476](476.md) | Issue reading parquet from a stream | 2024-02-21 | Recovered |
| [477](477.md) | Issue with certain stream sources | 2024-02-21 | Recovered |
| [478](478.md) | Deserialise new timestamp parquet format with low-level api | 2024-02-21 | Recovered |
| [479](479.md) | [BUG]: Risk for deadlock when ParquetWriter is disposed? | 2024-02-23 | Recovered |
| [481](481.md) | [BUG]: empty string array incorrect serialization | 2024-02-26 | Partially recovered |
| [482](482.md) | [BUG]: Reading decimal fields ignores precision and scale | 2024-02-28 | Recovered |
| [486](486.md) | [BUG]: Deserialising delta binary packed encoded data produces incorrect results | 2024-03-08 | Recovered |
| [492](492.md) | Unable to write Timestamp Logical Type | 2024-03-25 | Partially recovered |
| [494](494.md) | [BUG]: If the data transferred to WriteColumnAsync is too large, an error occurs | 2024-03-26 | Partially recovered |
| [495](495.md) | [BUG]: Serialisation error for class with nullable struct with child property | 2024-04-03 | Recovered |
| [496](496.md) | [BUG]: Big Endian Guid problem | 2024-04-04 | Partially recovered |
| [497](497.md) | Parquet file directly in binary | 2024-04-05 | Recovered |
| [499](499.md) | Working with null values when reading a Parquet file in FSharp | 2024-04-11 | Recovered |
| [502](502.md) | [BUG]: Deserialize nullable LIST, ArgumentOutOfRange exception | 2024-04-22 | Recovered |
| [503](503.md) | [BUG]: Exception: scale must be less than the precision | 2024-04-25 | Recovered |
| [512](512.md) | [BUG]: Deserializing nonnullable string field into nullable string  property throws exception | 2024-05-24 | Recovered |
| [514](514.md) | Do not try to set readonly properties when deserializing | 2024-05-30 | Recovered |
| [517](517.md) | Serializes the nullable TimeSpan property | 2024-06-06 | Recovered |
| [518](518.md) | Reading row groups in parallel | 2024-06-07 | Partially recovered |
| [523](523.md) | Breaking change was introduced with version 4.23.5 | 2024-06-24 | Recovered |
| [524](524.md) | [Feature Request] Add GeoParquet support and ability to read a file over HTTP | 2024-06-28 | Recovered |
| [526](526.md) | [BUG]: generated parquet cannot be read by Athena | 2024-07-03 | Recovered |
| [528](528.md) | Help deciphering exception | 2024-07-05 | Recovered |
| [536](536.md) | Add de-serializer options to handle Pascal case similar to JsonSerializerOptions.PropertyNameCase | 2024-07-22 | Recovered |
| [539](539.md) | Support BYTE_STREAM_SPLIT encoding | 2024-07-30 | Recovered |
| [540](540.md) | [BUG]: No rows read from an Amazon CUR 2.0 Parquet File | 2024-08-02 | Partially recovered |
| [541](541.md) | System.IO.FileNotFoundException thrown while using Append mode in Class serialization | 2024-08-15 | Recovered |
| [542](542.md) | [BUG]: parquet containing ConvertedType TIMESTAMP_MILLIS may throw IndexOutOfRangeException while reading. | 2024-08-21 | Recovered |
| [549](549.md) | When serializing to stream, cursor is not at the beginning | 2024-09-11 | Recovered |
| [554](554.md) | Sample given in https://aloneguid.github.io/parquet-dotnet/writing.html does not work | 2024-09-25 | Recovered |
| [555](555.md) | [BUG]: 4.25 breaks binary compatibility with files serialized using 4.24 and De/Serialize<T> | 2024-09-30 | Recovered |
| [558](558.md) | [BUG]: Exception from ReadParquetAsDataFrameAsync() | 2024-10-02 | Recovered |
| [564](564.md) | [BUG]: 32 bits stopped to work after upgrading to 5.0.0 | 2024-10-14 | Recovered |
| [573](573.md) | [BUG]: Type mismatch silently leads to incorrect de-serialization result | 2024-11-25 | Partially recovered |
| [575](575.md) | [BUG]: Corrupt files are produced when saving DataFrame to Parquet | 2024-11-29 | Recovered |
| [577](577.md) | [BUG]: The PAR1 magic number is sometimes duplicated at the end of the file. | 2024-12-06 | Recovered |
| [585](585.md) | Typo in documentation (DataF1eld instead of DataField) | 2025-01-09 | Recovered |
| [587](587.md) | Floor application immediately killed (osx) | 2025-01-10 | Recovered |
| [596](596.md) | [BUG]: `ParquetSchema` equality collisions | 2025-02-20 | Recovered |
| [601](601.md) | [BUG]: When calling ParquetSerializer.SerializeAsync() in .NET Framework 4.8, an error occurs because System.Text.Json 8.0.0 is not found. | 2025-03-03 | Recovered |
| [603](603.md) | [BUG]: System.InvalidOperationException: 'don't know how to skip type 14' | 2025-03-17 | Recovered |
| [605](605.md) | [BUG]:Parquet.Serialization.Dremel.ParquetDictionary`2[System.Int32,System.String] | 2025-03-24 | Recovered |
| [606](606.md) | [BUG]:   Failed to Serialization/Deserialization the dictionary's key type is an integer. | 2025-03-26 | Recovered |
| [617](617.md) | [BUG]: Cannot read a parquet file with DateTime fields as int96 | 2025-06-03 | Recovered |
| [619](619.md) | Ability to include a field in Parquet but not JSON | 2025-06-06 | Partially recovered |
| [629](629.md) | Add native glibc build and/or fully‐static musl build for Raspberry Pi OS | 2025-07-15 | Recovered |
| [659](659.md) | [BUG]: Reading a Parquet File with a date of 0001-01-01 throws an exception | 2025-10-10 | Partially recovered |
| [741](741.md) | [BUG]: System.FieldAccessException: failed to compile when using string[] | 2026-05-05 | Partially recovered |
| [748](748.md) | [BUG]: | 2026-05-07 | Partially recovered |
| [751](751.md) | [BUG]: Can no longer read fields with no definition levels defined | 2026-05-08 | Not recovered |
| [752](752.md) | [BUG]: Can't read nullable primitives anymore | 2026-05-08 | Not recovered |
| [753](753.md) | [BUG]: Update Snappier library reference due to vulnerability in it | 2026-05-11 | Not recovered |
| [754](754.md) | No ReadAsync overload for byte[] type columns | 2026-05-13 | Not recovered |
| [763](763.md) | Dependency IronCompress repository deleted? | 2026-06-04 | Not recovered |
| [770](770.md) | [BUG]: IAsyncEnumerable overloads not available in .NET 10 nuget | 2026-07-17 | Not recovered |
