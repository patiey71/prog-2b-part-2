using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Boots the real API in memory, swapping SQL Server for an in-memory database.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}

public static class TestHelpers
{
    public const string Password = "Passw0rd!";

    public static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Registers and logs in a user; the returned client keeps the session cookie.</summary>
    public static async Task<(HttpClient Client, string Email, int UserId)> NewUserAsync(ApiFactory f, string role)
    {
        var client = f.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.com";

        var reg = await client.PostAsJsonAsync("/api/auth/register",
            new { firstName = "Test", lastName = role, email, password = Password, role }, Json);
        reg.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password }, Json);
        login.EnsureSuccessStatusCode();
        var profile = await login.Content.ReadFromJsonAsync<ProfileResponse>(Json);
        return (client, email, profile!.Id);
    }

    public static async Task<EventResponse> CreateEventAsync(HttpClient organiser, string name = "Park Run")
    {
        var r = await organiser.PostAsJsonAsync("/api/events", new
        {
            name,
            description = "A friendly 5km fun run",
            date = DateTime.UtcNow.AddDays(30),
            location = "Central Park",
            distanceKm = 5,
            eventType = "Run"
        }, Json);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<EventResponse>(Json))!;
    }

    public static async Task<CategoryResponse> CreateCategoryAsync(HttpClient organiser, int eventId, string name = "Senior")
    {
        var r = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new { name }, Json);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CategoryResponse>(Json))!;
    }

    public static async Task<EnrolmentResponse> EnrolAsync(HttpClient participant, int eventId, int categoryId)
    {
        var r = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId }, Json);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<EnrolmentResponse>(Json))!;
    }
}
