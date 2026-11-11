using Application.Exports;
using Application.Tests.Support;
using Domain.Documents;
using Domain.Exports;

namespace Application.Tests;

public class ExportReadinessValidatorTests
{
    [Fact]
    public void Golden_sample_is_ready_to_export()
    {
        Assert.Empty(ExportReadinessValidator.Validate(Samples.Golden()));
    }

    [Fact]
    public void Empty_document_lists_what_the_pre_textual_pages_need()
    {
        var errors = ExportReadinessValidator.Validate(DocumentModel.CreateEmpty("abnt-tcc", "Meu TCC"));

        Assert.Equal(
            ["metadata.authors", "metadata.city", "metadata.year", "metadata.institution", "metadata.nature", "metadata.advisor",
             "preTextual.abstractPt.text", "preTextual.abstractPt.keywords"],
            errors.Select(e => e.Path));
        Assert.All(errors, e => Assert.Equal("export.required", e.Code));
    }

    [Fact]
    public void Disabled_parts_are_not_required()
    {
        var doc = Samples.Golden();
        doc = doc with
        {
            Metadata = doc.Metadata with { Institution = "", Nature = "", Advisor = "" },
            PreTextual = doc.PreTextual with
            {
                Cover = new ToggleElement { Enabled = false },
                TitlePage = new ToggleElement { Enabled = false },
                AbstractEn = doc.PreTextual.AbstractEn with { Text = "", Keywords = [] , Enabled = false },
            },
        };

        Assert.Empty(ExportReadinessValidator.Validate(doc));
    }
}

public class ExportFileNameTests
{
    [Theory]
    [InlineData("Formatação automática de trabalhos acadêmicos", "formatacao-automatica-de-trabalhos-academicos")]
    [InlineData("  TCC: versão final (2026)!  ", "tcc-versao-final-2026")]
    [InlineData("../../etc/passwd", "etc-passwd")]
    [InlineData("???", "trabalho")]
    public void Builds_safe_ascii_names(string title, string expected)
    {
        Assert.Equal(expected, ExportService.FileBaseName(title));
    }

    [Fact]
    public void Limits_the_length()
    {
        Assert.True(ExportService.FileBaseName(new string('a', 200)).Length <= 60);
    }
}

public class ExportJobTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static ExportJob NewJob(ExportFormat format = ExportFormat.Pdf) =>
        ExportJob.Create(Guid.NewGuid(), Guid.NewGuid(), 3, Samples.Golden(), format, "meu-tcc", T0);

    [Fact]
    public void Starts_queued_with_snapshot_and_file_name()
    {
        var job = NewJob();

        Assert.Equal(ExportStatus.Queued, job.Status);
        Assert.Equal(3, job.DocumentVersion);
        Assert.Equal("meu-tcc.pdf", job.FileName);
        Assert.Equal("application/pdf", job.ContentType);
        Assert.Equal("meu-tcc.docx", NewJob(ExportFormat.Docx).FileName);
    }

    [Fact]
    public void Success_sets_file_and_expiry()
    {
        var job = NewJob();
        job.Start(T0);
        job.Succeed("abc.pdf", 1234, ["aviso"], T0.AddSeconds(5), TimeSpan.FromHours(24));

        Assert.Equal(ExportStatus.Succeeded, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Equal(T0.AddSeconds(5).AddHours(24), job.ExpiresAt);
        Assert.Equal(["aviso"], job.Warnings);
    }

    [Fact]
    public void Running_job_can_be_requeued_but_finished_job_cannot()
    {
        var running = NewJob();
        running.Start(T0);
        running.Requeue();
        Assert.Equal(ExportStatus.Queued, running.Status);

        var failed = NewJob();
        failed.Fail("erro", T0);
        failed.Requeue();
        Assert.Equal(ExportStatus.Failed, failed.Status);
    }

    [Fact]
    public void Expire_forgets_the_file()
    {
        var job = NewJob();
        job.Succeed("abc.pdf", 1, [], T0, TimeSpan.FromHours(1));
        job.Expire();

        Assert.Equal(ExportStatus.Expired, job.Status);
        Assert.Null(job.FileKey);
    }
}
