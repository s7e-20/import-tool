namespace AnnuityImport.Server.Data;

public enum TargetKind
{
    Text,
    DateTime,
    Integer,
    Decimal,
    Boolean
}

public sealed record FieldDef(
    string Column,
    string Title,
    string SqlType,
    TargetKind Kind,
    bool Mandatory,
    bool Displayed);

public static class TargetCatalogue
{
    public static IReadOnlyList<FieldDef> Displayed { get; } =
    [
        new("Client code", "Client code", "varchar(12)", TargetKind.Text, true, true),
        new("Supplier code", "Supplier code", "varchar(12)", TargetKind.Text, false, true),
        new("Applicant code", "Tariff code", "varchar(12)", TargetKind.Text, false, true),
        new("Correspondence code", "Correspondance code", "varchar(12)", TargetKind.Text, false, true),
        new("Language", "Case language", "varchar(20)", TargetKind.Text, true, true),
        new("Root NVGI case Number", "Base case numbering", "varchar(50)", TargetKind.Text, false, true),
        new("Family", "Family grouping", "varchar(50)", TargetKind.Text, false, true),
        new("Divisional code", "NVGI reference divisional code", "varchar(50)", TargetKind.Text, false, true),
        new("Suffix", "Suffix", "varchar(20)", TargetKind.Text, false, true),
        new("Country code", "Country code", "varchar(50)", TargetKind.Text, true, true),
        new("kind", "Kind code", "varchar(20)", TargetKind.Text, true, true),
        new("Internal title", "Internal title", "text", TargetKind.Text, false, true),
        new("Official title", "Official title", "text", TargetKind.Text, false, true),
        new("Instructor", "Instructor name", "text", TargetKind.Text, false, true),
        new("Origin", "Origin", "varchar(50)", TargetKind.Text, true, true),
        new("Filing number", "Filing number", "varchar(50)", TargetKind.Text, true, true),
        new("Filing date", "Filing date", "datetime", TargetKind.DateTime, true, true),
        new("Next annuity due", "Next annuity year", "int", TargetKind.Integer, false, true),
        new("Next annuity due date", "Next due date", "datetime", TargetKind.DateTime, false, true),
        new("Publication number", "Publication number", "varchar(50)", TargetKind.Text, false, true),
        new("Publication date", "Publication date", "datetime", TargetKind.DateTime, false, true),
        new("Grant number", "Grant number", "varchar(50)", TargetKind.Text, false, true),
        new("Grant date", "Grant date", "datetime", TargetKind.DateTime, false, true),
        new("Client reference", "Client reference", "varchar(50)", TargetKind.Text, true, true),
        new("Client Internal ID", "Client internal reference", "varchar(50)", TargetKind.Text, false, true),
        new("Owner", "Applicants", "text", TargetKind.Text, false, true),
        new("Claims", "Number of claims", "int", TargetKind.Integer, false, true),
        new("Inventors", "Inventors", "text", TargetKind.Text, false, true),
        new("Service level", "Service level code", "varchar(50)", TargetKind.Text, true, true),
        new("Record Type", "Record type", "varchar(1)", TargetKind.Text, true, true),
        new("IgnoreAnnuityBefore", "Ignore annuity before", "datetime", TargetKind.DateTime, false, true),
        new("NVGI Reference", "NVGI Reference", "varchar(25)", TargetKind.Text, false, true),
        new("Small Entity", "Small entity", "bit", TargetKind.Boolean, true, true),
        new("ExpirationDate", "Expiration date", "datetime", TargetKind.DateTime, false, true),
        new("PCTNumber", "PCT Number", "varchar(25)", TargetKind.Text, false, true),
        new("DivisionNationalFilingDate", "Divisional/National date", "datetime", TargetKind.DateTime, false, true),
        new("PTAAdjustment", "PTA", "int", TargetKind.Integer, false, true),
        new("Payment Order Ref", "Payment order ref", "varchar(50)", TargetKind.Text, false, true),
        new("AgentReference", "Agent reference", "varchar(200)", TargetKind.Text, false, true),
        new("InstructionAnnuite", "Annuity instruction code", "varchar(1)", TargetKind.Text, false, true),
        new("MontantFacture", "Expected annuity amount", "decimal(19, 2)", TargetKind.Decimal, false, true),
        new("PTOReference", "PTO Reference", "varchar(100)", TargetKind.Text, false, true),
        new("LocalAgent", "Local agent name", "varchar(100)", TargetKind.Text, false, true),
        new("BusinessArea", "Business area", "varchar(255)", TargetKind.Text, false, true),
        new("ManagerClientDossier", "Client contact name", "nvarchar(100)", TargetKind.Text, false, true),
        new("ManagerClientEmailDossier", "Client contact email", "varchar(50)", TargetKind.Text, false, true),
        new("ApCountryCodeList", "AP countries", "varchar(200)", TargetKind.Text, false, true)
    ];

