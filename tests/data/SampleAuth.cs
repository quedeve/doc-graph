namespace DemoApp.Security;

public interface IAuthenticationService
{
    Task<bool> ValidateTokenAsync(string token);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly int _timeoutSeconds;

    public AuthenticationService(int timeoutSeconds)
    {
        _timeoutSeconds = timeoutSeconds;
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        await Task.Delay(10);
        return token.StartsWith("valid_");
    }

    public string GenerateChallenge(string userId)
    {
        return $"challenge_{userId}_{DateTime.UtcNow.Ticks}";
    }
}
