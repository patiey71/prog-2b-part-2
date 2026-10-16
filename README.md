# RaceDay API (PROG6212 POE – Part 2)

ASP.NET Core 8 Web API for the RaceDay system. Uses Entity Framework Core (Code-First) with SQL Server,
cookie-based session authentication with two roles (Organiser, Participant), Swagger, xUnit tests and a GitHub Actions CI pipeline.

[![CI](https://github.com/<your-username>/<your-repo>/actions/workflows/ci.yml/badge.svg)](https://github.com/<your-username>/<your-repo>/actions)

## Setup and run

Prerequisites: .NET 8 SDK, Visual Studio 2022 (or VS Code), SQL Server / LocalDB.

1. Clone the repo and open `RaceDay.sln`.
2. Check the connection string in `RaceDay.Api/appsettings.json` (defaults to LocalDB, database `RaceDayDb`).
3. Create the database from the code-first model:
   ```
   dotnet tool install --global dotnet-ef
   cd RaceDay.Api
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
   (If no migration exists the API creates the schema automatically on first start.)
4. Run: `dotnet run --project RaceDay.Api`, then open `http://localhost:5080/swagger`.
5. Run tests: `dotnet test RaceDay.sln`

Using Swagger: call `POST /api/auth/register`, then `POST /api/auth/login`. The session cookie is then sent
automatically by Swagger UI for every other request.

## Roles

| Role | Can do |
|------|--------|
| **Organiser** | Create, update and delete their own events; define categories for their events; view all enrolments for their events; capture finish times and positions; view/update own profile; view events and categories. |
| **Participant** | Register and log in; view/update own profile; view events and categories; enrol in an event by selecting a category; view and withdraw own enrolments; view own results. |

Organiser functionality returns **403** for Participants and vice versa; unauthenticated requests return **401**.
An Organiser can only manage events they created.
Optional: set `Registration:OrganiserCode` in configuration so only people with the code can register as Organisers.

## Endpoints

| Method | Route | Access |
|--------|-------|--------|
| POST | /api/auth/register | Public |
| POST | /api/auth/login | Public |
| POST | /api/auth/logout | Authenticated |
| GET / PUT | /api/profile | Authenticated |
| GET | /api/events, /api/events/{id} | Authenticated |
| POST / PUT / DELETE | /api/events, /api/events/{id} | Organiser (owner) |
| GET | /api/events/{eventId}/categories | Authenticated |
| POST | /api/events/{eventId}/categories | Organiser (owner) |
| DELETE | /api/categories/{id} | Organiser (owner) |
| POST | /api/events/{eventId}/enrolments | Participant |
| GET | /api/enrolments/mine | Participant |
| DELETE | /api/enrolments/{id} | Participant (own) |
| GET | /api/events/{eventId}/enrolments | Organiser (owner) |
| POST / GET | /api/events/{eventId}/results | Organiser (owner) |
| GET | /api/results/mine | Participant |

## Security

- Passwords are hashed with BCrypt; plain-text passwords are never stored.
- Session state (user id and role) is held in a signed, HttpOnly cookie.
- Role checks use `[Authorize(Roles = ...)]`; ownership checks stop one Organiser editing another's events.

## CI/CD

`.github/workflows/ci.yml` builds the solution and runs all unit tests on every push.

![Green build](docs/ci-green-build.png)

*(Add your own screenshot of the passing GitHub Actions run at `docs/ci-green-build.png`.)*

## Demo video

YouTube (unlisted): <paste link here>
