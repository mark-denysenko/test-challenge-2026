namespace HashChallenge.Api.DTOs;

public sealed class ErrorResponse
{
    public string Error { get; set; } = string.Empty;

    public int StatusCode { get; set; }
}
