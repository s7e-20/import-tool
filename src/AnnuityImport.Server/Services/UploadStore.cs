using NVGI.Projet.Importtool.Excel;

namespace NVGI.Projet.Importtool.Services;

public sealed class UploadStore
{
    private readonly string _root;
    private readonly Dictionary<string, UploadSession> _sessions = new(StringComparer.Ordinal);

    public UploadStore()
    {
        _root = Path.Combine(Path.GetTempPath(), "annuity-import-uploads");
        Directory.CreateDirectory(_root);
    }

    public UploadSession Save(string originalFileName, WorkbookRead workbook)
    {
        Cleanup();
        var id = Guid.NewGuid().ToString("N");
        var session = new UploadSession
        {
            Id = id,
            FileName = Path.GetFileName(originalFileName),
            Workbook = workbook,
            CreatedUtc = DateTime.UtcNow
        };

        var usable = workbook.Worksheets.Where(sheet => sheet.HasDataset).ToList();
        if (usable.Count == 1)
            session.SelectedWorksheet = usable[0].Name;

        lock (_sessions)
            _sessions[id] = session;

        return session;
    }

    public UploadSession? Get(string id)
    {
        lock (_sessions)
            return _sessions.TryGetValue(id, out var session) ? session : null;
    }

    public bool SelectWorksheet(string id, string worksheet)
    {
        lock (_sessions)
        {
            if (!_sessions.TryGetValue(id, out var session))
                return false;

            var sheet = session.Workbook.Worksheets.FirstOrDefault(item =>
                item.Name.Equals(worksheet, StringComparison.OrdinalIgnoreCase));
            if (sheet?.Dataset is null)
                return false;

            session.SelectedWorksheet = sheet.Name;
            return true;
        }
    }

    private void Cleanup()
    {
        var cutoff = DateTime.UtcNow.AddHours(-12);
        lock (_sessions)
        {
            var stale = _sessions.Where(pair => pair.Value.CreatedUtc < cutoff).Select(pair => pair.Key).ToList();
            foreach (var id in stale)
                _sessions.Remove(id);
        }
    }
}

public sealed class UploadSession
{
    public required string Id { get; init; }
    public required string FileName { get; init; }
    public required WorkbookRead Workbook { get; init; }
    public DateTime CreatedUtc { get; init; }
    public string? SelectedWorksheet { get; set; }

    public ExcelDataset? SelectedDataset =>
        Workbook.Worksheets.FirstOrDefault(sheet =>
            sheet.Name.Equals(SelectedWorksheet, StringComparison.OrdinalIgnoreCase))?.Dataset;
}
