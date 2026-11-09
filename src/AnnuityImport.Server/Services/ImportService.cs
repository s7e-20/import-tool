using System.Text.Json;
using NVGI.Projet.Importtool.Data;
using NVGI.Projet.Importtool.Excel;
using NVGI.Projet.Importtool.Mapping;
using Microsoft.EntityFrameworkCore;

namespace NVGI.Projet.Importtool.Services;

public sealed class ImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly ExcelDatasetReader _reader;
    private readonly AppDbContext _db;

    public ImportService(ExcelDatasetReader reader, AppDbContext db)
    {
        _reader = reader;
        _db = db;
    }

    public WorkbookRead ReadWorkbook(Stream stream) => _reader.Read(stream);

    public static MappingDocument? ParseMapping(string json, out string? error)
    {
        error = null;
        try
        {
            var mapping = JsonSerializer.Deserialize<MappingDocument>(json, JsonOptions);
            if (mapping is null)
            {
                error = "The mapping file is empty.";
                return null;
            }

            if (mapping.Version != 1)
            {
                error = "This mapping file version is not supported.";
                return null;
            }

            return mapping;
        }
        catch (JsonException)
        {
            error = "The mapping file is not valid JSON.";
            return null;
        }
    }

    public static object DatasetPayload(ExcelDataset dataset) => new
    {
        worksheet = dataset.Worksheet,
        rowCount = dataset.Rows.Count,
        columnCount = dataset.Columns.Count,
        firstRow = FirstRow(dataset),
        lastRow = LastRow(dataset),
        emptyRowsSkipped = EmptySkipped(dataset),
        columns = dataset.Columns.Select(column => new
        {
            key = column.Key,
            header = column.Header,
            letter = column.Letter,
            kinds = column.Kinds,
            samples = column.Samples.Select(sample => new { row = sample.Row, display = sample.Display })
        }),
        suggestions = AssignmentEngine.Suggest(dataset)
    };

    public static object? DistinctValues(ExcelDataset dataset, string header)
    {
        var column = dataset.Columns.FirstOrDefault(item =>
            string.Equals(item.Header, header, StringComparison.Ordinal)
            || string.Equals(item.Key, header.Trim(), StringComparison.OrdinalIgnoreCase));
        if (column is null)
            return null;

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var blanks = 0;
        foreach (var row in dataset.Rows)
        {
            if (!row.Cells.TryGetValue(column.Key, out var cell) || cell.IsEmpty)
            {
                blanks++;
                continue;
            }

            counts.TryGetValue(cell.Display, out var count);
            counts[cell.Display] = count + 1;
        }

        const int limit = 200;
        var ordered = counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new
        {
            header = column.Header,
            distinct = counts.Count,
            blankRows = blanks,
            dataRows = dataset.Rows.Count,
            truncated = ordered.Count > limit,
            values = ordered.Take(limit).Select(pair => new { value = pair.Key, count = pair.Value })
        };
    }

    public static object AssessmentPayload(AssessResult assessment) => new
    {
        definitionErrors = assessment.DefinitionErrors,
        targets = assessment.Targets.ToDictionary(
            pair => pair.Key,
            pair => new
            {
                status = pair.Value.Status,
                requiresDateFormat = pair.Value.RequiresDateFormat,
                requiresNumberFormat = pair.Value.RequiresNumberFormat,
                message = pair.Value.Message,
                suggestedDateFormat = pair.Value.SuggestedDateFormat,
                suggestedNumberPattern = pair.Value.SuggestedNumberPattern,
                suggestedDecimalSeparator = pair.Value.SuggestedDecimalSeparator,
                suggestedThousandsSeparator = pair.Value.SuggestedThousandsSeparator,
                suggestedThousandsNone = pair.Value.SuggestedThousandsNone,
                observedKinds = pair.Value.ObservedKinds,
                samples = pair.Value.Samples.Select(sample => new
                {
                    row = sample.Row,
                    source = sample.Source,
                    result = sample.Result,
                    error = sample.Error
                })
            },
            StringComparer.Ordinal)
    };

    public static object BuildPayload(BuildResult result) => new
    {
        ok = result.Ok,
        dataRows = result.DataRows,
        readyRows = result.ReadyRows,
        worksheet = result.Worksheet,
        definitionErrors = result.DefinitionErrors,
        errors = result.Errors.Select(issue => new { row = issue.Row, target = issue.Target, message = issue.Message })
    };

    public object Inspect(WorkbookRead workbook, MappingDocument mapping)
    {
        var sheets = SheetPayload(workbook);
        if (!string.IsNullOrWhiteSpace(mapping.Worksheet))
        {
            var saved = FindSheet(workbook, mapping.Worksheet);
            if (saved?.Dataset is null)
            {
                return new
                {
                    worksheetLocked = true,
                    needsWorksheet = false,
                    worksheet = mapping.Worksheet,
                    worksheets = sheets,
                    message = $"The saved worksheet '{mapping.Worksheet}' is not available in this workbook. Create a new mapping."
                };
            }

            return new
            {
                worksheetLocked = true,
                needsWorksheet = false,
                worksheet = saved.Name,
                worksheets = sheets,
                message = AssignmentEngine.SourceMismatch(saved.Dataset, mapping),
                rowCount = saved.Dataset.Rows.Count
            };
        }

        var usable = workbook.Worksheets.Where(sheet => sheet.HasDataset).ToList();
        if (usable.Count == 1)
        {
            return new
            {
                worksheetLocked = false,
                needsWorksheet = false,
                worksheet = usable[0].Name,
                worksheets = sheets,
                message = AssignmentEngine.SourceMismatch(usable[0].Dataset!, mapping),
                rowCount = usable[0].Dataset!.Rows.Count
            };
        }

        return new
        {
            worksheetLocked = false,
            needsWorksheet = true,
            worksheet = (string?)null,
            worksheets = sheets,
            message = usable.Count == 0 ? "None of the worksheets contain a dataset." : (string?)null
        };
    }

    public (BuildResult? Result, string? Error) PrepareExecute(WorkbookRead workbook, MappingDocument mapping, string? requestedWorksheet, string fileName)
    {
        WorksheetRead? sheet;
        if (!string.IsNullOrWhiteSpace(mapping.Worksheet))
        {
            sheet = FindSheet(workbook, mapping.Worksheet);
            if (sheet?.Dataset is null)
                return (null, $"The saved worksheet '{mapping.Worksheet}' is not available in this workbook. Create a new mapping.");
        }
        else if (!string.IsNullOrWhiteSpace(requestedWorksheet))
        {
            sheet = FindSheet(workbook, requestedWorksheet);
            if (sheet?.Dataset is null)
                return (null, $"Worksheet '{requestedWorksheet}' does not contain a dataset.");
        }
        else
        {
            var usable = workbook.Worksheets.Where(item => item.HasDataset).ToList();
            if (usable.Count == 1)
                sheet = usable[0];
            else
                return (null, "Select the worksheet to load.");
        }

        var mismatch = AssignmentEngine.SourceMismatch(sheet.Dataset!, mapping);
        if (mismatch is not null)
            return (null, mismatch);

        var built = AssignmentEngine.Build(sheet.Dataset!, mapping, Path.GetFileName(fileName), DateTime.Now);
        return (built, null);
    }

    public async Task<object> ExecuteAsync(BuildResult built, CancellationToken cancellationToken)
    {
        if (!built.Ok)
            return BuildPayload(built);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var (entity, index) in built.Rows.Select((entity, index) => (entity, index)))
            {
                _db.CaseTransferts.Add(entity);
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    var rowNumber = index + 1;
                    return new
                    {
                        ok = false,
                        inserted = 0,
                        dataRows = built.DataRows,
                        readyRows = built.ReadyRows,
                        definitionErrors = built.DefinitionErrors,
                        errors = new[]
                        {
                            new
                            {
                                row = (int?)null,
                                target = (string?)null,
                                message = $"SQL Server rejected the load while saving data row {rowNumber}. {Unwrap(ex)} Nothing was inserted."
                            }
                        }
                    };
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var ids = built.Rows.Select(row => row.IdRecord).ToList();
        return new
        {
            ok = true,
            inserted = built.Rows.Count,
            dataRows = built.DataRows,
            readyRows = built.ReadyRows,
            firstId = ids.Count == 0 ? (int?)null : ids.Min(),
            lastId = ids.Count == 0 ? (int?)null : ids.Max(),
            definitionErrors = Array.Empty<string>(),
            errors = Array.Empty<object>()
        };
    }

    public static object SheetPayload(WorkbookRead workbook) =>
        workbook.Worksheets.Select(sheet => new
        {
            name = sheet.Name,
            hasDataset = sheet.HasDataset,
            error = sheet.Error,
            rowCount = sheet.Dataset?.Rows.Count ?? 0,
            columnCount = sheet.Dataset?.Columns.Count ?? 0,
            firstRow = sheet.Dataset is null ? null : FirstRow(sheet.Dataset),
            lastRow = sheet.Dataset is null ? null : LastRow(sheet.Dataset),
            emptyRowsSkipped = sheet.Dataset is null ? 0 : EmptySkipped(sheet.Dataset)
        });

    private static int? FirstRow(ExcelDataset dataset) =>
        dataset.Rows.Count == 0 ? null : dataset.Rows.Min(row => row.RowNumber);

    private static int? LastRow(ExcelDataset dataset) =>
        dataset.Rows.Count == 0 ? null : dataset.Rows.Max(row => row.RowNumber);

    private static int EmptySkipped(ExcelDataset dataset)
    {
        if (dataset.Rows.Count == 0 || LastRow(dataset) is not int last)
            return 0;
        return Math.Max(0, last - 1 - dataset.Rows.Count);
    }

    private static WorksheetRead? FindSheet(WorkbookRead workbook, string name) =>
        workbook.Worksheets.FirstOrDefault(sheet => sheet.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string Unwrap(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
            current = current.InnerException;
        return current.Message;
    }
}
