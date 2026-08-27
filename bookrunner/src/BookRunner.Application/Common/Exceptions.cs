namespace BookRunner.Application.Common;

/// <summary>Istenen kayit bulunamadi (HTTP 404).</summary>
public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} bulunamadi: {key}");

/// <summary>Is kurali ihlali (HTTP 409/400).</summary>
public sealed class BusinessRuleException(string message) : Exception(message);

/// <summary>Kullanicinin bu islem icin yetkisi yok (HTTP 403).</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>Girdi dogrulama hatalari (HTTP 400).</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Bir veya daha fazla dogrulama hatasi olustu.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static ValidationException Single(string field, string message)
        => new(new Dictionary<string, string[]> { [field] = [message] });
}
