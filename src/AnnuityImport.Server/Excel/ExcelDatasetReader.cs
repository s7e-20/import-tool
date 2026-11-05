using System.Globalization;
using System.IO.Packaging;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AnnuityImport.Server.Excel;

public sealed partial class ExcelDatasetReader
{
    private static readonly HashSet<uint> BuiltInDateFormatIds =
    [
        14, 15, 16, 17, 18, 19, 20, 21, 22,
        27, 28, 29, 30, 31, 32, 33, 34, 35, 36,
        45, 46, 47, 50, 51, 52, 53, 54, 55, 56, 57, 58
    ];

    public WorkbookRead Read(Stream input)
    {
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        buffer.Position = 0;

        try
        {
            using var document = SpreadsheetDocument.Open(buffer, false);
            var workbookPart = document.WorkbookPart;
            if (workbookPart?.Workbook.Sheets is null)
                return new WorkbookRead { Error = "The workbook has no worksheets." };

            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable
                .Elements<SharedStringItem>()
                .Select(item => item.InnerText)
                .ToList() ?? [];

            var dateFormats = ReadDateFormats(workbookPart);
            var date1904 = workbookPart.Workbook.WorkbookProperties?.Date1904?.Value == true;
            var result = new WorkbookRead();

            foreach (var sheet in workbookPart.Workbook.Sheets.Elements<Sheet>())
            {
                var name = sheet.Name?.Value ?? "";
                if (workbookPart.GetPartById(sheet.Id!) is not WorksheetPart worksheetPart)
                {
                    result.Worksheets.Add(new WorksheetRead { Name = name, Error = $"'{name}' is not a worksheet." });
                    continue;
                }

                result.Worksheets.Add(ReadWorksheet(name, worksheetPart, sharedStrings, dateFormats, date1904));
            }

            if (result.Worksheets.Count == 0)
                result.Error = "The workbook has no worksheets.";

            return result;
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException or FileFormatException)
        {
            return new WorkbookRead { Error = "The file is not a readable .xlsx workbook. It may be corrupt or password protected." };
        }
    }

