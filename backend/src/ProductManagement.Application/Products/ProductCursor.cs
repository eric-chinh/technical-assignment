using System.Text;
using System.Text.Json;

namespace ProductManagement.Application.Products;

public sealed class InvalidCursorException : Exception
{
    public InvalidCursorException() : base("The pagination cursor is invalid or expired.") { }
}

public sealed record ProductCursor(DateTimeOffset? CreatedAt, float? Rank, long Id)
{
    public string Encode()
    {
        var json = JsonSerializer.Serialize(this);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Returns null only when no cursor was supplied at all (a legitimate first-page
    /// request). A cursor string that IS present but fails to decode throws
    /// InvalidCursorException instead of silently falling back to "no cursor" -
    /// spec section 9 requires an invalid/expired cursor to surface as 400, not to
    /// quietly restart pagination from page one.
    /// </summary>
    public static ProductCursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<ProductCursor>(json) ?? throw new InvalidCursorException();
        }
        catch (Exception ex) when (ex is not InvalidCursorException)
        {
            throw new InvalidCursorException();
        }
    }
}
