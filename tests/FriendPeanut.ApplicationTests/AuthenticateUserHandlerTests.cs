using FriendPeanut.Application.Abstractions;
using FriendPeanut.Application.Features.Identity.AuthenticateUser;
using Moq;
using Xunit;

namespace FriendPeanut.ApplicationTests;

public sealed class AuthenticateUserHandlerTests
{
    private static UserRecord User(
        Guid? id = null, string status = "ACTIVE", bool mustChange = false,
        int failedCount = 0, string? passwordHash = "HASHED")
        => new(id ?? Guid.NewGuid(), 1, "testuser", "Test User", passwordHash,
            status, mustChange, false, failedCount, Guid.NewGuid(), 1, DateTimeOffset.UtcNow);

    private static (AuthenticateUserHandler handler, Mock<IUserStore> users, Mock<IPasswordHasher> hasher,
        Mock<ISessionStore> sessions, Mock<IAuthChallengeStore> challenges, Mock<ILoginAttemptStore> attempts)
        Build(UserRecord? user)
    {
        var usersMock = new Mock<IUserStore>();
        usersMock.Setup(u => u.FindByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        usersMock.Setup(u => u.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);

        var hasherMock = new Mock<IPasswordHasher>();
        hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sessionsMock = new Mock<ISessionStore>();

        var challengesMock = new Mock<IAuthChallengeStore>();
        challengesMock.Setup(c => c.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("token", new AuthChallengeRecord(Guid.NewGuid(), Guid.Empty, "h", "MFA_VERIFY",
                DateTimeOffset.UtcNow.AddMinutes(5), null, 0)));

        var attemptsMock = new Mock<ILoginAttemptStore>();

        var handler = new AuthenticateUserHandler(usersMock.Object, hasherMock.Object, sessionsMock.Object,
            challengesMock.Object, attemptsMock.Object);

        return (handler, usersMock, hasherMock, sessionsMock, challengesMock, attemptsMock);
    }

    [Fact]
    public async Task Unknown_user_throws_InvalidCredentials()
    {
        var (handler, _, _, _, _, attempts) = Build(user: null);
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new AuthenticateUserCommand("ghost", "pw"), "LAN", "1.2.3.4", "corr", default));
        attempts.Verify(a => a.RecordAsync(null, false, "USER_NOT_FOUND", "1.2.3.4", "corr", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Inactive_account_throws_AccountNotActive()
    {
        var (handler, _, _, _, _, _) = Build(User(status: "INACTIVE"));
        await Assert.ThrowsAsync<AccountNotActiveException>(() =>
            handler.HandleAsync(new AuthenticateUserCommand("u", "pw"), "LAN", "1.2.3.4", "corr", default));
    }

    [Fact]
    public async Task Locked_account_throws_AccountNotActive()
    {
        var (handler, _, _, _, _, _) = Build(User(status: "LOCKED"));
        await Assert.ThrowsAsync<AccountNotActiveException>(() =>
            handler.HandleAsync(new AuthenticateUserCommand("u", "pw"), "LAN", null, "corr", default));
    }

    [Fact]
    public async Task Wrong_password_increments_and_throws()
    {
        var user = User(failedCount: 0);
        var (handler, users, hasher, _, _, attempts) = Build(user);
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new AuthenticateUserCommand("u", "pw"), "LAN", "1.2.3.4", "corr", default));

        users.Verify(u => u.IncrementFailedLoginAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        attempts.Verify(a => a.RecordAsync(user.Id, false, "BAD_PASSWORD", "1.2.3.4", "corr", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Fifth_failure_locks_account()
    {
        var user = User(failedCount: 4);
        var (handler, users, hasher, _, _, _) = Build(user);
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        // The handler calls FindByIdAsync exactly once — after IncrementFailedLoginAsync —
        // to re-read the count. Return the incremented record on that single call.
        users.Setup(u => u.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user with { FailedLoginCount = 5 });

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new AuthenticateUserCommand("u", "pw"), "LAN", null, "corr", default));

        users.Verify(u => u.LockAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Successful_login_with_must_change_password_returns_CHANGE_PASSWORD()
    {
        var (handler, _, _, sessions, _, _) = Build(User(mustChange: true));
        var result = await handler.HandleAsync(new AuthenticateUserCommand("u", "pw"), "LAN", null, "corr", default);
        Assert.Equal("CHANGE_PASSWORD", result.NextStep);
        Assert.Null(result.SessionId);
        sessions.Verify(s => s.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
            It.IsAny<TimeSpan>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Successful_login_without_must_change_returns_MFA_VERIFY()
    {
        var (handler, _, _, sessions, _, _) = Build(User(mustChange: false));
        var result = await handler.HandleAsync(new AuthenticateUserCommand("u", "pw"), "LAN", null, "corr", default);
        Assert.Equal("MFA_VERIFY", result.NextStep);
        Assert.Null(result.SessionId);
        sessions.Verify(s => s.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
            It.IsAny<TimeSpan>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Successful_login_resets_failed_count_and_records_success()
    {
        var user = User();
        var (handler, users, _, _, _, attempts) = Build(user);
        await handler.HandleAsync(new AuthenticateUserCommand("u", "pw"), "LAN", null, "corr", default);
        users.Verify(u => u.ResetFailedLoginAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        attempts.Verify(a => a.RecordAsync(user.Id, true, null, null, "corr", It.IsAny<CancellationToken>()), Times.Once);
    }
}