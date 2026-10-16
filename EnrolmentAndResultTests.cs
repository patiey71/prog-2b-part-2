using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using static RaceDay.Tests.TestHelpers;

namespace RaceDay.Tests;

public class EnrolmentAndResultTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public EnrolmentAndResultTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecorded()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, pId) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.Id, "21km");

        var res = await p.PostAsJsonAsync($"/api/events/{ev.Id}/enrolments", new { categoryId = cat.Id }, Json);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var enrolment = await res.Content.ReadFromJsonAsync<EnrolmentResponse>(Json);
        Assert.Equal(pId, enrolment!.ParticipantId);
        Assert.Equal(ev.Id, enrolment.EventId);
        Assert.Equal(cat.Id, enrolment.CategoryId);

        var mine = await p.GetFromJsonAsync<List<EnrolmentResponse>>("/api/enrolments/mine", Json);
        Assert.Single(mine!);
    }

    [Fact]
    public async Task Organiser_CanViewAllEnrolmentsForOwnEvent()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p1, _, _) = await NewUserAsync(_f, "Participant");
        var (p2, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.Id);
        await EnrolAsync(p1, ev.Id, cat.Id);
        await EnrolAsync(p2, ev.Id, cat.Id);

        var all = await org.GetFromJsonAsync<List<EnrolmentResponse>>($"/api/events/{ev.Id}/enrolments", Json);
        Assert.Equal(2, all!.Count);
    }

    [Fact]
    public async Task Enrol_Twice_Returns409()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.Id);
        await EnrolAsync(p, ev.Id, cat.Id);

        var res = await p.PostAsJsonAsync($"/api/events/{ev.Id}/enrolments", new { categoryId = cat.Id }, Json);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_Returns400()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev1 = await CreateEventAsync(org, "Event 1");
        var ev2 = await CreateEventAsync(org, "Event 2");
        var catOfEv2 = await CreateCategoryAsync(org, ev2.Id);

        var res = await p.PostAsJsonAsync($"/api/events/{ev1.Id}/enrolments", new { categoryId = catOfEv2.Id }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_MissingEvent_Returns404()
    {
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var res = await p.PostAsJsonAsync("/api/events/999999/enrolments", new { categoryId = 1 }, Json);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCaptureResult_ParticipantCanViewOwn()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.Id);
        var enrol = await EnrolAsync(p, ev.Id, cat.Id);

        var capture = await org.PostAsJsonAsync($"/api/events/{ev.Id}/results",
            new { enrolmentId = enrol.Id, finishTime = "00:42:15", position = 3 }, Json);
        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);

        var mine = await p.GetFromJsonAsync<List<ResultResponse>>("/api/results/mine", Json);
        var result = Assert.Single(mine!);
        Assert.Equal(3, result.Position);
        Assert.Equal(new TimeSpan(0, 42, 15), result.FinishTime);
    }

    [Fact]
    public async Task CaptureResult_Twice_UpdatesExistingResult()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.Id);
        var enrol = await EnrolAsync(p, ev.Id, cat.Id);

        await org.PostAsJsonAsync($"/api/events/{ev.Id}/results", new { enrolmentId = enrol.Id, finishTime = "00:50:00", position = 5 }, Json);
        var second = await org.PostAsJsonAsync($"/api/events/{ev.Id}/results", new { enrolmentId = enrol.Id, finishTime = "00:45:00", position = 2 }, Json);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var all = await org.GetFromJsonAsync<List<ResultResponse>>($"/api/events/{ev.Id}/results", Json);
        var only = Assert.Single(all!);
        Assert.Equal(2, only.Position);
    }

    [Fact]
    public async Task CaptureResult_ForEnrolmentOfDifferentEvent_Returns400()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev1 = await CreateEventAsync(org, "A");
        var ev2 = await CreateEventAsync(org, "B");
        var cat = await CreateCategoryAsync(org, ev1.Id);
        var enrol = await EnrolAsync(p, ev1.Id, cat.Id);

        var res = await org.PostAsJsonAsync($"/api/events/{ev2.Id}/results",
            new { enrolmentId = enrol.Id, finishTime = "00:30:00", position = 1 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Participant_OnlySeesOwnResults()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p1, _, _) = await NewUserAsync(_f, "Participant");
        var (p2, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.Id);
        var e1 = await EnrolAsync(p1, ev.Id, cat.Id);
        var e2 = await EnrolAsync(p2, ev.Id, cat.Id);

        await org.PostAsJsonAsync($"/api/events/{ev.Id}/results", new { enrolmentId = e1.Id, finishTime = "00:30:00", position = 1 }, Json);
        await org.PostAsJsonAsync($"/api/events/{ev.Id}/results", new { enrolmentId = e2.Id, finishTime = "00:31:00", position = 2 }, Json);

        var seenByP1 = await p1.GetFromJsonAsync<List<ResultResponse>>("/api/results/mine", Json);
        var only = Assert.Single(seenByP1!);
        Assert.Equal(e1.Id, only.EnrolmentId);
    }

    [Fact]
    public async Task Participant_CanWithdraw_ButNotOnceResultExists()
    {
        var (org, _, _) = await NewUserAsync(_f, "Organiser");
        var (p, _, _) = await NewUserAsync(_f, "Participant");
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.Id);
        var enrol = await EnrolAsync(p, ev.Id, cat.Id);
        await org.PostAsJsonAsync($"/api/events/{ev.Id}/results", new { enrolmentId = enrol.Id, finishTime = "00:30:00", position = 1 }, Json);

        Assert.Equal(HttpStatusCode.Conflict, (await p.DeleteAsync($"/api/enrolments/{enrol.Id}")).StatusCode);
    }
}
