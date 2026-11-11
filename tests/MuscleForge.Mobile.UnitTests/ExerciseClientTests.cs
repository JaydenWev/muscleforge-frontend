using System.Net;
using System.Text.Json;
using MuscleForge.Client;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public partial class GeneratedApiClientTests
{
    private static string ExerciseJson(Guid id, bool archived = false) =>
        JsonSerializer.Serialize(new
        {
            id, name = "Press", equipment = "Dumbbells", trackingType = "ExternalWeightAndReps",
            loadConvention = "PerDumbbell", isArchived = archived,
            archivedAt = archived ? new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero) : (DateTimeOffset?)null
        });

    [Fact]
    public async Task ExerciseCrudPreservesPrefixBearerAndStringEnumsIncludingCreatedResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var calls = new List<HttpMethod>();
        using var http = new HttpClient(new Handler(async (request, cancellation) =>
        {
            calls.Add(request.Method);
            Assert.StartsWith("/prefix/api/v1/exercises", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("synthetic-exercise-access", request.Headers.Authorization.Parameter);
            if (request.Method == HttpMethod.Post || request.Method == HttpMethod.Put)
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
                Assert.Equal("ExternalWeightAndReps", body.RootElement.GetProperty("trackingType").GetString());
                Assert.Equal("PerDumbbell", body.RootElement.GetProperty("loadConvention").GetString());
                Assert.False(body.RootElement.TryGetProperty("ownerId", out _));
                if (request.Method == HttpMethod.Put) Assert.True(body.RootElement.GetProperty("isArchived").GetBoolean());
            }
            return Json(request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK,
                ExerciseJson(id, request.Method == HttpMethod.Put));
        }));
        var client = new MuscleForgeApiClient(http);
        var root = new Uri("https://example.test/prefix/");
        const string token = "synthetic-exercise-access";
        var created = await client.CreateExerciseAsync(root, token, new CreateExerciseRequest
        {
            Name = "Press", Equipment = "Dumbbells", LoadConvention = CreateExerciseRequestLoadConvention.PerDumbbell
        }, ct);
        Assert.Equal(id, created.Id);
        var fetched = await client.GetExerciseAsync(root, token, id, ct);
        Assert.False(fetched.IsArchived);
        var archived = await client.UpdateExerciseAsync(root, token, id, new UpdateExerciseRequest
        {
            Name = "Press", Equipment = "Dumbbells", LoadConvention = UpdateExerciseRequestLoadConvention.PerDumbbell,
            IsArchived = true
        }, ct);
        Assert.True(archived.IsArchived);
        Assert.NotNull(archived.ArchivedAt);
        Assert.Equal(new[] { HttpMethod.Post, HttpMethod.Get, HttpMethod.Put }, calls);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task ExerciseListEscapesSearchAndReadsNumericPaging()
    {
        const string search = "a%_\\ /&";
        using var http = new HttpClient(new Handler((request, _) =>
        {
            Assert.Contains("search=" + Uri.EscapeDataString(search), request.RequestUri!.OriginalString);
            Assert.Contains("offset=3", request.RequestUri.Query);
            Assert.Contains("limit=25", request.RequestUri.Query);
            Assert.Equal("synthetic-list-access", request.Headers.Authorization!.Parameter);
            return Task.FromResult(Json(HttpStatusCode.OK, """{"items":[],"offset":3,"limit":25,"totalCount":0}"""));
        }));
        var result = await new MuscleForgeApiClient(http).ListExercisesAsync(new Uri("https://example.test/"),
            "synthetic-list-access", search, true, 3, 25, TestContext.Current.CancellationToken);
        Assert.Empty(result.Items);
        Assert.Equal(3, result.Offset);
        Assert.Equal(25, result.Limit);
    }

    [Theory]
    [InlineData(400, "validation_failed")]
    [InlineData(404, "resource_not_found")]
    [InlineData(409, "state_conflict")]
    public async Task ExerciseFailuresPreserveProblemAndHideSubmittedData(int status, string code)
    {
        var problem = JsonSerializer.Serialize(new
        {
            code, traceId = "trace", errors = new Dictionary<string, string[]> { ["name"] = ["Name is required."] }
        });
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json((HttpStatusCode)status,
            problem, "application/problem+json"))));
        var error = await Assert.ThrowsAsync<ApiException>(() => new MuscleForgeApiClient(http).UpdateExerciseAsync(
            new Uri("https://example.test/"), "synthetic-access", Guid.NewGuid(), new UpdateExerciseRequest
            { Name = "private-exercise-name", Equipment = "Dumbbells", IsArchived = false }, TestContext.Current.CancellationToken));
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.Equal(code, error.Problem!.Code);
        Assert.Equal("Name is required.", error.Problem.Errors!["name"][0]);
        Assert.DoesNotContain("private-exercise-name", error.ToString());
        Assert.DoesNotContain("synthetic-access", error.ToString());
    }

    [Fact]
    public async Task MalformedCreatedBodyIsSanitized()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(HttpStatusCode.Created, "private-catalog-body"))));
        var error = await Assert.ThrowsAsync<JsonException>(() => new MuscleForgeApiClient(http).CreateExerciseAsync(
            new Uri("https://example.test/"), "synthetic-access", new CreateExerciseRequest
            { Name = "Press", Equipment = "Dumbbells" }, TestContext.Current.CancellationToken));
        Assert.DoesNotContain("private-catalog-body", error.ToString());
    }
}
