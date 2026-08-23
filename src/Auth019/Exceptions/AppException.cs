namespace Auth019.Exceptions;

/// <summary>
/// Domain errors that map to a client-facing HTTP status rather than a 500.
/// </summary>
public abstract class AppException : Exception
{
    public abstract int StatusCode { get; }

    protected AppException(string message) : base(message)
    {
    }
}

public class ValidationAppException : AppException
{
    public override int StatusCode => 400;
    public ValidationAppException(string message) : base(message) { }
}

public class ForbiddenAppException : AppException
{
    public override int StatusCode => 403;
    public ForbiddenAppException(string message) : base(message) { }
}

public class NotFoundAppException : AppException
{
    public override int StatusCode => 404;
    public NotFoundAppException(string message) : base(message) { }
}

public class ConflictAppException : AppException
{
    public override int StatusCode => 409;
    public ConflictAppException(string message) : base(message) { }
}