    public static IReadOnlyDictionary<string, FieldDef> ByColumn { get; } =
        Displayed.ToDictionary(f => f.Column, StringComparer.Ordinal);

    public static readonly HashSet<string> HiddenColumns = new(StringComparer.Ordinal)
    {
        "IdRecord", "DateRecord", "NVGI ID", "Remark", "Annuity flag", "Record Direction", "Processed Date",
        "SOURCE_FILE", "FinalClientReference", "FinalClientAccount", "FinalClientName", "Validation flag",
        "Mail validation", "Mail cc validation", "ValidationTarifCode", "TranslationTarifCode", "ExpectedDeadline",
        "Communication", "SalesForceReference", "Source", "translateOnly713", "translateEN713", "translateDE713",
        "translateFR713", "checkPA713", "confirmFiling713", "consent713", "payTaxes713", "payAnnuities",
        "representative713", "withdrawRepresentative713", "removeCountry713", "note713", "withdrawAddress713",
        "amendedText713", "ValidationOrderRef", "Representative", "CEAVoie", "CEAPriorite", "CEAPublicationGrant",
        "CEAQuotePart", "CEACabinetProcedure", "CEARefCabinetProcedure", "CEAQuotePartCEA", "Revalidation",
        "ApplicantNameAddress", "ChangeOfName", "Assignment", "ChangeOfAddress", "EPOAccountNumber",
        "EPOAccountName", "ClientName713", "DelayGrantForUP", "OptOutEP", "internalNoteNVGI",
        "allowDuplicateCreation", "SessionImport", "Mail notif validation", "ProcessedByProjetProxy"
    };

    static TargetCatalogue()
    {
        if (Displayed.Count != 47)
            throw new InvalidOperationException($"Expected 47 displayed fields, found {Displayed.Count}.");
        if (HiddenColumns.Count != 59)
            throw new InvalidOperationException($"Expected 59 hidden fields, found {HiddenColumns.Count}.");
    }

    public static void ApplyHidden(CaseTransfert row, string sourceFileName, DateTime importDate)
    {
        row.DateRecord = DateTime.SpecifyKind(importDate.Date, DateTimeKind.Unspecified);
        row.NvgiId = null;
        row.Remark = null;
        row.AnnuityFlag = true;
        row.RecordDirection = "I";
        row.ProcessedDate = null;
        row.SourceFile = sourceFileName;
        row.FinalClientReference = null;
        row.FinalClientAccount = null;
        row.FinalClientName = null;
        row.ValidationFlag = false;
        row.MailValidation = null;
        row.MailCcValidation = null;
        row.ValidationTarifCode = null;
        row.TranslationTarifCode = null;
        row.ExpectedDeadline = null;
        row.Communication = null;
        row.SalesForceReference = null;
        row.Source = "Import tool";
        row.TranslateOnly713 = null;
        row.TranslateEn713 = false;
        row.TranslateDe713 = false;
        row.TranslateFr713 = false;
        row.CheckPa713 = false;
        row.ConfirmFiling713 = false;
        row.Consent713 = false;
        row.PayTaxes713 = false;
        row.PayAnnuities = false;
        row.Representative713 = false;
        row.WithdrawRepresentative713 = false;
        row.RemoveCountry713 = null;
        row.Note713 = null;
        row.WithdrawAddress713 = null;
        row.AmendedText713 = null;
        row.ValidationOrderRef = null;
        row.Representative = null;
        row.CeaVoie = null;
        row.CeaPriorite = null;
        row.CeaPublicationGrant = null;
        row.CeaQuotePart = null;
        row.CeaCabinetProcedure = null;
        row.CeaRefCabinetProcedure = null;
        row.CeaQuotePartCea = null;
        row.Revalidation = null;
        row.ApplicantNameAddress = null;
        row.ChangeOfName = false;
        row.AssignmentFlag = false;
        row.ChangeOfAddress = false;
        row.EpoAccountNumber = null;
        row.EpoAccountName = null;
        row.ClientName713 = null;
        row.DelayGrantForUp = null;
        row.OptOutEp = null;
        row.InternalNoteNvgi = null;
        row.AllowDuplicateCreation = false;
        row.SessionImport = null;
        row.MailNotifValidation = null;
        row.ProcessedByProjetProxy = null;
    }

