namespace IdTheAthlete.Api.Middleware;

public interface IAdminLoginThrottle
{
    // How long this client must wait before trying again; null if it may try now.
    TimeSpan? LockedOutFor(string client);
    void RecordFailure(string client);
    void RecordSuccess(string client);
}
