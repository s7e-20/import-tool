using Microsoft.EntityFrameworkCore;

namespace NVGI.Projet.Importtool.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<CaseTransfert> CaseTransferts => Set<CaseTransfert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CaseTransfert>();
        entity.ToTable("CASE_TRANSFERT", "dbo");
        entity.HasKey(e => e.IdRecord);

        entity.Property(e => e.IdRecord).HasColumnName("IdRecord").ValueGeneratedOnAdd();
        entity.Property(e => e.DateRecord).HasColumnName("DateRecord").HasColumnType("datetime");
        entity.Property(e => e.NvgiId).HasColumnName("NVGI ID");
        entity.Property(e => e.Remark).HasColumnName("Remark").HasColumnType("varchar(max)").IsUnicode(false);
        entity.Property(e => e.ClientCode).HasColumnName("Client code").HasMaxLength(12).IsUnicode(false);
        entity.Property(e => e.SupplierCode).HasColumnName("Supplier code").HasMaxLength(12).IsUnicode(false);
        entity.Property(e => e.ApplicantCode).HasColumnName("Applicant code").HasMaxLength(12).IsUnicode(false);
        entity.Property(e => e.CorrespondenceCode).HasColumnName("Correspondence code").HasMaxLength(12).IsUnicode(false);
        entity.Property(e => e.AnnuityFlag).HasColumnName("Annuity flag");
        entity.Property(e => e.Language).HasColumnName("Language").HasMaxLength(20).IsUnicode(false);
        entity.Property(e => e.RootNvgiCaseNumber).HasColumnName("Root NVGI case Number").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.Family).HasColumnName("Family").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.DivisionalCode).HasColumnName("Divisional code").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.Suffix).HasColumnName("Suffix").HasMaxLength(20).IsUnicode(false);
        entity.Property(e => e.CountryCode).HasColumnName("Country code").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.Kind).HasColumnName("kind").HasMaxLength(20).IsUnicode(false);
        entity.Property(e => e.InternalTitle).HasColumnName("Internal title").HasColumnType("varchar(max)").IsUnicode(false);
        entity.Property(e => e.OfficialTitle).HasColumnName("Official title").HasColumnType("varchar(max)").IsUnicode(false);
        entity.Property(e => e.Instructor).HasColumnName("Instructor").HasColumnType("varchar(max)").IsUnicode(false);
        entity.Property(e => e.Origin).HasColumnName("Origin").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.FilingNumber).HasColumnName("Filing number").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.FilingDate).HasColumnName("Filing date").HasColumnType("datetime");
        entity.Property(e => e.NextAnnuityDue).HasColumnName("Next annuity due");
        entity.Property(e => e.NextAnnuityDueDate).HasColumnName("Next annuity due date").HasColumnType("datetime");
        entity.Property(e => e.PublicationNumber).HasColumnName("Publication number").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.PublicationDate).HasColumnName("Publication date").HasColumnType("datetime");
        entity.Property(e => e.GrantNumber).HasColumnName("Grant number").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.GrantDate).HasColumnName("Grant date").HasColumnType("datetime");
        entity.Property(e => e.ClientReference).HasColumnName("Client reference").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.ClientInternalId).HasColumnName("Client Internal ID").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.Owner).HasColumnName("Owner").HasColumnType("varchar(max)").IsUnicode(false);
        entity.Property(e => e.Claims).HasColumnName("Claims");
        entity.Property(e => e.Inventors).HasColumnName("Inventors").HasColumnType("varchar(max)").IsUnicode(false);
        entity.Property(e => e.ServiceLevel).HasColumnName("Service level").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.RecordType).HasColumnName("Record Type").HasMaxLength(1).IsUnicode(false).IsRequired();
        entity.Property(e => e.RecordDirection).HasColumnName("Record Direction").HasMaxLength(1).IsUnicode(false).IsRequired();
        entity.Property(e => e.ProcessedDate).HasColumnName("Processed Date").HasColumnType("datetime");
        entity.Property(e => e.IgnoreAnnuityBefore).HasColumnName("IgnoreAnnuityBefore").HasColumnType("datetime");
        entity.Property(e => e.NvgiReference).HasColumnName("NVGI Reference").HasMaxLength(25).IsUnicode(false);
        entity.Property(e => e.SmallEntity).HasColumnName("Small Entity");
        entity.Property(e => e.ExpirationDate).HasColumnName("ExpirationDate").HasColumnType("datetime");
        entity.Property(e => e.PctNumber).HasColumnName("PCTNumber").HasMaxLength(25).IsUnicode(false);
        entity.Property(e => e.DivisionNationalFilingDate).HasColumnName("DivisionNationalFilingDate").HasColumnType("datetime");
        entity.Property(e => e.PtaAdjustment).HasColumnName("PTAAdjustment");
        entity.Property(e => e.PaymentOrderRef).HasColumnName("Payment Order Ref").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.SourceFile).HasColumnName("SOURCE_FILE").HasMaxLength(255).IsUnicode(false);
        entity.Property(e => e.FinalClientReference).HasColumnName("FinalClientReference").HasMaxLength(150).IsUnicode(false);
        entity.Property(e => e.FinalClientAccount).HasColumnName("FinalClientAccount").HasMaxLength(10).IsUnicode(false);
        entity.Property(e => e.FinalClientName).HasColumnName("FinalClientName").HasMaxLength(100).IsUnicode(false);
        entity.Property(e => e.ValidationFlag).HasColumnName("Validation flag");
        entity.Property(e => e.MailValidation).HasColumnName("Mail validation").HasMaxLength(250).IsUnicode(false);
        entity.Property(e => e.MailCcValidation).HasColumnName("Mail cc validation").HasMaxLength(250).IsUnicode(false);
        entity.Property(e => e.ValidationTarifCode).HasColumnName("ValidationTarifCode").HasMaxLength(12).IsUnicode(false);
        entity.Property(e => e.TranslationTarifCode).HasColumnName("TranslationTarifCode").HasMaxLength(12).IsUnicode(false);
        entity.Property(e => e.ExpectedDeadline).HasColumnName("ExpectedDeadline").HasColumnType("datetime");
        entity.Property(e => e.Communication).HasColumnName("Communication").HasMaxLength(1000).IsUnicode(false);
        entity.Property(e => e.SalesForceReference).HasColumnName("SalesForceReference").HasMaxLength(30).IsUnicode(false);
        entity.Property(e => e.AgentReference).HasColumnName("AgentReference").HasMaxLength(200).IsUnicode(false);
        entity.Property(e => e.Source).HasColumnName("Source").HasMaxLength(15).IsUnicode(false).IsRequired();
        entity.Property(e => e.TranslateOnly713).HasColumnName("translateOnly713");
        entity.Property(e => e.TranslateEn713).HasColumnName("translateEN713").IsRequired();
        entity.Property(e => e.TranslateDe713).HasColumnName("translateDE713").IsRequired();
        entity.Property(e => e.TranslateFr713).HasColumnName("translateFR713").IsRequired();
        entity.Property(e => e.CheckPa713).HasColumnName("checkPA713").IsRequired();
        entity.Property(e => e.ConfirmFiling713).HasColumnName("confirmFiling713").IsRequired();
        entity.Property(e => e.Consent713).HasColumnName("consent713").IsRequired();
        entity.Property(e => e.PayTaxes713).HasColumnName("payTaxes713").IsRequired();
        entity.Property(e => e.PayAnnuities).HasColumnName("payAnnuities").IsRequired();
        entity.Property(e => e.Representative713).HasColumnName("representative713").IsRequired();
        entity.Property(e => e.WithdrawRepresentative713).HasColumnName("withdrawRepresentative713").IsRequired();
        entity.Property(e => e.RemoveCountry713).HasColumnName("removeCountry713").HasMaxLength(150).IsUnicode(false);
        entity.Property(e => e.Note713).HasColumnName("note713").HasMaxLength(5000).IsUnicode(false);
        entity.Property(e => e.WithdrawAddress713).HasColumnName("withdrawAddress713").HasMaxLength(1000).IsUnicode(false);
        entity.Property(e => e.AmendedText713).HasColumnName("amendedText713").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.ValidationOrderRef).HasColumnName("ValidationOrderRef").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.InstructionAnnuite).HasColumnName("InstructionAnnuite").HasMaxLength(1).IsUnicode(false);
        entity.Property(e => e.MontantFacture).HasColumnName("MontantFacture").HasPrecision(19, 2);
        entity.Property(e => e.Representative).HasColumnName("Representative").HasMaxLength(1000).IsUnicode(false);
        entity.Property(e => e.CeaVoie).HasColumnName("CEAVoie").HasMaxLength(4).IsUnicode(false);
        entity.Property(e => e.CeaPriorite).HasColumnName("CEAPriorite").HasMaxLength(250).IsUnicode(false);
        entity.Property(e => e.CeaPublicationGrant).HasColumnName("CEAPublicationGrant").HasColumnType("datetime");
        entity.Property(e => e.CeaQuotePart).HasColumnName("CEAQuotePart").HasMaxLength(500).IsUnicode(false);
        entity.Property(e => e.CeaCabinetProcedure).HasColumnName("CEACabinetProcedure").HasMaxLength(100).IsUnicode(false);
        entity.Property(e => e.CeaRefCabinetProcedure).HasColumnName("CEARefCabinetProcedure").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.CeaQuotePartCea).HasColumnName("CEAQuotePartCEA").HasPrecision(5, 2);
        entity.Property(e => e.PtoReference).HasColumnName("PTOReference").HasMaxLength(100).IsUnicode(false);
        entity.Property(e => e.LocalAgent).HasColumnName("LocalAgent").HasMaxLength(100).IsUnicode(false);
        entity.Property(e => e.Revalidation).HasColumnName("Revalidation");
        entity.Property(e => e.ApplicantNameAddress).HasColumnName("ApplicantNameAddress").HasMaxLength(1000).IsUnicode(false);
        entity.Property(e => e.ChangeOfName).HasColumnName("ChangeOfName").IsRequired();
        entity.Property(e => e.AssignmentFlag).HasColumnName("Assignment").IsRequired();
        entity.Property(e => e.ChangeOfAddress).HasColumnName("ChangeOfAddress").IsRequired();
        entity.Property(e => e.EpoAccountNumber).HasColumnName("EPOAccountNumber").HasMaxLength(25).IsUnicode(false);
        entity.Property(e => e.EpoAccountName).HasColumnName("EPOAccountName").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.BusinessArea).HasColumnName("BusinessArea").HasMaxLength(255).IsUnicode(false);
        entity.Property(e => e.ClientName713).HasColumnName("ClientName713").HasMaxLength(150).IsUnicode(false);
        entity.Property(e => e.DelayGrantForUp).HasColumnName("DelayGrantForUP");
        entity.Property(e => e.OptOutEp).HasColumnName("OptOutEP");
        entity.Property(e => e.InternalNoteNvgi).HasColumnName("internalNoteNVGI").HasMaxLength(250).IsUnicode(false);
        entity.Property(e => e.AllowDuplicateCreation).HasColumnName("allowDuplicateCreation").IsRequired();
        entity.Property(e => e.ManagerClientDossier).HasColumnName("ManagerClientDossier").HasMaxLength(100).IsUnicode(true);
        entity.Property(e => e.ManagerClientEmailDossier).HasColumnName("ManagerClientEmailDossier").HasMaxLength(50).IsUnicode(false);
        entity.Property(e => e.SessionImport).HasColumnName("SessionImport").HasMaxLength(32).IsUnicode(false);
        entity.Property(e => e.MailNotifValidation).HasColumnName("Mail notif validation").HasMaxLength(250).IsUnicode(false);
        entity.Property(e => e.ProcessedByProjetProxy).HasColumnName("ProcessedByProjetProxy");
        entity.Property(e => e.ApCountryCodeList).HasColumnName("ApCountryCodeList").HasMaxLength(200).IsUnicode(false);
    }
}
