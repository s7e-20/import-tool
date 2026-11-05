namespace AnnuityImport.Server.Mapping;

public sealed class MappingDocument
{
    public string Format { get; set; } = "annuity-import-mapping";
    public int Version { get; set; } = 1;
    public string? Worksheet { get; set; }
    public List<string> SourceHeaders { get; set; } = [];
    public List<AssignmentSpec> Assignments { get; set; } = [];
}

public sealed class AssignmentSpec
{
    public string Target { get; set; } = "";
    public string Mode { get; set; } = "unassigned";
    public string? SourceHeader { get; set; }
    public string? Constant { get; set; }
    public string? DateFormat { get; set; }
    public string? DecimalSeparator { get; set; }
    public string? ThousandsSeparator { get; set; }
    public bool ThousandsSeparatorNone { get; set; }
}

public sealed class RowIssue
{
    public int? Row { get; init; }
    public string? Target { get; init; }
    public string Message { get; init; } = "";
}

public sealed class SampleResult
{
    public int Row { get; init; }
    public string Source { get; init; } = "";
    public string? Result { get; init; }
    public string? Error { get; init; }
}

public sealed class TargetAssessment
{
    public string Status { get; init; } = "unassigned";
    public bool RequiresDateFormat { get; init; }
    public bool RequiresNumberFormat { get; init; }
    public string? Message { get; init; }
    public List<string> ObservedKinds { get; init; } = [];
    public List<SampleResult> Samples { get; init; } = [];
}

public sealed class AssessResult
{
    public List<string> DefinitionErrors { get; init; } = [];
    public Dictionary<string, TargetAssessment> Targets { get; init; } = new(StringComparer.Ordinal);
}

public sealed class BuildResult
{
    public bool Ok { get; set; }
    public int DataRows { get; init; }
    public int ReadyRows { get; set; }
    public List<string> DefinitionErrors { get; init; } = [];
    public List<RowIssue> Errors { get; init; } = [];
    public List<Data.CaseTransfert> Rows { get; init; } = [];
    public string? Worksheet { get; init; }
}
