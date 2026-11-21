using FriendPeanut.Application.Abstractions;
using FriendPeanut.Application.Features.Identity.EnrollAndVerifyMfa;
using Moq;
using Xunit;

namespace FriendPeanut.ApplicationTests;

public sealed class VerifyMfaHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string ValidSecret = "JBSWY3DPEHPK3PXP";

    private static (VerifyMfaHandler handler, Mock<IMfaCredentialStore> creds, Mock<ITotpService> totp,
        Mock<IUserStore> users, Mock<ISessionStore> sessions)
        Build(bool totpVerifies, bool replay = false)
    {
        var creds = new Mock<IMfaCredentialStore>();
        creds.Setup(c => c.FindForUserAsync(UserId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new MfaCredentialRecord(Guid.NewGuid(), UserId, "cipher",
                 "v1", true, replay ? 1_000_000L : null));

        var crypto = new Mock<IMfaCrypto>();
        crypto.Setup(c => c.Decrypt("cipher")).Returns(ValidSecret);

        var totp = new Mock<ITotpService>();
        totp.Setup(t => t.Verify(ValidSecret, "123456", 1)).Returns(totpVerifies);
        totp.Setup(t => t.CurrentTimeStep()).Returns(replay ? 1_000_000L : 1_000_001L);

        var users = new Mock<IUserStore>();
        users.Setup(u => u.FindByIdAsync(UserId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new UserRecord(UserId, 1, "u", "User", "h", "ACTIVE",
                 false, false, 0, Guid.NewGuid(), 1, DateTimeOffset.UtcNow));

        var sessions = new Mock<ISessionStore>();
        sessions.Setup(s => s.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<TimeSpan>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        clock.Setup(c => c.TodayUtc).Returns(DateOnly.FromDateTime(DateTime.UtcNow));

        var handler = new VerifyMfaHandler(creds.Object, crypto.Object, totp.Object,
            users.Object, sessions.Object, clock.Object);

        return (handler, creds, totp, users, sessions);
    }

    [Fact]
    public async Task Wrong_code_returns_failure_and_no_session()
    {
        var (handler, _, _, _, sessions) = Build(totpVerifies: false);
        var result = await handler.HandleAsync(new VerifyMfaCommand(UserId, "000000"), "LAN", default);
        Assert.False(result.Success);
        Assert.Null(result.SessionToken);
        sessions.Verify(s => s.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
            It.IsAny<TimeSpan>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Replay_of_accepted_time_step_is_rejected()
    {
        var (handler, _, _, _, sessions) = Build(totpVerifies: true, replay: true);
        var result = await handler.HandleAsync(new VerifyMfaCommand(UserId, "123456"), "LAN", default);
        Assert.False(result.Success);
        sessions.Verify(s => s.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
            It.IsAny<TimeSpan>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Valid_code_creates_session_and_returns_token()
    {
        var (handler, _, _, _, sessions) = Build(totpVerifies: true);
        var result = await handler.HandleAsync(new VerifyMfaCommand(UserId, "123456"), "LAN", default);
        Assert.True(result.Success);
        Assert.NotNull(result.SessionToken);
        Assert.NotNull(result.SessionId);
        Assert.NotNull(result.ExpiresAt);
        sessions.Verify(s => s.CreateAsync(
            UserId, It.IsAny<string>(), It.IsAny<Guid?>(), "LAN",
            It.IsAny<TimeSpan>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Valid_code_records_accepted_time_step()
    {
        var (handler, creds, _, _, _) = Build(totpVerifies: true);
        await handler.HandleAsync(new VerifyMfaCommand(UserId, "123456"), "LAN", default);
        creds.Verify(c => c.RecordAcceptedTimeStepAsync(UserId, 1_000_001L, It.IsAny<CancellationToken>()), Times.Once);
    }
}