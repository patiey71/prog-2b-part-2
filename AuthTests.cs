using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using static RaceDay.Tests.TestHelpers;

namespace RaceDay.Tests;

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public AuthTests(ApiFactory f) => _f = f;

    private static object Reg(string email, string role = "Participant", string password = Password) =>
        new { firstName = "Sam", lastName = "Runner", email, password, role };

    [Fact]
    public async Task Register_ValidParticipant_Returns201()
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", Reg($"{Guid.NewGuid():N}@t.com"), Json);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ProfileResponse>(Json);
        Assert.Equal("Participant", body!.Role);
    }

    [Fact]
    public async Task Register_ValidOrganiser_Returns201()
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", Reg($"{Guid.NewGuid():N}@t.com", "Organiser"), Json);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _f.CreateClient();
        var email = $"{Guid.NewGuid():N}@t.com";
        await client.PostAsJsonAsync("/api/auth/register", Reg(email), Json);
        var res = await client.PostAsJsonAsync("/api/auth/register", Reg(email), Json);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("0")]
    [InlineData("")]
    public async Task Register_InvalidRole_Returns400(string role)
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", Reg($"{Guid.NewGuid():N}@t.com", role), Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", Reg($"{Guid.NewGuid():N}@t.com", password: "short"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", Reg("not-an-email"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var email = $"{Guid.NewGuid():N}@t.com";
        await _f.CreateClient().PostAsJsonAsync("/api/auth/register", Reg(email), Json);

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(u => u.Email == email);

        Assert.NotEqual(Password, user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash));
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithRole()
    {
        var client = _f.CreateClient();
        var email = $"{Guid.NewGuid():N}@t.com";
        await client.PostAsJsonAsync("/api/auth/register", Reg(email, "Organiser"), Json);

        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password }, Json);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ProfileResponse>(Json);
        Assert.Equal("Organiser", body!.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _f.CreateClient();
        var email = $"{Guid.NewGuid():N}@t.com";
        await client.PostAsJsonAsync("/api/auth/register", Reg(email), Json);

        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass1!" }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@t.com", password = Password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Profile_Unauthenticated_Returns401()
    {
        var res = await _f.CreateClient().GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Profile_LoggedIn_CanViewAndUpdateOwnProfile()
    {
        var (client, email, _) = await NewUserAsync(_f, "Participant");

        var get = await client.GetFromJsonAsync<ProfileResponse>("/api/profile", Json);
        Assert.Equal(email, get!.Email);

        var put = await client.PutAsJsonAsync("/api/profile", new { firstName = "Updated", lastName = "Name", phoneNumber = "0821234567" }, Json);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await put.Content.ReadFromJsonAsync<ProfileResponse>(Json);
        Assert.Equal("Updated", updated!.FirstName);
        Assert.Equal("Participant", updated.Role);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var (client, _, _) = await NewUserAsync(_f, "Participant");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
    }
}
