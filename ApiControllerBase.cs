using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user, taken from the session cookie's claims.</summary>
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
