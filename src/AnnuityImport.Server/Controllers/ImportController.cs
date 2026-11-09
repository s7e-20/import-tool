using System.Text;
using NVGI.Projet.Importtool.Data;
using NVGI.Projet.Importtool.Mapping;
using NVGI.Projet.Importtool.Services;
using Microsoft.AspNetCore.Mvc;

namespace NVGI.Projet.Importtool.Controllers;

[ApiController]
[Route("api")]
public sealed class ImportController : ControllerBase
{
    private readonly UploadStore _uploads;
    private readonly ImportService _import;
    private readonly IConfiguration _configuration;

    public ImportController(UploadStore uploads, ImportService import, IConfiguration configuration)
    {
        _uploads = uploads;
        _import = import;
        _configuration = configuration;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        try
        {
            SchemaBootstrap.Ensure(_configuration);
            return Ok(new { database = "ok", table = "dbo.CASE_TRANSFERT" });
        }
        catch (Exception ex)
        {
            return Ok(new { database = "unavailable", table = "dbo.CASE_TRANSFERT", message = ex.Message });
        }
    }

    [HttpGet("catalogue")]
    public IActionResult Catalogue() => Ok(new
    {
        fields = TargetCatalogue.Displayed.Select((field, index) => new
        {
            column = field.Column,
            title = field.Title,
            sqlType = field.SqlType,
            kind = field.Kind.ToString().ToLowerInvariant(),
            mandatory = field.Mandatory,
            order = index + 1
        }),
        hiddenCount = TargetCatalogue.HiddenColumns.Count
    });

    [HttpPost("uploads")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Choose an .xlsx file." });
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .xlsx files are supported." });

        await using var stream = file.OpenReadStream();
        var workbook = _import.ReadWorkbook(stream);
        if (workbook.Error is not null)
            return BadRequest(new { message = workbook.Error });

        var session = _uploads.Save(file.FileName, workbook);
        return Ok(new
        {
            uploadId = session.Id,
            fileName = session.FileName,
            selectedWorksheet = session.SelectedWorksheet,
            needsWorksheet = session.SelectedWorksheet is null,
            worksheets = ImportService.SheetPayload(workbook)
        });
    }

    [HttpPost("uploads/{id}/worksheet")]
    public IActionResult SelectWorksheet(string id, [FromBody] WorksheetChoice choice)
    {
        var session = _uploads.Get(id);
        if (session is null)
            return NotFound(new { message = "That upload has expired. Choose the Excel file again." });
        if (string.IsNullOrWhiteSpace(choice.Worksheet) || !_uploads.SelectWorksheet(id, choice.Worksheet))
            return BadRequest(new { message = "Choose a worksheet that contains a dataset." });

        return Ok(new { selectedWorksheet = session.SelectedWorksheet });
    }

    [HttpGet("uploads/{id}/dataset")]
    public IActionResult Dataset(string id)
    {
        var session = _uploads.Get(id);
        if (session is null)
            return NotFound(new { message = "That upload has expired. Choose the Excel file again." });
        if (session.SelectedDataset is null)
            return BadRequest(new { message = "Choose a worksheet before configuring the mapping." });

        return Ok(ImportService.DatasetPayload(session.SelectedDataset));
    }

    [HttpGet("uploads/{id}/values")]
    public IActionResult Values(string id, [FromQuery] string? header)
    {
        var dataset = Selected(id, out var error);
        if (dataset is null)
            return error!;
        if (string.IsNullOrWhiteSpace(header))
            return BadRequest(new { message = "Choose a source column." });

        var values = ImportService.DistinctValues(dataset, header);
        if (values is null)
            return BadRequest(new { message = "That source column is not in this worksheet." });

        return Ok(values);
    }

    [HttpPost("uploads/{id}/assess")]
    public IActionResult Assess(string id, [FromBody] MappingDocument mapping)
    {
        var dataset = Selected(id, out var error);
        if (dataset is null)
            return error!;
        return Ok(ImportService.AssessmentPayload(AssignmentEngine.Assess(dataset, mapping)));
    }