    public static void SetDisplayed(CaseTransfert row, string column, object? value)
    {
        switch (column)
        {
            case "Client code": row.ClientCode = (string?)value; break;
            case "Supplier code": row.SupplierCode = (string?)value; break;
            case "Applicant code": row.ApplicantCode = (string?)value; break;
            case "Correspondence code": row.CorrespondenceCode = (string?)value; break;
            case "Language": row.Language = (string?)value; break;
            case "Root NVGI case Number": row.RootNvgiCaseNumber = (string?)value; break;
            case "Family": row.Family = (string?)value; break;
            case "Divisional code": row.DivisionalCode = (string?)value; break;
            case "Suffix": row.Suffix = (string?)value; break;
            case "Country code": row.CountryCode = (string?)value; break;
            case "kind": row.Kind = (string?)value; break;
            case "Internal title": row.InternalTitle = (string?)value; break;
            case "Official title": row.OfficialTitle = (string?)value; break;
            case "Instructor": row.Instructor = (string?)value; break;
            case "Origin": row.Origin = (string?)value; break;
            case "Filing number": row.FilingNumber = (string?)value; break;
            case "Filing date": row.FilingDate = (DateTime?)value; break;
            case "Next annuity due": row.NextAnnuityDue = (int?)value; break;
            case "Next annuity due date": row.NextAnnuityDueDate = (DateTime?)value; break;
            case "Publication number": row.PublicationNumber = (string?)value; break;
            case "Publication date": row.PublicationDate = (DateTime?)value; break;
            case "Grant number": row.GrantNumber = (string?)value; break;
            case "Grant date": row.GrantDate = (DateTime?)value; break;
            case "Client reference": row.ClientReference = (string?)value; break;
            case "Client Internal ID": row.ClientInternalId = (string?)value; break;
            case "Owner": row.Owner = (string?)value; break;
            case "Claims": row.Claims = (int?)value; break;
            case "Inventors": row.Inventors = (string?)value; break;
            case "Service level": row.ServiceLevel = (string?)value; break;
            case "Record Type": row.RecordType = (string?)value ?? ""; break;
            case "IgnoreAnnuityBefore": row.IgnoreAnnuityBefore = (DateTime?)value; break;
            case "NVGI Reference": row.NvgiReference = (string?)value; break;
            case "Small Entity": row.SmallEntity = (bool?)value; break;
            case "ExpirationDate": row.ExpirationDate = (DateTime?)value; break;
            case "PCTNumber": row.PctNumber = (string?)value; break;
            case "DivisionNationalFilingDate": row.DivisionNationalFilingDate = (DateTime?)value; break;
            case "PTAAdjustment": row.PtaAdjustment = (int?)value; break;
            case "Payment Order Ref": row.PaymentOrderRef = (string?)value; break;
            case "AgentReference": row.AgentReference = (string?)value; break;
            case "InstructionAnnuite": row.InstructionAnnuite = (string?)value; break;
            case "MontantFacture": row.MontantFacture = (decimal?)value; break;
            case "PTOReference": row.PtoReference = (string?)value; break;
            case "LocalAgent": row.LocalAgent = (string?)value; break;
            case "BusinessArea": row.BusinessArea = (string?)value; break;
            case "ManagerClientDossier": row.ManagerClientDossier = (string?)value; break;
            case "ManagerClientEmailDossier": row.ManagerClientEmailDossier = (string?)value; break;
            case "ApCountryCodeList": row.ApCountryCodeList = (string?)value; break;
            default: throw new InvalidOperationException($"'{column}' is not a displayed target.");
        }
    }
}
