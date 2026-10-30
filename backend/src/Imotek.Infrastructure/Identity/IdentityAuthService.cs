using System.Security.Claims;
using Imotek.Application.Auth;
using Imotek.Application.Common;
using Microsoft.AspNetCore.Identity;

namespace Imotek.Infrastructure.Identity;

internal sealed class IdentityAuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : IAuthService
{
    private const string InvalidCredentials = "Invalid email or password.";

    public async Task<Result<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var role = Roles.SelfRegistrable.FirstOrDefault(r => string.Equals(r, request.Role, StringComparison.OrdinalIgnoreCase));
        if (role is null)
        {
            errors[nameof(request.Role)] = [$"Role must be one of: {string.Join(", ", Roles.SelfRegistrable)}."];
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors[nameof(request.Email)] = ["Email is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors[nameof(request.Password)] = ["Password is required."];
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors[nameof(request.FullName)] = ["FullName is required."];
        }
        else if (request.FullName.Trim().Length > ApplicationUser.FullNameMaxLength)
        {
            errors[nameof(request.FullName)] = [$"FullName must be at most {ApplicationUser.FullNameMaxLength} characters."];
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        var email = request.Email!.Trim();
        var user = new ApplicationUser { UserName = email, Email = email, FullName = request.FullName!.Trim() };

        var created = await userManager.CreateAsync(user, request.Password!);
        if (!created.Succeeded)
        {
            return ToError(created);
        }

        var addedToRole = await userManager.AddToRoleAsync(user, role!);
        if (!addedToRole.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return ToError(addedToRole);
        }

        return new UserResponse(user.Id, email, user.FullName, [role!]);
    }

    public async Task<Result<ClaimsPrincipal>> CreateLoginPrincipalAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Error.Unauthorized(InvalidCredentials);
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Error.Unauthorized(InvalidCredentials);
        }

        var check = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            return Error.Unauthorized("Too many failed attempts. Try again later.");
        }

        if (!check.Succeeded)
        {
            return Error.Unauthorized(InvalidCredentials);
        }

        return await signInManager.CreateUserPrincipalAsync(user);
    }

    public async Task<UserResponse?> GetUserAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return new UserResponse(user.Id, user.Email ?? string.Empty, user.FullName, roles.ToList());
    }

    private static Error ToError(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return Error.Conflict("An account with this email already exists.");
        }

        var errors = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? nameof(RegisterRequest.Password) : nameof(RegisterRequest.Email))
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return Error.Validation(errors);
    }
}