    [HttpPost("uploads/{id}/test")]
    public IActionResult Test(string id, [FromBody] MappingDocument mapping)
    {
        var session = _uploads.Get(id);
        if (session is null)
            return NotFound(new { message = "That upload has expired. Choose the Excel file again." });
        if (session.SelectedDataset is null)
            return BadRequest(new { message = "Choose a worksheet before testing the mapping." });

        var mismatch = AssignmentEngine.SourceMismatch(session.SelectedDataset, mapping);
        if (mismatch is not null)
            return BadRequest(new { message = mismatch });

        var built = AssignmentEngine.Build(session.SelectedDataset, mapping, session.FileName, DateTime.Now);
        return Ok(ImportService.BuildPayload(built));
    }

    [HttpPost("loads/inspect")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Inspect(IFormFile? excel, IFormFile? mapping, CancellationToken cancellationToken)
    {
        var loaded = await ReadLoad(excel, mapping, cancellationToken);
        if (loaded.Error is not null)
            return BadRequest(new { message = loaded.Error });

        return Ok(_import.Inspect(loaded.Workbook!, loaded.Mapping!));
    }

    [HttpPost("loads/execute")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Execute(IFormFile? excel, IFormFile? mapping, [FromForm] string? worksheet, CancellationToken cancellationToken)
    {
        var loaded = await ReadLoad(excel, mapping, cancellationToken);
        if (loaded.Error is not null)
            return BadRequest(new { message = loaded.Error });

        try
        {
            SchemaBootstrap.Ensure(_configuration);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Could not reach SQL Server. " + ex.Message });
        }

        var prepared = _import.PrepareExecute(loaded.Workbook!, loaded.Mapping!, worksheet, loaded.FileName!);
        if (prepared.Error is not null)
            return BadRequest(new { message = prepared.Error });

        var outcome = await _import.ExecuteAsync(prepared.Result!, cancellationToken);
        return Ok(outcome);
    }

    private Excel.ExcelDataset? Selected(string id, out IActionResult? error)
    {
        var session = _uploads.Get(id);
        if (session is null)
        {
            error = NotFound(new { message = "That upload has expired. Choose the Excel file again." });
            return null;
        }

        if (session.SelectedDataset is null)
        {
            error = BadRequest(new { message = "Choose a worksheet before configuring the mapping." });
            return null;
        }

        error = null;
        return session.SelectedDataset;
    }

    private async Task<LoadedFiles> ReadLoad(IFormFile? excel, IFormFile? mapping, CancellationToken cancellationToken)
    {
        if (excel is null || excel.Length == 0)
            return LoadedFiles.Fail("Choose an .xlsx file.");
        if (!excel.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return LoadedFiles.Fail("Only .xlsx files are supported.");
        if (mapping is null || mapping.Length == 0)
            return LoadedFiles.Fail("Choose a mapping JSON file.");

        string json;
        using (var reader = new StreamReader(mapping.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            json = await reader.ReadToEndAsync(cancellationToken);

        var document = ImportService.ParseMapping(json, out var parseError);
        if (document is null)
            return LoadedFiles.Fail(parseError ?? "The mapping file could not be read.");

        await using var stream = excel.OpenReadStream();
        var workbook = _import.ReadWorkbook(stream);
        if (workbook.Error is not null)
            return LoadedFiles.Fail(workbook.Error);

        return new LoadedFiles(workbook, document, excel.FileName, null);
    }

    private sealed record LoadedFiles(Excel.WorkbookRead? Workbook, MappingDocument? Mapping, string? FileName, string? Error)
    {
        public static LoadedFiles Fail(string error) => new(null, null, null, error);
    }

    public sealed class WorksheetChoice
    {
        public string? Worksheet { get; set; }
    }
}
