namespace Ims.Api.Services;

/// <summary>Source of "now". A tiny interface (registered in Program.cs) so tests could replace the time if they ever need to.</summary>
public interface IClock { DateTime UtcNow { get; } }

public class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
