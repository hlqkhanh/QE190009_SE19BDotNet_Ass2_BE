namespace TaskTrack.Service.Exceptions;

public sealed class ResourceNotFoundException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);
public sealed class AuthenticationException(string message) : Exception(message);

public sealed class ServiceValidationException : Exception
{
    public ServiceValidationException(string field, string message) : base(message) => Errors = new Dictionary<string, string[]> { [field] = [message] };
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
