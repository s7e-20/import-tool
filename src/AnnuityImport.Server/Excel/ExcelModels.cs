namespace AnnuityImport.Server.Excel;

public enum ExcelValueKind
{
    Blank,
    Text,
    Number,
    DateTime,
    Boolean,
    Error
}

public sealed class ExcelCell
{
    public ExcelValueKind Kind { get; init; }
    public string? Text { get; init; }
    public decimal? Number { get; init; }
    public DateTime? DateTime { get; init; }
    public bool? Boolean { get; init; }
    public string Display { get; init; } = "";
    public string? Error { get; init; }

    public static ExcelCell Blank { get; } = new() { Kind = ExcelValueKind.Blank, Display = "" };

    public bool IsEmpty => Kind is ExcelValueKind.Blank || (Kind == ExcelValueKind.Text && Text is "");
}

public sealed class SourceColumn
{
    public required string Key { get; init; }
    public required string Header { get; init; }
    public required string Letter { get; init; }
    public int Index { get; init; }
    public List<string> Kinds { get; init; } = [];
    public List<SampleValue> Samples { get; init; } = [];
}

public sealed class SampleValue
{
    public int Row { get; init; }
    public string Display { get; init; } = "";
}

public sealed class ExcelDataRow
{
    public int RowNumber { get; init; }
    public Dictionary<string, ExcelCell> Cells { get; init; } = new(StringComparer.Ordinal);
}

public sealed class ExcelDataset
{
    public required string Worksheet { get; init; }
    public List<SourceColumn> Columns { get; init; } = [];
    public List<ExcelDataRow> Rows { get; init; } = [];
}

public sealed class WorksheetRead
{
    public required string Name { get; init; }
    public string? Error { get; init; }
    public ExcelDataset? Dataset { get; init; }
    public bool HasDataset => Dataset is not null;
}

public sealed class WorkbookRead
{
    public string? Error { get; set; }
    public List<WorksheetRead> Worksheets { get; init; } = [];
}
