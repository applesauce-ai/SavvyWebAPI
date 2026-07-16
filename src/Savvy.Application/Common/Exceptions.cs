namespace Savvy.Application.Common;

/// <summary>Base type for expected application/domain errors mapped to HTTP responses.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

/// <summary>Requested resource does not exist (→ 404).</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }

    public static NotFoundException For(string resource, object key)
        => new($"{resource} '{key}' was not found.");
}

/// <summary>Authentication failed or is required (→ 401).</summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Authentication is required.")
        : base(message) { }
}

/// <summary>Caller is authenticated but not permitted to access the resource (→ 403).</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have access to this resource.")
        : base(message) { }
}

/// <summary>Request is well-formed but violates a business rule (→ 400/409 depending on caller).</summary>
public sealed class ValidationException : AppException
{
    public ValidationException(string message) : base(message) { }
}

/// <summary>A conflicting resource already exists with materially different data (→ 409).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}
