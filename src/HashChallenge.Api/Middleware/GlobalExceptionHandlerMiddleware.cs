using System.Net;
using System.Text.Json;
using HashChallenge.Api.DTOs;
using RabbitMQ.Client.Exceptions;

namespace HashChallenge.Api.Middleware;

public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (BrokerUnreachableException ex)
        {
            _logger.LogError(ex, "RabbitMQ connection failed");
            await WriteErrorResponseAsync(context, HttpStatusCode.ServiceUnavailable, "Message broker is unavailable.").ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Request was cancelled, no need to write response
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorResponseAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.").ConfigureAwait(false);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Error = message,
            StatusCode = (int)statusCode,
        };

        await context.Response.WriteAsJsonAsync(errorResponse).ConfigureAwait(false);
    }
}
