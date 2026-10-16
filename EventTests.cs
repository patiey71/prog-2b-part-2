using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using static RaceDay.Tests.TestHelpers;

namespace RaceDay.Tests;

public class EventTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public EventTests(ApiFactory f) => _f = f;

    private static object EventBody(string name = "City 10km") => new
    {
        name, description = "Road race", date = DateTime.UtcNow.AddDays(10),
        location = "Waterfront", distanceKm = 10, eventType = "Run"
    };

    [Fact]
    public async Task Organiser_CanCreateEvent()
    {
        var (org, _, orgId) = await NewUserAsync(_f, "Organiser");
        var res = await org.PostAsJsonAsync("/api/events", EventBody(), Json);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var ev = await res.Content.ReadFromJsonAsync<EventResponse>(Json);
        Assert.Equal(orgId, ev!.OrganiserId);
        Assert.Equal("City 10km", ev.Name);
    }

    [Fact]
    public async Task Organiser_CreateEvent_MissingName_Returns400()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var res = await org.PostAsJsonAsync("/api/events",
            new { description = "x", date = DateTime.UtcNow, location = "y", distanceKm = 5, eventType = "Run" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreateEvent_InvalidEventType_Returns400()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var res = await org.PostAsJsonAsync("/api/events",
            new { name = "x", description = "x", date = DateTime.UtcNow, location = "y", distanceKm = 5, eventType = "Swim" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var ev = await CreateEventAsync(org);

        var res = await org.PutAsJsonAsync($"/api/events/{ev.Id}", EventBody("Renamed Event"), Json);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = await res.Content.ReadFromJsonAsync<EventResponse>(Json);
        Assert.Equal("Renamed Event", updated!.Name);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var ev = await CreateEventAsync(org);

        Assert.Equal(HttpStatusCode.NoContent, (await org.DeleteAsync($"/api/events/{ev.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await org.GetAsync($"/api/events/{ev.Id}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_DeleteEvent_RemovesEnrolmentsAndCategories()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.Id);
        await EnrolAsync(p, ev.Id, cat.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await org.DeleteAsync($"/api/events/{ev.Id}")).StatusCode);

        var mine = await p.GetFromJsonAsync<List<EnrolmentResponse>>("/api/enrolments/mine", Json);
        Assert.Empty(mine!);
    }

    [Fact]
    public async Task Organiser_CannotUpdateOrDeleteAnotherOrganisersEvent()
    {
        var (owner, _, _) = await NewUserAsync(_f, "Organiser");
        var (other, _, _) = await NewUserAsync(_f, "Organiser");
        var ev = await CreateEventAsync(owner);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/events/{ev.Id}", EventBody(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/events/{ev.Id}")).StatusCode);
    }

    [Fact]
    public async Task UpdateMissingEvent_Returns404()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        Assert.Equal(HttpStatusCode.NotFound, (await org.PutAsJsonAsync("/api/events/999999", EventBody(), Json)).StatusCode);
    }

    [Fact]
    public async Task BothRoles_CanViewEvents()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);

        foreach (var client in new[] { org, p })
        {
            var list = await client.GetFromJsonAsync<List<EventResponse>>("/api/events", Json);
            Assert.Contains(list!, e => e.Id == ev.Id);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/events/{ev.Id}")).StatusCode);
        }
    }

    [Fact]
    public async Task Unauthenticated_CannotViewEvents()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/events")).StatusCode);
    }

    [Fact]
    public async Task BothRoles_CanViewCategories_OrganiserCanDefineThem()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);
        await CreateCategoryAsync(org, ev.Id, "Under 20");
        await CreateCategoryAsync(org, ev.Id, "Senior");

        var seen = await p.GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{ev.Id}/categories", Json);
        Assert.Equal(2, seen!.Count);
    }

    [Fact]
    public async Task DuplicateCategory_Returns409()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var ev = await CreateEventAsync(org);
        await CreateCategoryAsync(org, ev.Id, "10km");

        var res = await org.PostAsJsonAsync($"/api/events/{ev.Id}/categories", new { name = "10km" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}
