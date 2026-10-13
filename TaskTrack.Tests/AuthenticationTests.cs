using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TaskTrack.Repo.Data;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Services;
using Xunit;

namespace TaskTrack.Tests;

// A fresh factory/schema per test keeps state-changing scenarios independent.
public sealed class AuthenticationTests : IAsyncLifetime
{
    private readonly TestApplication app = new();
    private HttpClient client = null!;
    public async Task InitializeAsync() { await app.InitializeAsync(); client = app.CreateClient(); }
    public async Task DisposeAsync() { client.Dispose(); await ((IAsyncLifetime)app).DisposeAsync(); }
    private async Task<SessionDto> Login(string email) => (await (await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestApplication.Password })).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<SessionDto>())!;
    private async Task<AccountDto> Register(string email = "staff@integration.test") => (await (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Integration Staff", email, password = TestApplication.Password, role = 1 })).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<AccountDto>())!;
    private void Bearer(SessionDto session) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

    [Fact]
    public async Task Registration_validation_uniqueness_and_password_storage()
    {
        var staff = await Register(" Staff@integration.test "); Assert.Equal(0, staff.Role);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Duplicate", email = "STAFF@integration.test", password = TestApplication.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { fullName = " ", email = "invalid", password = "short" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = staff.Email, password = "wrong" })).StatusCode);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync("/api/auth/register", new { fullName = "Race", email = "race@integration.test", password = TestApplication.Password })));
        Assert.Single(concurrent, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(concurrent, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TaskManagementDbContext>();
        var account = await db.Accounts.FindAsync(staff.AccountId); Assert.NotNull(account); Assert.NotEqual(TestApplication.Password, account.PasswordHash); Assert.True(PasswordHasher.Verify(TestApplication.Password, account.PasswordHash));
        var session = await Login(staff.Email); var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        Assert.Contains(jwt.Claims, c => c.Type == "AccountID"); Assert.Contains(jwt.Claims, c => c.Type == "Role" && c.Value == "Staff"); Assert.Contains(jwt.Claims, c => c.Type == "exp");
    }

    [Fact]
    public async Task Public_reads_and_all_write_endpoints_enforce_authentication()
    {
        foreach (var path in new[] { "departments", "projects", "tasks", "tags", "departments/search?name=", "projects/search", "tasks/search", "projects/department/1", "tasks/project/1", "departments/1", "projects/1", "tasks/1" }) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/" + path)).StatusCode);
        foreach (var resource in new[] { "departments", "projects", "tasks", "tags" })
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            var path = "/api/" + resource + (method == HttpMethod.Post ? "" : "/1");
            using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(new { }) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
        }
        await Register(); Bearer(await Login("staff@integration.test"));
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, "/api/accounts/" + app.AdminId) { Content = method == HttpMethod.Put ? JsonContent.Create(new { role = 0 }) : null };
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/accounts")).StatusCode);
        Bearer(await Login(TestApplication.AdminEmail)); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/accounts")).StatusCode);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Both_roles_can_manage_resources_and_audit_is_server_owned(bool admin)
    {
        var account = admin ? app.AdminId : (await Register()).AccountId;
        Bearer(await Login(admin ? TestApplication.AdminEmail : "staff@integration.test"));
        var department = await (await client.PostAsJsonAsync("/api/departments", new { departmentName = "Test department", departmentDescription = "Description" })).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<DepartmentListDto>();
        var project = await (await client.PostAsJsonAsync("/api/projects", new { projectName = "Test project", departmentId = department!.DepartmentId, startDate = "2026-10-13", status = 0, createdById = 999 })).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<ProjectListDto>(); Assert.Equal(account, project!.CreatedById);
        var tag = await (await client.PostAsJsonAsync("/api/tags", new { tagName = "Integration tag", color = "#123456" })).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<TagDto>();
        var taskRequest = new { title = "Test task", projectId = project.ProjectId, tagIds = new[] { tag!.TagId }, createdById = 999, updatedById = 999 };
        var task = await (await client.PostAsJsonAsync("/api/tasks", taskRequest)).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<TaskDetailDto>(); Assert.Equal(account, task!.CreatedById); Assert.Single(task.Tags);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/tasks/{task.TaskId}", taskRequest)).StatusCode);
        Assert.Equal(account, (await client.GetFromJsonAsync<TaskDetailDto>($"/api/tasks/{task.TaskId}"))!.UpdatedById);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/projects/{project.ProjectId}", new { projectName = "Updated", departmentId = department.DepartmentId, startDate = "2026-10-13", status = 1 })).StatusCode);
        Assert.Equal(account, (await client.GetFromJsonAsync<ProjectDetailDto>($"/api/projects/{project.ProjectId}"))!.UpdatedById);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/departments/{department.DepartmentId}", new { departmentName = "Updated", departmentDescription = "Updated" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/tags/{tag.TagId}", new { tagName = "Updated tag", color = "#654321" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tasks/{task.TaskId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tasks/{task.TaskId}")).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TaskManagementDbContext>();
        var archived = await db.Tasks.FindAsync(task.TaskId); Assert.False(archived!.IsActive); Assert.Equal(account, archived.UpdatedById);
        // Existing Ass1 deletion rules retain references to archived tasks.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/projects/{project.ProjectId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/tags/{tag.TagId}")).StatusCode);
        var unused = await (await client.PostAsJsonAsync("/api/tags", new { tagName = "Unused" })).Content.ReadFromJsonAsync<TagDto>(); Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tags/{unused!.TagId}")).StatusCode);
        var emptyProject = await (await client.PostAsJsonAsync("/api/projects", new { projectName = "Empty", departmentId = department.DepartmentId, startDate = "2026-10-13" })).Content.ReadFromJsonAsync<ProjectListDto>(); Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/projects/{emptyProject!.ProjectId}")).StatusCode);
        var emptyDepartment = await (await client.PostAsJsonAsync("/api/departments", new { departmentName = "Empty", departmentDescription = "Empty" })).Content.ReadFromJsonAsync<DepartmentListDto>(); Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/departments/{emptyDepartment!.DepartmentId}")).StatusCode);
    }

    [Fact]
    public async Task Account_deletion_checks_archived_tasks_and_last_admin()
    {
        var staff = await Register(); var deletable = await Register("deletable@integration.test");
        Bearer(await Login("staff@integration.test"));
        var task = await (await client.PostAsJsonAsync("/api/tasks", new { title = "Creator test", projectId = 1 })).Content.ReadFromJsonAsync<TaskDetailDto>();
        Bearer(await Login(TestApplication.AdminEmail));
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/accounts/{staff.AccountId}")).StatusCode);
        await client.DeleteAsync($"/api/tasks/{task!.TaskId}"); Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/accounts/{staff.AccountId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/accounts/{app.AdminId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/accounts/{app.AdminId}", new { role = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/accounts/999999")).StatusCode);
        var deletedSession = await Login(deletable.Email);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/accounts/{deletable.AccountId}")).StatusCode);
        Bearer(deletedSession); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Rotation_replay_logout_profile_password_and_role_invalidate_sessions()
    {
        var staff = await Register(); var first = await Login(staff.Email);
        var rotated = await (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken })).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<SessionDto>();
        Assert.NotEqual(first.RefreshToken, rotated!.RefreshToken); Assert.Equal(first.RefreshExpiresAt, rotated.RefreshExpiresAt);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = rotated.RefreshToken })).StatusCode);
        var loggedOut = await Login(staff.Email); await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = loggedOut.RefreshToken }); Bearer(loggedOut); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var changedRole = await Login(staff.Email); Bearer(await Login(TestApplication.AdminEmail));
        await client.PutAsJsonAsync($"/api/accounts/{staff.AccountId}", new { fullName = "Renamed", role = 1 }); Bearer(changedRole);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = changedRole.RefreshToken })).StatusCode);
        var passwordSession = await Login(staff.Email); Bearer(passwordSession);
        var profile = await (await client.PutAsJsonAsync("/api/auth/profile", new { fullName = "Profile name" })).Content.ReadFromJsonAsync<AccountDto>(); Assert.Equal("Profile name", profile!.FullName);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/auth/password", new { currentPassword = "wrong", newPassword = "ChangedPass@123" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/auth/password", new { currentPassword = TestApplication.Password, newPassword = "ChangedPass@123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = passwordSession.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = staff.Email, password = "ChangedPass@123" })).StatusCode);
    }

    [Fact]
    public async Task Invalid_and_expired_tokens_are_rejected_and_upgrade_is_repeatable()
    {
        var session = await Login(TestApplication.AdminEmail);
        var original = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        var expired = new JwtSecurityToken(original.Issuer, "TaskTrack.Frontend", original.Claims.Where(c => c.Type != "exp" && c.Type != "nbf"), DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(app.JwtSecret)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(expired)); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken + "invalid"); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        await using var connection = new NpgsqlConnection(app.ConnectionString); await connection.OpenAsync();
        var count = await new NpgsqlCommand("SELECT count(*) FROM \"Task\"", connection).ExecuteScalarAsync();
        await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "database", "Assignment2_upgrade.sql")), connection).ExecuteNonQueryAsync();
        Assert.Equal(count, await new NpgsqlCommand("SELECT count(*) FROM \"Task\"", connection).ExecuteScalarAsync());
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TaskManagementDbContext>(); Assert.True(await db.Tasks.AllAsync(t => t.CreatedById == null));
    }

    [Fact]
    public async Task Concurrent_admin_demotions_preserve_one_admin_and_expired_refresh_is_rejected()
    {
        var staff = await Register(); var originalAdmin = await Login(TestApplication.AdminEmail); Bearer(originalAdmin);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/accounts/{staff.AccountId}", new { role = 1 })).StatusCode);
        var promoted = await Login(staff.Email);
        using var second = app.CreateClient(); second.DefaultRequestHeaders.Authorization = new("Bearer", promoted.AccessToken);
        var attempts = await Task.WhenAll(client.PutAsJsonAsync($"/api/accounts/{app.AdminId}", new { role = 0 }), second.PutAsJsonAsync($"/api/accounts/{staff.AccountId}", new { role = 0 }));
        Assert.Single(attempts, r => r.StatusCode == HttpStatusCode.NoContent); Assert.Single(attempts, r => r.StatusCode == HttpStatusCode.Conflict);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TaskManagementDbContext>(); Assert.Equal(1, await db.Accounts.CountAsync(x => x.Role == 1));
        await db.RefreshTokens.ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresDate, DateTime.UtcNow.AddDays(-1)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = promoted.RefreshToken })).StatusCode);
    }
}
