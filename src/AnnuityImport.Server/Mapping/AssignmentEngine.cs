using System.Globalization;
using AnnuityImport.Server.Data;
using AnnuityImport.Server.Excel;

namespace AnnuityImport.Server.Mapping;

public static class AssignmentEngine
{
    private static readonly Dictionary<string, bool> BooleanTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0"] = false,
        ["1"] = true,
        ["-1"] = true,
        ["no"] = false,
        ["yes"] = true,
        ["false"] = false,
        ["true"] = true,
        ["n"] = false,
        ["y"] = true,
        ["x"] = true
    };

    public static AssessResult Assess(ExcelDataset dataset, MappingDocument mapping)
    {
        var prepared = Prepare(dataset, mapping);
        var targets = new Dictionary<string, TargetAssessment>(StringComparer.Ordinal);
        foreach (var plan in prepared.Plans)
            targets[plan.Field.Column] = BuildAssessment(dataset, plan);

        return new AssessResult
        {
            DefinitionErrors = prepared.DefinitionErrors,
            Targets = targets
        };
    }

    public static BuildResult Build(ExcelDataset dataset, MappingDocument mapping, string uploadedFileName, DateTime importDate)
    {
        var prepared = Prepare(dataset, mapping);
        var result = new BuildResult
        {
            DataRows = dataset.Rows.Count,
            Worksheet = dataset.Worksheet,
            DefinitionErrors = prepared.DefinitionErrors
        };

        if (uploadedFileName.Length > 255)
            result.DefinitionErrors.Add("The uploaded file name is longer than SOURCE_FILE allows (255 characters).");

        foreach (var plan in prepared.Plans.Where(plan => plan.Mode == AssignmentMode.Constant))
        {
            var converted = ConvertConstant(plan.Field, plan.Constant ?? "");
            if (!converted.Ok)
                result.DefinitionErrors.Add($"{plan.Field.Title}: {converted.Error}");
        }

        if (result.DefinitionErrors.Count > 0)
            return result;

        var errors = new List<RowIssue>();
        var entities = new List<CaseTransfert>();
        foreach (var dataRow in dataset.Rows)
        {
            var entity = new CaseTransfert();
            TargetCatalogue.ApplyHidden(entity, uploadedFileName, importDate);
            var rowFailed = false;
            foreach (var plan in prepared.Plans)
            {
                if (plan.Mode == AssignmentMode.Unassigned)
                    continue;

                var converted = ConvertPlan(plan, dataRow);
                if (!converted.Ok)
                {
                    rowFailed = true;
                    if (errors.Count < 500)
                    {
                        errors.Add(new RowIssue
                        {
                            Row = dataRow.RowNumber,
                            Target = plan.Field.Title,
                            Message = converted.Error ?? "The value could not be stored."
                        });
                    }
                    continue;
                }

                TargetCatalogue.SetDisplayed(entity, plan.Field.Column, converted.Value);
            }

            if (!rowFailed)
                entities.Add(entity);
        }

        result.Errors.AddRange(errors);
        result.ReadyRows = entities.Count;
        result.Rows.AddRange(entities);
        result.Ok = errors.Count == 0;
        return result;
    }

    public static string? SourceMismatch(ExcelDataset dataset, MappingDocument mapping)
    {
        if (mapping.SourceHeaders.Count == 0)
            return null;

        var fileKeys = dataset.Columns.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
        var missing = mapping.SourceHeaders
            .Where(header => !fileKeys.Contains(HeaderKey(header)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missing.Count == 0)
            return null;

        return "Source fields have changed. Missing: "
            + string.Join(", ", missing)
            + ". Create a new mapping from the Excel file. This load does not change the saved mapping.";
    }

    public static Dictionary<string, string> Suggest(ExcelDataset dataset)
    {
        var suggestions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in TargetCatalogue.Displayed)
        {
            var wanted = new HashSet<string>(StringComparer.Ordinal)
            {
                Normalize(field.Title),
                Normalize(field.Column)
            };
            var matches = dataset.Columns.Where(column => wanted.Contains(Normalize(column.Header))).ToList();
            if (matches.Count == 1)
                suggestions[field.Column] = matches[0].Header;
        }

        return suggestions;
    }

    private static TargetAssessment BuildAssessment(ExcelDataset dataset, FieldPlan plan)
    {
        var samples = new List<SampleResult>();
        if (plan.Mode == AssignmentMode.Source && plan.Source is not null && plan.Status is not "incompatible")
        {
            var taken = 0;
            var errors = 0;
            foreach (var row in dataset.Rows)
            {
                row.Cells.TryGetValue(plan.Source.Key, out var cell);
                cell ??= ExcelCell.Blank;
                if (cell.IsEmpty && plan.Field.Kind != TargetKind.Boolean && plan.Field.Column != "Record Type")
                    continue;

                var converted = ConvertPlan(plan, row);
                var interesting = !cell.IsEmpty || !converted.Ok;
                if (!interesting)
                    continue;

                if (converted.Ok && taken >= 5)
                    continue;

                if (!converted.Ok)
                    errors++;

                if (samples.Count < 5 || !converted.Ok && errors <= 3)
                {
                    samples.Add(new SampleResult
                    {
                        Row = row.RowNumber,
                        Source = cell.Display,
                        Result = converted.Ok ? ExcelDatasetReader.FormatValue(converted.Value) : null,
                        Error = converted.Error
                    });
                }

                if (converted.Ok)
                    taken++;
                if (samples.Count >= 8)
                    break;
            }
        }

        string? suggestedDate = null;
        string? suggestedPattern = null;
        string? suggestedDecimal = null;
        string? suggestedThousands = null;
        var suggestedThousandsNone = false;
        if (plan.Source is not null && plan.Status is not "incompatible")
        {
            var texts = TextValues(dataset, plan.Source);
            if (plan.RequiresDateFormat)
                suggestedDate = SuggestDateFormat(texts);
            if (plan.RequiresNumberFormat && SuggestNumberFormat(texts) is { } number)
            {
                suggestedPattern = number.Pattern;
                suggestedDecimal = number.Decimal.ToString();
                suggestedThousands = number.Thousands?.ToString();
                suggestedThousandsNone = number.Thousands is null;
            }
        }

        var message = plan.Message;
        if (plan.Status == "needs-date-format" && suggestedDate is not null)
            message = $"{message} Suggested: {suggestedDate}.";
        if (plan.Status == "needs-number-format" && suggestedPattern is not null)
            message = $"{message} Suggested: {suggestedPattern}.";

        return new TargetAssessment
        {
            Status = plan.Status,
            RequiresDateFormat = plan.RequiresDateFormat,
            RequiresNumberFormat = plan.RequiresNumberFormat,
            Message = message,
            SuggestedDateFormat = suggestedDate,
            SuggestedNumberPattern = suggestedPattern,
            SuggestedDecimalSeparator = suggestedDecimal,
            SuggestedThousandsSeparator = suggestedThousands,
            SuggestedThousandsNone = suggestedThousandsNone,
            ObservedKinds = plan.Source?.Kinds ?? [],
            Samples = samples
        };
    }

    private static List<string> TextValues(ExcelDataset dataset, SourceColumn column)
    {
        var values = new List<string>();
        foreach (var row in dataset.Rows)
        {
            if (!row.Cells.TryGetValue(column.Key, out var cell) || cell.Kind != ExcelValueKind.Text || cell.IsEmpty)
                continue;
            values.Add(cell.Text ?? "");
        }

        return values;
    }

    private static readonly string[] DateFormatCandidates =
    [
        "dd.MM.yyyy",
        "d.M.yyyy",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd-MM-yyyy",
        "d-M-yyyy",
        "yyyy-MM-dd",
        "yyyy-M-d",
        "yyyy.MM.dd",
        "yyyy/MM/dd",
        "dd.MM.yy",
        "dd/MM/yy",
        "dd.MM.yyyy HH:mm:ss",
        "dd/MM/yyyy HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "dd.MM.yyyy HH:mm",
        "dd/MM/yyyy HH:mm",
        "yyyy-MM-dd HH:mm",
        "MM/dd/yyyy",
        "M/d/yyyy",
        "MM-dd-yyyy",
        "MM.dd.yyyy"
    ];

    private static readonly (string DayFirst, string MonthFirst)[] AmbiguousDatePairs =
    [
        ("dd.MM.yyyy", "MM.dd.yyyy"),
        ("d.M.yyyy", "M.d.yyyy"),
        ("dd/MM/yyyy", "MM/dd/yyyy"),
        ("d/M/yyyy", "M/d/yyyy"),
        ("dd-MM-yyyy", "MM-dd-yyyy"),
        ("dd/MM/yy", "MM/dd/yy")
    ];

    private static string? SuggestDateFormat(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return null;

        var matches = DateFormatCandidates.Where(format => values.All(value => DateRoundTrips(value, format))).ToList();
        foreach (var (dayFirst, monthFirst) in AmbiguousDatePairs)
        {
            if (matches.Contains(dayFirst) && matches.Contains(monthFirst))
            {
                matches.Remove(dayFirst);
                matches.Remove(monthFirst);
            }
        }

        return matches.FirstOrDefault();
    }

    private static bool DateRoundTrips(string text, string format)
    {
        if (!DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return false;
        return parsed.ToString(format, CultureInfo.InvariantCulture) == text;
    }

    private readonly record struct NumberSuggestion(string Pattern, char Decimal, char? Thousands);

    private static NumberSuggestion? SuggestNumberFormat(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return null;

        char? decidedDecimal = null;
        char? decidedThousands = null;
        char? ambiguousMark = null;
        var sawFraction = false;
        foreach (var value in values)
        {
            if (!TryReadNumber(value, out var shape, out var decimalMark, out var thousandMark))
                return null;

            switch (shape)
            {
                case NumberShape.Integer:
                    break;
                case NumberShape.Ambiguous:
                    if (ambiguousMark is char seen && seen != decimalMark)
                        return null;
                    ambiguousMark = decimalMark;
                    break;
                case NumberShape.Decimal:
                    sawFraction = true;
                    if (!Agree(ref decidedDecimal, decimalMark))
                        return null;
                    if (thousandMark is char thousand && !Agree(ref decidedThousands, thousand))
                        return null;
                    break;
                case NumberShape.ThousandsOnly:
                    if (thousandMark is not char group || !Agree(ref decidedThousands, group))
                        return null;
                    break;
            }
        }

        if (ambiguousMark is char mark)
        {
            var resolved = decidedDecimal == mark || decidedThousands == mark;
            if (!resolved)
                return null;
        }

        if (decidedDecimal is null && decidedThousands is char grouped)
        {
            decidedDecimal = grouped switch
            {
                '.' => ',',
                ',' => '.',
                ' ' => ',',
                '\'' => '.',
                _ => null
            };
        }

        if (decidedDecimal is null)
            decidedDecimal = '.';
        if (decidedDecimal == decidedThousands)
            return null;

        return new NumberSuggestion(
            NumberPattern(decidedDecimal.Value, decidedThousands, sawFraction),
            decidedDecimal.Value,
            decidedThousands);
    }

    private static string NumberPattern(char decimalSeparator, char? thousands, bool hasFraction)
    {
        var fraction = hasFraction ? $"{decimalSeparator}##" : "";
        if (thousands is not char group)
            return "#" + fraction;

        var grouped = group switch
        {
            ' ' => "# ###",
            '\'' => "#'###",
            _ => $"#{group}###"
        };
        return grouped + fraction;
    }

    private enum NumberShape
    {
        Integer,
        Decimal,
        ThousandsOnly,
        Ambiguous
    }

    private static bool TryReadNumber(string text, out NumberShape shape, out char decimalMark, out char? thousandMark)
    {
        shape = NumberShape.Integer;
        decimalMark = '.';
        thousandMark = null;
        if (text.Length == 0)
            return false;

        var body = text;
        if (body[0] is '+' or '-')
        {
            if (body.Length == 1)
                return false;
            body = body[1..];
        }

        var marks = new List<char>();
        foreach (var character in body)
        {
            if (char.IsDigit(character))
                continue;
            if (character is not ('.' or ',' or '\'' or ' '))
                return false;
            if (!marks.Contains(character))
                marks.Add(character);
        }

        if (!body.Any(char.IsDigit))
            return false;

        if (marks.Count == 0)
            return true;

        if (marks.Count > 2)
            return false;

        if (marks.Count == 2)
        {
            var last = body.Last(character => !char.IsDigit(character));
            if (last is not ('.' or ','))
                return false;
            var thousand = marks.First(character => character != last);
            var split = body.LastIndexOf(last);
            if (!IsDigits(body[(split + 1)..]) || !ValidGrouped(body[..split], thousand))
                return false;
            shape = NumberShape.Decimal;
            decimalMark = last;
            thousandMark = thousand;
            return true;
        }

        var mark = marks[0];
        var count = body.Count(character => character == mark);
        if (mark is '\'' or ' ' || count > 1)
        {
            if (!ValidGrouped(body, mark))
                return false;
            shape = NumberShape.ThousandsOnly;
            thousandMark = mark;
            return true;
        }

        var index = body.IndexOf(mark);
        var left = body[..index];
        var right = body[(index + 1)..];
        if (!IsDigits(left) || !IsDigits(right))
            return false;
        decimalMark = mark;
        shape = right.Length == 3 ? NumberShape.Ambiguous : NumberShape.Decimal;
        return true;
    }

    private static bool ValidGrouped(string integerPart, char thousands)
    {
        var groups = integerPart.Split(thousands);
        if (groups.Length < 2 || groups[0].Length is < 1 or > 3 || !IsDigits(groups[0]))
            return false;
        for (var index = 1; index < groups.Length; index++)
        {
            if (groups[index].Length != 3 || !IsDigits(groups[index]))
                return false;
        }

        return true;
    }

    private static bool IsDigits(string text) => text.Length > 0 && text.All(char.IsDigit);

    private static bool Agree(ref char? current, char next)
    {
        if (current is char existing && existing != next)
            return false;
        current = next;
        return true;
    }

    private static PreparedMapping Prepare(ExcelDataset dataset, MappingDocument mapping)
    {
        var prepared = new PreparedMapping();
        var columns = dataset.Columns.ToDictionary(column => column.Key, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var byTarget = new Dictionary<string, AssignmentSpec>(StringComparer.Ordinal);

        foreach (var assignment in mapping.Assignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.Target))
            {
                prepared.DefinitionErrors.Add("A mapping entry has no target field.");
                continue;
            }

            if (!seen.Add(assignment.Target))
            {
                prepared.DefinitionErrors.Add($"'{assignment.Target}' is mapped more than once.");
                continue;
            }

            if (TargetCatalogue.HiddenColumns.Contains(assignment.Target))
            {
                prepared.DefinitionErrors.Add($"'{assignment.Target}' is filled automatically and cannot be mapped.");
                continue;
            }

            if (!TargetCatalogue.ByColumn.ContainsKey(assignment.Target))
            {
                prepared.DefinitionErrors.Add($"'{assignment.Target}' is not a target field.");
                continue;
            }

            byTarget[assignment.Target] = assignment;
        }

        foreach (var field in TargetCatalogue.Displayed)
        {
            byTarget.TryGetValue(field.Column, out var assignment);
            prepared.Plans.Add(Compile(field, assignment, columns, prepared.DefinitionErrors));
        }

        return prepared;
    }

    private static FieldPlan Compile(
        FieldDef field,
        AssignmentSpec? assignment,
        Dictionary<string, SourceColumn> columns,
        List<string> definitionErrors)
    {
        var mode = ParseMode(assignment?.Mode);
        var plan = new FieldPlan { Field = field, Mode = mode ?? AssignmentMode.Unassigned };

        if (assignment is not null && mode is null)
        {
            plan.Status = "invalid";
            plan.Message = $"Assignment mode '{assignment.Mode}' is not supported.";
            definitionErrors.Add($"{field.Title}: {plan.Message}");
            return plan;
        }

        if (plan.Mode == AssignmentMode.Unassigned)
        {
            plan.Status = "unassigned";
            if (field.Mandatory)
            {
                plan.Status = "invalid";
                plan.Message = $"{field.Title} must be mapped to a source column or a constant.";
                definitionErrors.Add(plan.Message);
            }
            return plan;
        }

        if (plan.Mode == AssignmentMode.Constant)
        {
            plan.Status = "constant";
            if (assignment?.Constant is null)
            {
                plan.Status = "invalid";
                plan.Message = $"{field.Title} is a constant but has no value. Enter a value, or choose Unassigned to store NULL.";
                definitionErrors.Add(plan.Message);
                return plan;
            }

            plan.Constant = assignment.Constant;
            return plan;
        }

        var header = assignment?.SourceHeader;
        if (string.IsNullOrWhiteSpace(header))
        {
            plan.Status = "invalid";
            plan.Message = $"{field.Title} is set to Source but no source column is selected.";
            definitionErrors.Add(plan.Message);
            return plan;
        }

        if (!columns.TryGetValue(HeaderKey(header), out var column))
        {
            plan.Status = "invalid";
            plan.Message = $"Source field '{header}' is not in this worksheet.";
            definitionErrors.Add($"{field.Title}: {plan.Message}");
            return plan;
        }

        plan.Source = column;
        var analysis = Analyze(field, column);
        plan.RequiresDateFormat = analysis.RequiresDateFormat;
        plan.RequiresNumberFormat = analysis.RequiresNumberFormat;

        if (analysis.Incompatible)
        {
            plan.Status = "incompatible";
            plan.Message = $"{field.Title} cannot use '{column.Header}' ({string.Join(", ", column.Kinds)}). Choose another source column.";
            definitionErrors.Add(plan.Message);
            return plan;
        }

        if (analysis.RequiresDateFormat)
        {
            plan.DateFormat = assignment?.DateFormat?.Trim();
            if (string.IsNullOrWhiteSpace(plan.DateFormat))
            {
                plan.Status = "needs-date-format";
                plan.Message = "Enter the source date format. Examples: dd.MM.yyyy, yyyy-MM-dd, dd/MM/yyyy, dd.MM.yyyy HH:mm:ss.";
                definitionErrors.Add($"{field.Title} needs a source date format.");
                return plan;
            }
        }

        if (analysis.RequiresNumberFormat)
        {
            var separators = ReadSeparators(assignment, out var separatorError);
            if (separatorError is not null)
            {
                plan.Status = "needs-number-format";
                plan.Message = separatorError;
                definitionErrors.Add($"{field.Title}: {separatorError}");
                return plan;
            }

            plan.DecimalSeparator = separators.Decimal;
            plan.ThousandsSeparator = separators.Thousands;
            plan.ThousandsNone = separators.ThousandsNone;
        }

        plan.Status = "compatible";
        return plan;
    }

    private static (bool RequiresDateFormat, bool RequiresNumberFormat, bool Incompatible) Analyze(FieldDef field, SourceColumn column)
    {
        var kinds = column.Kinds.Where(kind => kind != nameof(ExcelValueKind.Error)).ToHashSet(StringComparer.Ordinal);
        var hasText = kinds.Contains(nameof(ExcelValueKind.Text));
        var hasNumber = kinds.Contains(nameof(ExcelValueKind.Number));
        var hasDate = kinds.Contains(nameof(ExcelValueKind.DateTime));
        var hasBoolean = kinds.Contains(nameof(ExcelValueKind.Boolean));

        return field.Kind switch
        {
            TargetKind.Text => (false, false, hasDate || hasBoolean),
            TargetKind.DateTime => (hasText, false, hasNumber || hasBoolean),
            TargetKind.Integer or TargetKind.Decimal => (false, hasText, hasDate || hasBoolean),
            TargetKind.Boolean => (false, false, hasDate),
            _ => (false, false, true)
        };
    }

    private static Converted ConvertPlan(FieldPlan plan, ExcelDataRow row)
    {
        if (plan.Mode == AssignmentMode.Constant)
            return ConvertConstant(plan.Field, plan.Constant ?? "");

        row.Cells.TryGetValue(plan.Source!.Key, out var cell);
        cell ??= ExcelCell.Blank;
        return ConvertCell(plan, cell);
    }

    private static Converted ConvertCell(FieldPlan plan, ExcelCell cell)
    {
        if (cell.Kind == ExcelValueKind.Error)
            return Converted.Fail(cell.Error ?? "The cell contains an Excel error.");

        if (plan.Field.Column == "Record Type" && (cell.IsEmpty || cell.Kind == ExcelValueKind.Text && cell.Text is ""))
            return Converted.Fail("Record type cannot be empty.");

        if (cell.IsEmpty && plan.Field.Kind == TargetKind.Boolean)
            return Converted.Success(false);

        if (cell.IsEmpty)
            return Converted.Success(null);

        try
        {
            return plan.Field.Kind switch
            {
                TargetKind.Text when cell.Kind == ExcelValueKind.Text => TextValue(plan.Field, cell.Text ?? ""),
                TargetKind.Text when cell.Kind == ExcelValueKind.Number => TextValue(plan.Field, PlainNumber(cell.Number!.Value)),
                TargetKind.DateTime when cell.Kind == ExcelValueKind.DateTime => Converted.Success(cell.DateTime),
                TargetKind.DateTime when cell.Kind == ExcelValueKind.Text => ParseDate(cell.Text ?? "", plan.DateFormat),
                TargetKind.Integer when cell.Kind == ExcelValueKind.Number => ToInteger(cell.Number!.Value, cell.Display),
                TargetKind.Decimal when cell.Kind == ExcelValueKind.Number => Converted.Success(cell.Number),
                TargetKind.Integer when cell.Kind == ExcelValueKind.Text => ParseNumber(cell.Text ?? "", plan, integer: true),
                TargetKind.Decimal when cell.Kind == ExcelValueKind.Text => ParseNumber(cell.Text ?? "", plan, integer: false),
                TargetKind.Boolean when cell.Kind == ExcelValueKind.Boolean => Converted.Success(cell.Boolean),
                TargetKind.Boolean when cell.Kind == ExcelValueKind.Number => ParseBooleanNumber(cell.Number!.Value, cell.Display),
                TargetKind.Boolean when cell.Kind == ExcelValueKind.Text => ParseBooleanText(cell.Text ?? ""),
                _ => Converted.Fail($"A {cell.Kind.ToString().ToLowerInvariant()} value cannot be stored in {plan.Field.Title}.")
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return Converted.Fail(ex.Message);
        }
    }

    private static string PlainNumber(decimal number) =>
        number.ToString("0.#############################", CultureInfo.InvariantCulture);

    private static Converted TextValue(FieldDef field, string value)
    {
        var max = MaxCharacters(field.SqlType);
        if (max is int limit && value.Length > limit)
        {
            var shown = value.Length <= 40 ? $"'{value}'" : $"'{value[..40]}…'";
            return Converted.Fail($"{shown} is {value.Length} characters. {field.SqlType} allows {limit}.");
        }

        return Converted.Success(value);
    }

    private static int? MaxCharacters(string sqlType)
    {
        var open = sqlType.IndexOf('(');
        var close = sqlType.IndexOf(')', open + 1);
        if (open < 0 || close < 0)
            return null;

        var kind = sqlType[..open].Trim();
        if (!kind.Equals("varchar", StringComparison.OrdinalIgnoreCase) &&
            !kind.Equals("nvarchar", StringComparison.OrdinalIgnoreCase))
            return null;

        var digits = sqlType[(open + 1)..close].Trim();
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) ? limit : null;
    }

    private static Converted ConvertConstant(FieldDef field, string value)
    {
        if (field.Kind == TargetKind.Text)
            return TextValue(field, value);

        if (field.Kind == TargetKind.Boolean)
        {
            if (bool.TryParse(value, out var flag))
                return Converted.Success(flag);
            if (value == "1")
                return Converted.Success(true);
            if (value == "0")
                return Converted.Success(false);
            return Converted.Fail($"Constant '{value}' cannot be stored as a boolean. Use true, false, 1, or 0.");
        }

        if (field.Kind == TargetKind.Integer)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                return Converted.Success(number);
            return Converted.Fail($"Constant '{value}' cannot be stored as an integer.");
        }

        if (field.Kind == TargetKind.Decimal)
        {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                return Converted.Success(number);
            return Converted.Fail($"Constant '{value}' cannot be stored as a decimal.");
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out var parsed))
            return Converted.Success(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified));

        return Converted.Fail($"Constant '{value}' cannot be stored as a date. Use an invariant date such as 2024-01-31.");
    }

    private static Converted ParseDate(string text, string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return Converted.Fail("A source date format is required.");

        try
        {
            if (!DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return Converted.Fail($"'{text}' does not match the date format {format}.");
            return Converted.Success(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified));
        }
        catch (FormatException)
        {
            return Converted.Fail($"'{format}' is not a valid .NET date format.");
        }
    }

    private static Converted ParseNumber(string text, FieldPlan plan, bool integer)
    {
        if (text.Length == 0)
            return Converted.Success(null);

        if (plan.DecimalSeparator is not char decimalSeparator)
            return Converted.Fail("Choose the decimal separator and the thousands separator, or None.");

        if (!TryParseLooseNumber(text, decimalSeparator, plan.ThousandsNone ? null : plan.ThousandsSeparator, out var number, out var error))
            return Converted.Fail(error ?? $"'{text}' is not a number with the selected separators.");

        return integer ? ToInteger(number, text) : Converted.Success(number);
    }

    private static Converted ToInteger(decimal number, string display)
    {
        if (number != decimal.Truncate(number) || number is < int.MinValue or > int.MaxValue)
            return Converted.Fail($"'{display}' cannot be stored as an integer.");
        return Converted.Success((int)number);
    }

    private static Converted ParseBooleanNumber(decimal number, string display)
    {
        if (number == 0)
            return Converted.Success(false);
        if (number is 1 or -1)
            return Converted.Success(true);
        return Converted.Fail($"'{display}' is not a recognized boolean value. Use 0, 1, or -1.");
    }

    private static Converted ParseBooleanText(string text)
    {
        var token = text.Trim();
        if (token.Length == 0)
            return Converted.Success(false);
        if (BooleanTokens.TryGetValue(token, out var value))
            return Converted.Success(value);
        return Converted.Fail($"'{text}' is not a recognized boolean value.");
    }

    public static bool TryParseLooseNumber(string text, char decimalSeparator, char? thousands, out decimal value, out string? error)
    {
        value = 0;
        error = null;
        if (thousands is char thousand && thousand == decimalSeparator)
        {
            error = "The decimal separator and the thousands separator must be different.";
            return false;
        }

        if (char.IsDigit(decimalSeparator) || thousands is char digit && char.IsDigit(digit))
        {
            error = "A separator cannot be a digit.";
            return false;
        }

        var body = thousands is char mark ? text.Replace(mark.ToString(), "") : text;
        if (body.Count(ch => ch == decimalSeparator) > 1)
        {
            error = $"'{text}' has more than one decimal separator.";
            return false;
        }

        if (decimalSeparator != '.')
        {
            if (body.Contains('.'))
            {
                error = $"'{text}' contains a '.' that is not the selected separator.";
                return false;
            }

            body = body.Replace(decimalSeparator, '.');
        }

        if (!decimal.TryParse(body, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
        {
            error = $"'{text}' is not a number with the selected separators.";
            return false;
        }

        return true;
    }

    private static (char Decimal, char? Thousands, bool ThousandsNone) ReadSeparators(AssignmentSpec? assignment, out string? error)
    {
        error = null;
        var decimalText = assignment?.DecimalSeparator;
        if (decimalText is not { Length: 1 })
        {
            error = "Enter one decimal separator character, such as . or ,.";
            return default;
        }

        char? thousands = null;
        var none = assignment?.ThousandsSeparatorNone == true;
        if (!none)
        {
            if (assignment?.ThousandsSeparator is not { Length: 1 })
            {
                error = "Enter one thousands separator, or choose None.";
                return default;
            }

            thousands = assignment.ThousandsSeparator[0];
        }

        if (thousands == decimalText[0])
        {
            error = "The decimal separator and the thousands separator must be different.";
            return default;
        }

        if (char.IsDigit(decimalText[0]) || thousands is char digit && char.IsDigit(digit))
        {
            error = "A separator cannot be a digit.";
            return default;
        }

        return (decimalText[0], thousands, none);
    }

    private static AssignmentMode? ParseMode(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        null or "" or "unassigned" => AssignmentMode.Unassigned,
        "source" => AssignmentMode.Source,
        "constant" => AssignmentMode.Constant,
        _ => null
    };

    public static string HeaderKey(string header) => header.Trim().ToLowerInvariant();

    private static string Normalize(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
        return new string(chars);
    }

    private enum AssignmentMode
    {
        Unassigned,
        Source,
        Constant
    }

    private sealed class PreparedMapping
    {
        public List<string> DefinitionErrors { get; } = [];
        public List<FieldPlan> Plans { get; } = [];
    }

    private sealed class FieldPlan
    {
        public required FieldDef Field { get; init; }
        public AssignmentMode Mode { get; init; }
        public SourceColumn? Source { get; set; }
        public string? Constant { get; set; }
        public string? DateFormat { get; set; }
        public char? DecimalSeparator { get; set; }
        public char? ThousandsSeparator { get; set; }
        public bool ThousandsNone { get; set; }
        public bool RequiresDateFormat { get; set; }
        public bool RequiresNumberFormat { get; set; }
        public string Status { get; set; } = "unassigned";
        public string? Message { get; set; }
    }

    private readonly struct Converted
    {
        public bool Ok { get; init; }
        public object? Value { get; init; }
        public string? Error { get; init; }
        public static Converted Success(object? value) => new() { Ok = true, Value = value };
        public static Converted Fail(string error) => new() { Ok = false, Error = error };
    }
}
