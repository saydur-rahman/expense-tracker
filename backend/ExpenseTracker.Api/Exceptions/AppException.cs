namespace ExpenseTracker.Api.Exceptions;

/// <summary>
/// Base type for domain/application errors that should be mapped to a client-facing
/// HTTP status rather than surfaced as an unhandled 500.
/// </summary>
public abstract class AppException : Exception
{
    public abstract int StatusCode { get; }

    protected AppException(string message) : base(message)
    {
    }
}

public class UnauthorizedAppException : AppException
{
    public override int StatusCode => 401;
    public UnauthorizedAppException(string message) : base(message)
    {
    }
}

public class ForbiddenAppException : AppException
{
    public override int StatusCode => 403;
    public ForbiddenAppException(string message) : base(message)
    {
    }
}

public class ConflictAppException : AppException
{
    public override int StatusCode => 409;
    public ConflictAppException(string message) : base(message)
    {
    }
}

public class ValidationAppException : AppException
{
    public override int StatusCode => 400;
    public ValidationAppException(string message) : base(message)
    {
    }
}

public class NotFoundAppException : AppException
{
    public override int StatusCode => 404;
    public NotFoundAppException(string message) : base(message)
    {
    }
}
