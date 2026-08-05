using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Common;
using PosFlow.Application.Users;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.Users;

public sealed class UserService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser,
    IPasswordHasher<AppUser> passwordHasher)
    : IUserService
{
    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IPasswordHasher<AppUser> _passwordHasher = passwordHasher;

    public async Task<IReadOnlyList<UserResponse>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users
            .AsNoTracking()
            .Where(x => x.TenantId == _currentUser.TenantId);

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var users = await query
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        return users
            .Select(MapResponse)
            .ToList();
    }

    public async Task<UserResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        return user is null
            ? null
            : MapResponse(user);
    }

    public async Task<UserResponse> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Role),
                "الدور غير صحيح.");
        }

        var normalizedUsername = request.Username
            .Trim()
            .ToUpperInvariant();

        // Username uniqueness is global (not per-tenant) - matches the
        // unique index on AppUser.NormalizedUsername in the DB.
        var usernameTaken = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.NormalizedUsername == normalizedUsername,
                cancellationToken);

        if (usernameTaken)
        {
            throw new InvalidOperationException(
                "اسم المستخدم مستخدم بالفعل.");
        }

        var user = new AppUser
        {
            TenantId = _currentUser.TenantId,
            BranchId = request.BranchId,
            Username = request.Username.Trim(),
            NormalizedUsername = normalizedUsername,
            DisplayName = request.DisplayName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email)
                ? null
                : request.Email.Trim(),
            Role = role,
            IsActive = true,
            PasswordHash = string.Empty
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.Password);

        _dbContext.Users.Add(user);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapResponse(user);
    }

    public async Task<UserResponse> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Role),
                "الدور غير صحيح.");
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException(
                "المستخدم غير موجود.");
        }

        if (id == _currentUser.UserId && !request.IsActive)
        {
            throw new InvalidOperationException(
                "لا يمكنك تعطيل حسابك الخاص.");
        }

        if (id == _currentUser.UserId && role != UserRole.Admin)
        {
            throw new InvalidOperationException(
                "لا يمكنك تغيير دورك الخاص.");
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim();
        user.Role = role;
        user.BranchId = request.BranchId;
        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapResponse(user);
    }

    public async Task ResetPasswordAsync(
        Guid id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException(
                "المستخدم غير موجود.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.NewPassword);

        user.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static UserResponse MapResponse(
        AppUser user)
    {
        return new UserResponse(
            Id: user.Id,
            Username: user.Username,
            DisplayName: user.DisplayName,
            Email: user.Email,
            Role: user.Role.ToString(),
            BranchId: user.BranchId,
            IsActive: user.IsActive);
    }
}
