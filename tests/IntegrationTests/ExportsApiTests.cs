using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Application.Exports;
using DocumentFormat.OpenXml.Packaging;
using Domain.Documents;
using Domain.Exports;
using DTOs.Exports;
using DTOs.Works;
using Exporter.Pdf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using UglyToad.PdfPig;

namespace IntegrationTests;

/// <summary>End-to-end: API → queue → background worker → Open XML (→ LibreOffice) → storage → download.</summary>
public sealed class ExportsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = DocumentJson.Options;
    private static readonly bool LibreOfficeAvailable =
        new LibreOfficeConverter(Options.Create(new LibreOfficeOptions())).IsAvailable;

    private readonly HttpClient _client = factory.CreateClient();

    private static string GoldenJson => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "golden-tcc.json"));

    private async Task<WorkResponse> CreateWorkWithGolden(Action<JsonNode>? edit = null)
    {
        var created = await _client.PostAsJsonAsync("/api/works", new { profileId = "abnt-tcc" });
        var work = (await created.Content.ReadFromJsonAsync<WorkResponse>(Json))!;
        var doc = JsonNode.Parse(GoldenJson)!;
        edit?.Invoke(doc);
        var saved = await _client.PutAsync($"/api/works/{work.Id}/document",
            new StringContent($"{{\"version\":{work.Version},\"document\":{doc.ToJsonString()}}}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        return (await _client.GetFromJsonAsync<WorkResponse>($"/api/works/{work.Id}", Json))!;
    }

    private async Task<HttpResponseMessage> RequestExport(Guid workId, string format) =>
        await _client.PostAsJsonAsync($"/api/works/{workId}/exports", new { format });

    private async Task<ExportJobResponse> WaitForCompletion(Guid jobId, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(90));
        while (true)
        {
            var job = (await _client.GetFromJsonAsync<ExportJobResponse>($"/api/exports/{jobId}", Json))!;
            if (job.Status is ExportStatus.Succeeded or ExportStatus.Failed) return job;
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Export {jobId} still {job.Status}.");
            await Task.Delay(200);
        }
    }

    private async Task<ExportJobResponse> ExportAndWait(Guid workId, string format)
    {
        var response = await RequestExport(workId, format);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = (await response.Content.ReadFromJsonAsync<ExportJobResponse>(Json))!;
        Assert.Equal($"/api/exports/{job.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal(ExportStatus.Queued, job.Status);
        return await WaitForCompletion(job.Id);
    }

    [Fact]
    public async Task Exports_docx_end_to_end()
    {
        var work = await CreateWorkWithGolden();

        var job = await ExportAndWait(work.Id, "docx");

        Assert.Equal(ExportStatus.Succeeded, job.Status);
        Assert.Equal(work.Version, job.DocumentVersion);
        Assert.Equal("formatacao-automatica-de-trabalhos-academicos.docx", job.FileName);
        Assert.Contains(job.Warnings, w => w.Contains("figura"));
        Assert.Equal($"/api/exports/{job.Id}/file", job.DownloadUrl);

        var file = await _client.GetAsync(job.DownloadUrl);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", file.Content.Headers.ContentType!.MediaType);
        Assert.Equal(job.FileName, file.Content.Headers.ContentDisposition!.FileNameStar ?? file.Content.Headers.ContentDisposition.FileName);

        await using var stream = new MemoryStream(await file.Content.ReadAsByteArrayAsync());
        using var docx = WordprocessingDocument.Open(stream, false);
        Assert.Contains("INTRODUÇÃO".ToLowerInvariant(), docx.MainDocumentPart!.Document!.Body!.InnerText.ToLowerInvariant());
    }

    [SkippableFact]
    public async Task Exports_pdf_end_to_end()
    {
        Skip.IfNot(LibreOfficeAvailable, "LibreOffice (soffice) não está instalado nesta máquina.");
        var work = await CreateWorkWithGolden();

        var job = await ExportAndWait(work.Id, "pdf");

        Assert.True(job.Status == ExportStatus.Succeeded, job.Error);
        var bytes = await _client.GetByteArrayAsync(job.DownloadUrl);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(8, pdf.NumberOfPages);
        Assert.Equal(bytes.LongLength, job.FileSize);
    }

    [Fact]
    public async Task Exports_the_snapshot_taken_when_requested()
    {
        var work = await CreateWorkWithGolden(doc => doc["metadata"]!["title"] = "Titulo original");

        var response = await RequestExport(work.Id, "docx");
        var queued = (await response.Content.ReadFromJsonAsync<ExportJobResponse>(Json))!;

        // Edit the work right after requesting the export.
        var doc = JsonNode.Parse(GoldenJson)!;
        doc["metadata"]!["title"] = "Titulo alterado depois";
        await _client.PutAsync($"/api/works/{work.Id}/document",
            new StringContent($"{{\"version\":{work.Version},\"document\":{doc.ToJsonString()}}}", Encoding.UTF8, "application/json"));

        var job = await WaitForCompletion(queued.Id);
        var bytes = await _client.GetByteArrayAsync(job.DownloadUrl);
        using var docx = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var text = docx.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("Titulo original", text);
        Assert.DoesNotContain("Titulo alterado depois", text);
    }

    [Fact]
    public async Task Rejects_work_that_is_not_ready_with_paths()
    {
        var created = await _client.PostAsJsonAsync("/api/works", new { profileId = "abnt-tcc", title = "Rascunho" });
        var work = (await created.Content.ReadFromJsonAsync<WorkResponse>(Json))!;

        var response = await RequestExport(work.Id, "docx");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["errors"]!.AsObject();
        Assert.Contains("metadata.authors", errors.Select(e => e.Key));
        Assert.Contains("metadata.advisor", errors.Select(e => e.Key));
    }

    [Fact]
    public async Task Rejects_unknown_format_and_unknown_resources()
    {
        var work = await CreateWorkWithGolden();

        Assert.Equal(HttpStatusCode.BadRequest, (await RequestExport(work.Id, "odt")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RequestExport(Guid.NewGuid(), "docx")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/exports/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/exports/{Guid.NewGuid()}/file")).StatusCode);
    }

    [Fact]
    public async Task Lists_exports_of_a_work_newest_first()
    {
        var work = await CreateWorkWithGolden();
        var first = await ExportAndWait(work.Id, "docx");
        var second = await ExportAndWait(work.Id, "docx");

        var list = (await _client.GetFromJsonAsync<List<ExportJobResponse>>($"/api/works/{work.Id}/exports", Json))!;

        Assert.Equal([second.Id, first.Id], list.Select(j => j.Id));
    }

    [Fact]
    public async Task Expired_files_are_removed_and_return_410()
    {
        var work = await CreateWorkWithGolden();
        var job = await ExportAndWait(work.Id, "docx");
        var path = Path.Combine(factory.StorageRoot, $"{job.Id:N}.docx");
        Assert.True(File.Exists(path));

        await using (var conn = new NpgsqlConnection(factory.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("update export_jobs set expires_at = now() - interval '1 hour' where id = @id", conn);
            cmd.Parameters.AddWithValue("id", job.Id);
            Assert.Equal(1, await cmd.ExecuteNonQueryAsync());
        }

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ExportJobProcessor>().CleanupExpiredAsync(CancellationToken.None);

        Assert.False(File.Exists(path));
        Assert.Equal(HttpStatusCode.Gone, (await _client.GetAsync(job.DownloadUrl)).StatusCode);
        var status = (await _client.GetFromJsonAsync<ExportJobResponse>($"/api/exports/{job.Id}", Json))!;
        Assert.Equal(ExportStatus.Expired, status.Status);
        Assert.Null(status.DownloadUrl);
    }

    [Fact]
    public async Task Deleting_a_work_removes_its_exports_and_files()
    {
        var work = await CreateWorkWithGolden();
        var job = await ExportAndWait(work.Id, "docx");
        var path = Path.Combine(factory.StorageRoot, $"{job.Id:N}.docx");
        Assert.True(File.Exists(path));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/works/{work.Id}")).StatusCode);

        Assert.False(File.Exists(path));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/exports/{job.Id}")).StatusCode);
    }

    [Fact]
    public async Task Health_reports_libreoffice()
    {
        var body = await _client.GetStringAsync("/api/health");

        Assert.Contains("\"name\":\"libreoffice\"", body);
    }
}
