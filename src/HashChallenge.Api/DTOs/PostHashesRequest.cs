using System.ComponentModel.DataAnnotations;

namespace HashChallenge.Api.DTOs;

public sealed class PostHashesRequest
{
    [Required]
    [Range(1, 1_000_000)]
    public int Count { get; set; }
}
