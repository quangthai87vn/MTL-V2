using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MTL.Infrastructure.Identity;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAntiforgery antiforgery)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<UserSessionResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, new
            {
                message = "Tài khoản tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau."
            });
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
        }

        user.LastLoginAtUtc = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
        return Ok(await BuildSessionAsync(user));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<UserSessionResponse>> ChangePassword(ChangePasswordRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(
                result.Errors.GroupBy(x => x.Code).ToDictionary(
                    x => x.Key,
                    x => x.Select(e => e.Description).ToArray())));
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);
        return Ok(await BuildSessionAsync(user));
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserSessionResponse>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(await BuildSessionAsync(user));
    }

    private async Task<UserSessionResponse> BuildSessionAsync(ApplicationUser user)
    {
        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        var permissions = User.Claims
            .Where(x => x.Type == "permission")
            .Select(x => x.Value)
            .Distinct()
            .Order()
            .ToArray();

        if (permissions.Length == 0)
        {
            var principal = await signInManager.CreateUserPrincipalAsync(user);
            permissions = principal.Claims
                .Where(x => x.Type == "permission")
                .Select(x => x.Value)
                .Distinct()
                .Order()
                .ToArray();
        }

        return new UserSessionResponse(
            user.Id,
            user.CompanyId,
            user.Email ?? string.Empty,
            user.FullName,
            user.MustChangePassword,
            roles,
            permissions);
    }
}

public sealed record LoginRequest(string Email, string Password, bool RememberMe);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UserSessionResponse(
    Guid Id,
    Guid CompanyId,
    string Email,
    string FullName,
    bool MustChangePassword,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

