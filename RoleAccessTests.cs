using System.Net;
using System.Net.Http.Json;
using static RaceDay.Tests.TestHelpers;

namespace RaceDay.Tests;

/// <summary>Verifies Organiser-only and Participant-only functionality rejects the other role.</summary>
public class RoleAccessTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public RoleAccessTests(ApiFactory f) => _f = f;

    private static object EventBody => new
    {
        name = "X", description = "x", date = DateTime.UtcNow.AddDays(5),
        location = "Y", distanceKm = 5, eventType = "Walk"
    };

    [Fact]
    public async Task Participant_CannotCreateUpdateOrDeleteEvents()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);

        Assert.Equal(HttpStatusCode.Forbidden, (await p.PostAsJsonAsync("/api/events", EventBody, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await p.PutAsJsonAsync($"/api/events/{ev.Id}", EventBody, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await p.DeleteAsync($"/api/events/{ev.Id}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateCategories()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);

        var res = await p.PostAsJsonAsync($"/api/events/{ev.Id}/categories", new { name = "Hack" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments_OrCaptureResults()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.Id);
        var enrol = await EnrolAsync(p, ev.Id, cat.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await p.GetAsync($"/api/events/{ev.Id}/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await p.GetAsync($"/api/events/{ev.Id}/results")).StatusCode);

        var capture = await p.PostAsJsonAsync($"/api/events/{ev.Id}/results",
            new { enrolmentId = enrol.Id, finishTime = "00:30:00", position = 1 }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, capture.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_OrUseParticipantOnlyEndpoints()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.Id);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await org.PostAsJsonAsync($"/api/events/{ev.Id}/enrolments", new { categoryId = cat.Id }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.GetAsync("/api/enrolments/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.GetAsync("/api/results/mine")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotSeeAnotherOrganisersEnrolmentsOrResults()
    {
        var (owner, _, _) = await NewUserAsync(_f, "Organiser");
        var (other, _, _) = await NewUserAsync(_f, "Organiser");
        var ev = await CreateEventAsync(owner);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/events/{ev.Id}/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/events/{ev.Id}/results")).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_IsRejectedEverywhere()
    {
        var anon = _f.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/events", EventBody, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/enrolments/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/results/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/auth/logout", null)).StatusCode);
    }
}
