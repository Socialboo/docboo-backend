using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Domain.Documents;
using DTOs.Works;
using Npgsql;

namespace IntegrationTests;

public sealed class WorksApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = DocumentJson.Options;
    private readonly HttpClient _client = factory.CreateClient();

    private static string GoldenJson => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "golden-tcc.json"));

    private async Task<WorkResponse> CreateWork(string? title = null)
    {
        var response = await _client.PostAsJsonAsync("/api/works", new { profileId = "abnt-tcc", title });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkResponse>(Json))!;
    }

    private Task<HttpResponseMessage> SaveRaw(Guid id, int version, string documentJson) =>
        _client.PutAsync($"/api/works/{id}/document", new StringContent(
            $"{{\"version\":{version},\"document\":{documentJson}}}", Encoding.UTF8, "application/json"));

    private static async Task<JsonObject> ReadProblem(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    }

    [Fact]
    public async Task Health_reports_database_healthy()
    {
        var body = await _client.GetStringAsync("/api/health");

        Assert.Contains("\"status\":\"Healthy\"", body);
        Assert.Contains("\"name\":\"postgres\",\"status\":\"Healthy\"", body);
    }

    [Fact]
    public async Task Lists_abnt_profile()
    {
        var profiles = await _client.GetFromJsonAsync<List<ProfileResponse>>("/api/profiles", Json);

        var abnt = Assert.Single(profiles!, p => p.Id == "abnt-tcc");
        Assert.Equal(5, abnt.MaxSectionDepth);
    }

    [Fact]
    public async Task Create_then_get_returns_empty_document()
    {
        var created = await CreateWork();

        Assert.Equal("Trabalho sem título", created.Title);
        Assert.Equal(1, created.Version);

        var fetched = await _client.GetFromJsonAsync<WorkResponse>($"/api/works/{created.Id}", Json);
        Assert.Equal(DocumentJson.Serialize(created.Document), DocumentJson.Serialize(fetched!.Document));
        Assert.Equal("Introdução", fetched.Document.Textual.Sections[0].Title);
    }

    [Fact]
    public async Task Create_rejects_unknown_profile_and_missing_profile()
    {
        var unknown = await _client.PostAsJsonAsync("/api/works", new { profileId = "apa" });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.NotNull((await ReadProblem(unknown))["errors"]!["profileId"]);

        var missing = await _client.PostAsJsonAsync("/api/works", new { title = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task Save_golden_document_bumps_version_and_persists_jsonb()
    {
        var work = await CreateWork("Rascunho");

        var response = await SaveRaw(work.Id, work.Version, GoldenJson);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<SaveDocumentResponse>(Json))!;
        Assert.Equal(2, saved.Version);
        Assert.Equal("Formatação automática de trabalhos acadêmicos", saved.Title);

        var fetched = (await _client.GetFromJsonAsync<WorkResponse>($"/api/works/{work.Id}", Json))!;
        Assert.Equal(DocumentJson.Serialize(DocumentJson.Deserialize(GoldenJson)), DocumentJson.Serialize(fetched.Document));

        // Stored as real JSONB: queryable with PostgreSQL JSON operators.
        await using var conn = new NpgsqlConnection(factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "select document->'textual'->'sections'->2->'blocks'->0->>'type', jsonb_typeof(document), version from works where id = @id", conn);
        cmd.Parameters.AddWithValue("id", work.Id);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("table", reader.GetString(0));
        Assert.Equal("object", reader.GetString(1));
        Assert.Equal(2, reader.GetInt32(2));
    }

    [Fact]
    public async Task Save_sanitizes_text()
    {
        var work = await CreateWork();
        var doc = JsonNode.Parse(GoldenJson)!;
        doc["metadata"]!["title"] = "  Título\u0007 limpo  ";

        var response = await SaveRaw(work.Id, work.Version, doc.ToJsonString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = (await _client.GetFromJsonAsync<WorkResponse>($"/api/works/{work.Id}", Json))!;
        Assert.Equal("Título limpo", fetched.Document.Metadata.Title);
        Assert.Equal("Título limpo", fetched.Title);
    }

    [Fact]
    public async Task Save_with_stale_version_returns_409_with_current_version()
    {
        var work = await CreateWork();
        Assert.Equal(HttpStatusCode.OK, (await SaveRaw(work.Id, 1, GoldenJson)).StatusCode);

        var stale = await SaveRaw(work.Id, 1, GoldenJson);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(2, (int)(await ReadProblem(stale))["currentVersion"]!);
    }

    [Fact]
    public async Task Concurrent_saves_of_the_same_version_let_exactly_one_win()
    {
        var work = await CreateWork();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SaveRaw(work.Id, 1, GoldenJson)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        var fetched = (await _client.GetFromJsonAsync<WorkResponse>($"/api/works/{work.Id}", Json))!;
        Assert.Equal(2, fetched.Version);
    }

    [Fact]
    public async Task Save_returns_validation_errors_by_path()
    {
        var work = await CreateWork();
        var doc = JsonNode.Parse(GoldenJson)!;
        doc["references"] = new JsonArray(); // citations now point nowhere

        var response = await SaveRaw(work.Id, work.Version, doc.ToJsonString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadProblem(response))["errors"]!.AsObject();
        Assert.Contains("textual.sections[0].blocks[0].content[1].refIds[0]", errors.Select(e => e.Key));
    }

    [Fact]
    public async Task Save_rejects_malformed_document()
    {
        var work = await CreateWork();
        var doc = JsonNode.Parse(GoldenJson)!;
        doc["textual"]!["sections"]![0]!["blocks"]![0]!["type"] = "html";

        var response = await SaveRaw(work.Id, work.Version, doc.ToJsonString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Save_rejects_document_over_size_limit()
    {
        // ~2.4 MiB of valid content: under the 4 MiB request cap, over the 2 MiB document limit.
        // (The 413 request cap is enforced by Kestrel, which TestServer does not emulate.)
        var work = await CreateWork();
        var doc = JsonNode.Parse(GoldenJson)!;
        var blocks = new JsonArray();
        for (var i = 0; i < 300; i++)
        {
            blocks.Add(new JsonObject
            {
                ["type"] = "paragraph",
                ["id"] = $"big-{i}",
                ["content"] = new JsonArray(new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = new string('a', 8_000),
                    ["marks"] = new JsonArray(),
                }),
            });
        }
        doc["textual"]!["sections"]![3]!["blocks"] = blocks;

        var response = await SaveRaw(work.Id, work.Version, doc.ToJsonString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadProblem(response))["errors"]!.AsObject();
        Assert.Contains("KiB", errors[""]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task List_returns_most_recently_updated_first()
    {
        var older = await CreateWork("Mais antigo");
        var newer = await CreateWork("Mais novo");
        Assert.Equal(HttpStatusCode.OK, (await SaveRaw(older.Id, 1, GoldenJson)).StatusCode);

        var list = (await _client.GetFromJsonAsync<List<WorkSummaryResponse>>("/api/works", Json))!;

        var ids = list.Select(w => w.Id).ToList();
        Assert.True(ids.IndexOf(older.Id) < ids.IndexOf(newer.Id));
    }

    [Fact]
    public async Task Delete_removes_work()
    {
        var work = await CreateWork();

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/works/{work.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/works/{work.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/works/{work.Id}")).StatusCode);
    }
}