    private static WorksheetRead ReadWorksheet(
        string name,
        WorksheetPart worksheetPart,
        IReadOnlyList<string> sharedStrings,
        HashSet<uint> dateFormats,
        bool date1904)
    {
        var cells = new Dictionary<(int Row, int Col), ExcelCell>();
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
        if (sheetData is null)
            return new WorksheetRead { Name = name, Error = "The worksheet has no header row." };

        foreach (var row in sheetData.Elements<Row>())
        {
            var rowIndex = checked((int)(row.RowIndex?.Value ?? 0));
            if (rowIndex <= 0)
                continue;

            foreach (var cell in row.Elements<Cell>())
            {
                if (string.IsNullOrWhiteSpace(cell.CellReference?.Value))
                    continue;

                var (cellRow, cellCol) = ParseCell(cell.CellReference.Value);
                if (cellRow != rowIndex)
                    continue;

                var style = cell.StyleIndex?.Value ?? 0;
                var isDate = dateFormats.Contains(style);
                cells[(cellRow, cellCol)] = ReadCell(cell, sharedStrings, isDate, date1904);
            }
        }

        var headerColumns = new List<(int Index, string Header, string Key)>();
        var headerKeys = new Dictionary<string, int>(StringComparer.Ordinal);
        var maxCol = cells.Count == 0 ? 0 : cells.Keys.Max(k => k.Col);

        for (var col = 1; col <= maxCol; col++)
        {
            cells.TryGetValue((1, col), out var headerCell);
            if (headerCell is null || headerCell.Kind == ExcelValueKind.Blank)
                continue;
            if (headerCell.Kind == ExcelValueKind.Error)
                return new WorksheetRead { Name = name, Error = $"Header cell {ColumnLetter(col)}1 contains an Excel error." };

            var label = headerCell.Display;
            var key = label.Trim().ToLowerInvariant();
            if (key.Length == 0)
                continue;
            if (!headerKeys.TryAdd(key, col))
            {
                return new WorksheetRead
                {
                    Name = name,
                    Error = $"Header '{label.Trim()}' is duplicated in columns {ColumnLetter(headerKeys[key])} and {ColumnLetter(col)}. Headers must be unique after trimming, ignoring case."
                };
            }

            headerColumns.Add((col, label, key));
        }

        if (headerColumns.Count == 0)
            return new WorksheetRead { Name = name, Error = "Row 1 has no header values. The header row must be row 1." };

        var headerSet = headerColumns.Select(c => c.Index).ToHashSet();
        var orphanColumns = cells
            .Where(pair => pair.Key.Row >= 2 && pair.Key.Col > 0 && !headerSet.Contains(pair.Key.Col) && !pair.Value.IsEmpty)
            .Select(pair => pair.Key.Col)
            .Distinct()
            .Order()
            .Take(8)
            .Select(ColumnLetter)
            .ToList();
        if (orphanColumns.Count > 0)
        {
            return new WorksheetRead
            {
                Name = name,
                Error = $"Column {(orphanColumns.Count == 1 ? orphanColumns[0] : string.Join(", ", orphanColumns))} has values but row 1 has no header."
            };
        }

        var lastDataRow = 1;
        foreach (var pair in cells)
        {
            if (pair.Key.Row >= 2 && headerSet.Contains(pair.Key.Col) && !pair.Value.IsEmpty && pair.Key.Row > lastDataRow)
                lastDataRow = pair.Key.Row;
        }

        if (lastDataRow < 2)
            return new WorksheetRead { Name = name, Error = "The worksheet has a header row but no data rows." };

        var merges = worksheetPart.Worksheet.Elements<MergeCells>()
            .SelectMany(collection => collection.Elements<MergeCell>())
            .Select(merge => merge.Reference?.Value)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .ToList();

        var minCol = headerColumns.Min(c => c.Index);
        var maxHeaderCol = headerColumns.Max(c => c.Index);
        foreach (var reference in merges)
        {
            var parts = reference!.Split(':');
            if (parts.Length != 2)
                continue;
            var start = ParseCell(parts[0]);
            var end = ParseCell(parts[1]);
            var overlaps = start.Row <= lastDataRow && end.Row >= 1 && start.Col <= maxHeaderCol && end.Col >= minCol
                && RangeHits(start, end, headerSet, lastDataRow);
            if (overlaps)
            {
                return new WorksheetRead
                {
                    Name = name,
                    Error = $"Merged cells {reference} overlap the dataset and cannot be read unambiguously."
                };
            }
        }

        var columns = new List<SourceColumn>();
        foreach (var header in headerColumns)
        {
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            var samples = new List<SampleValue>();
            for (var row = 2; row <= lastDataRow; row++)
            {
                cells.TryGetValue((row, header.Index), out var value);
                value ??= ExcelCell.Blank;
                if (value.IsEmpty)
                    continue;
                kinds.Add(value.Kind.ToString());
                if (samples.Count < 3)
                    samples.Add(new SampleValue { Row = row, Display = value.Display });
            }

            columns.Add(new SourceColumn
            {
                Key = header.Key,
                Header = header.Header,
                Letter = ColumnLetter(header.Index),
                Index = header.Index,
                Kinds = kinds.Order().ToList(),
                Samples = samples
            });
        }

        var rows = new List<ExcelDataRow>();
        for (var rowNumber = 2; rowNumber <= lastDataRow; rowNumber++)
        {
            var data = new ExcelDataRow { RowNumber = rowNumber };
            var any = false;
            foreach (var header in headerColumns)
            {
                cells.TryGetValue((rowNumber, header.Index), out var value);
                value ??= ExcelCell.Blank;
                data.Cells[header.Key] = value;
                if (!value.IsEmpty)
                    any = true;
            }

            if (any)
                rows.Add(data);
        }

        if (rows.Count == 0)
            return new WorksheetRead { Name = name, Error = "The worksheet has a header row but no data rows." };

        return new WorksheetRead
        {
            Name = name,
            Dataset = new ExcelDataset
            {
                Worksheet = name,
                Columns = columns,
                Rows = rows
            }
        };
    }

    private static bool RangeHits((int Row, int Col) start, (int Row, int Col) end, HashSet<int> headerColumns, int lastDataRow)
    {
        var rowFrom = Math.Max(1, Math.Min(start.Row, end.Row));
        var rowTo = Math.Min(lastDataRow, Math.Max(start.Row, end.Row));
        var colFrom = Math.Min(start.Col, end.Col);
        var colTo = Math.Max(start.Col, end.Col);
        if (rowFrom > rowTo)
            return false;

        for (var col = colFrom; col <= colTo; col++)
        {
            if (headerColumns.Contains(col))
                return true;
        }

        return false;
    }

