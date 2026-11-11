using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Domain.Documents;
using DTOs.Assets;
using DTOs.Works;

namespace IntegrationTests;

public sealed class AssetsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = DocumentJson.Options;
    private readonly HttpClient _client = factory.CreateClient();

    public static byte[] GoldenImage => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", "fluxo-exportacao.png"));

    public static Task<HttpResponseMessage> Upload(HttpClient client, Guid workId, byte[] content, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return client.PostAsync($"/api/works/{workId}/assets", form);
    }

    /// <summary>Uploads the golden figure image and returns its assetId.</summary>
    public static async Task<string> UploadGoldenImage(HttpClient client, Guid workId)
    {
        var response = await Upload(client, workId, GoldenImage, "fluxo.png", "image/png");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssetResponse>(Json))!.Id;
    }

    private async Task<WorkResponse> NewWork()
    {
        var created = await _client.PostAsJsonAsync("/api/works", new { profileId = "abnt-tcc" });
        return (await created.Content.ReadFromJsonAsync<WorkResponse>(Json))!;
    }

    private static async Task<string[]> ErrorKeys(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["errors"]!.AsObject().Select(e => e.Key).ToArray();

    [Fact]
    public async Task Uploads_png_and_serves_it_back()
    {
        var work = await NewWork();

        var response = await Upload(_client, work.Id, GoldenImage, "Fluxo de exportação.png", "image/png");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var asset = (await response.Content.ReadFromJsonAsync<AssetResponse>(Json))!;
        Assert.Equal(("image/png", 600, 240), (asset.ContentType, asset.Width, asset.Height));
        Assert.Equal(32, asset.Id.Length); // usable as a document id
        Assert.Equal("Fluxo de exportação.png", asset.OriginalName);
        Assert.Equal($"/api/assets/{asset.Id}", response.Headers.Location!.AbsolutePath);

        var file = await _client.GetAsync(asset.Url);
        Assert.Equal("image/png", file.Content.Headers.ContentType!.MediaType);
        Assert.Equal(GoldenImage, await file.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", file.Headers.GetValues("X-Content-Type-Options").Single());

        var list = (await _client.GetFromJsonAsync<List<AssetResponse>>($"/api/works/{work.Id}/assets", Json))!;
        Assert.Equal([asset.Id], list.Select(a => a.Id));
    }

    [Fact]
    public async Task Detects_type_from_content_not_from_name_or_header()
    {
        var work = await NewWork();

        var svg = System.Text.Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");
        var fake = await Upload(_client, work.Id, svg, "foto.png", "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
        Assert.Equal(["file"], await ErrorKeys(fake));

        var png = await Upload(_client, work.Id, GoldenImage, "foto.jpg", "application/octet-stream");
        Assert.Equal(HttpStatusCode.Created, png.StatusCode);
        Assert.Equal("image/png", (await png.Content.ReadFromJsonAsync<AssetResponse>(Json))!.ContentType);
    }

    [Fact]
    public async Task Rejects_files_over_the_size_limit()
    {
        var work = await NewWork();
        var big = new byte[250_000];
        GoldenImage.CopyTo(big, 0); // valid PNG header, but over the 200 kB test limit

        var response = await Upload(_client, work.Id, big, "grande.png", "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["file"], await ErrorKeys(response));
    }

    [Fact]
    public async Task Unknown_work_or_asset_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Upload(_client, Guid.NewGuid(), GoldenImage, "a.png", "image/png")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/assets/not-a-guid")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/assets/{Guid.NewGuid():N}")).StatusCode);
    }
}