    private static ExcelCell ReadCell(Cell cell, IReadOnlyList<string> sharedStrings, bool dateFormat, bool date1904)
    {
        var type = cell.DataType?.Value;
        if (type == CellValues.Error)
            return new ExcelCell { Kind = ExcelValueKind.Error, Display = cell.CellValue?.Text ?? "#ERR", Error = "The cell contains an Excel error." };

        if (type == CellValues.InlineString)
        {
            var text = cell.InlineString?.InnerText ?? "";
            return TextCell(text);
        }

        if (type == CellValues.SharedString)
        {
            if (!int.TryParse(cell.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                || index < 0 || index >= sharedStrings.Count)
            {
                return new ExcelCell { Kind = ExcelValueKind.Error, Display = "#ERR", Error = "The cell references a missing shared string." };
            }

            return TextCell(sharedStrings[index]);
        }

        if (type == CellValues.Boolean)
        {
            var truth = cell.CellValue?.Text == "1";
            return new ExcelCell { Kind = ExcelValueKind.Boolean, Boolean = truth, Display = truth ? "TRUE" : "FALSE" };
        }

        if (type == CellValues.Date)
        {
            var raw = cell.CellValue?.Text ?? "";
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
                return new ExcelCell { Kind = ExcelValueKind.DateTime, DateTime = parsed, Display = FormatDate(parsed) };
            }

            return new ExcelCell { Kind = ExcelValueKind.Error, Display = raw, Error = "The cell date could not be read." };
        }

        if (type is null || type == CellValues.Number || type == CellValues.String)
        {
            var raw = type == CellValues.String
                ? cell.CellValue?.Text ?? cell.InnerText ?? ""
                : cell.CellValue?.Text ?? "";

            if (type == CellValues.String)
                return TextCell(raw);

            if (raw.Length == 0)
                return ExcelCell.Blank;

            if (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return TextCell(raw);

            if (dateFormat)
            {
                try
                {
                    var serial = date1904 ? (double)number + 1462d : (double)number;
                    var date = DateTime.SpecifyKind(DateTime.FromOADate(serial), DateTimeKind.Unspecified);
                    return new ExcelCell { Kind = ExcelValueKind.DateTime, DateTime = date, Number = number, Display = FormatDate(date) };
                }
                catch (ArgumentException)
                {
                    return new ExcelCell { Kind = ExcelValueKind.Error, Display = raw, Error = $"'{raw}' is marked as a date but is outside the Excel date range." };
                }
            }

            return new ExcelCell { Kind = ExcelValueKind.Number, Number = number, Display = raw };
        }

        var fallback = cell.CellValue?.Text ?? cell.InnerText ?? "";
        return fallback.Length == 0 ? ExcelCell.Blank : TextCell(fallback);
    }

    private static ExcelCell TextCell(string text) =>
        new() { Kind = ExcelValueKind.Text, Text = text, Display = text };

    public static string FormatDate(DateTime value) =>
        value.TimeOfDay == TimeSpan.Zero
            ? value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static string FormatValue(object? value) => value switch
    {
        null => "NULL",
        string text when text.Length == 0 => "\"\"",
        string text => text,
        bool flag => flag ? "True" : "False",
        DateTime date => FormatDate(date),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        int number => number.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };

    private static HashSet<uint> ReadDateFormats(WorkbookPart workbookPart)
    {
        var formats = new HashSet<uint>();
        var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet;
        if (stylesheet?.CellFormats is null)
            return formats;

        var custom = new Dictionary<uint, string>();
        if (stylesheet.NumberingFormats is not null)
        {
            foreach (var format in stylesheet.NumberingFormats.Elements<NumberingFormat>())
            {
                if (format.NumberFormatId?.Value is uint id && format.FormatCode?.Value is string code)
                    custom[id] = code;
            }
        }

        uint index = 0;
        foreach (var cellFormat in stylesheet.CellFormats.Elements<CellFormat>())
        {
            var formatId = cellFormat.NumberFormatId?.Value ?? 0;
            var code = custom.TryGetValue(formatId, out var customCode) ? customCode : null;
            if (BuiltInDateFormatIds.Contains(formatId) || (code is not null && IsDateFormat(code)))
                formats.Add(index);
            index++;
        }

        return formats;
    }

    private static bool IsDateFormat(string format)
    {
        var stripped = new StringBuilder();
        for (var i = 0; i < format.Length; i++)
        {
            var ch = format[i];
            if (ch == '"')
            {
                i++;
                while (i < format.Length && format[i] != '"')
                    i++;
                continue;
            }

            if (ch == '\\' && i + 1 < format.Length)
            {
                i++;
                continue;
            }

            if (ch == '[')
            {
                while (i < format.Length && format[i] != ']')
                    i++;
                continue;
            }

            stripped.Append(ch);
        }

        var text = AmPmPattern().Replace(stripped.ToString(), "");
        return DateTokenPattern().IsMatch(text);
    }

    private static (int Row, int Col) ParseCell(string reference)
    {
        var column = 0;
        var i = 0;
        while (i < reference.Length && char.IsLetter(reference[i]))
        {
            column = column * 26 + (char.ToUpperInvariant(reference[i]) - 'A' + 1);
            i++;
        }

        var row = int.Parse(reference[i..], CultureInfo.InvariantCulture);
        return (row, column);
    }

    public static string ColumnLetter(int column)
    {
        var letters = "";
        while (column > 0)
        {
            column--;
            letters = (char)('A' + column % 26) + letters;
            column /= 26;
        }

        return letters;
    }

    [GeneratedRegex("am/pm|a/p", RegexOptions.IgnoreCase)]
    private static partial Regex AmPmPattern();

    [GeneratedRegex("[dymhs]", RegexOptions.IgnoreCase)]
    private static partial Regex DateTokenPattern();
}
