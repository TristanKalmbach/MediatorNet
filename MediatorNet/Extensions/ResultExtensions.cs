namespace MediatorNet.Extensions;

#if NETCOREAPP
using Implementation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Extensions for working with <see cref="Result"/> and <see cref="Result{T}"/> in ASP.NET Core applications.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a <see cref="Result{T}"/> to an appropriate <see cref="IActionResult"/> based on the result state.
    /// </summary>
    /// <typeparam name="T">The type of the value in the result.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <returns>
    /// <see cref="OkObjectResult"/> with the value if successful;
    /// <see cref="BadRequestObjectResult"/> with the error message if failed.
    /// </returns>
    public static IActionResult ToActionResult<T>(this Result<T> result) => 
        result.IsSuccess 
            ? new OkObjectResult(result.Value) 
            : new BadRequestObjectResult(new ProblemDetails 
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

    /// <summary>
    /// Converts a <see cref="Result{T}"/> to an appropriate <see cref="IActionResult"/> based on the result state,
    /// using custom delegate functions to create success and error responses.
    /// </summary>
    /// <typeparam name="T">The type of the value in the result.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <param name="onSuccess">A function that creates a success response.</param>
    /// <param name="onFailure">A function that creates a failure response.</param>
    /// <returns>The appropriate action result.</returns>
    public static IActionResult ToActionResult<T>(
        this Result<T> result,
        Func<T, IActionResult> onSuccess,
        Func<string, IActionResult> onFailure) =>
        result.IsSuccess 
            ? onSuccess(result.Value) 
            : onFailure(result.Error);

    /// <summary>
    /// Converts a <see cref="Result"/> to an appropriate <see cref="IActionResult"/> based on the result state.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>
    /// <see cref="OkResult"/> if successful;
    /// <see cref="BadRequestObjectResult"/> with the error message if failed.
    /// </returns>
    public static IActionResult ToActionResult(this Result result) =>
        result.IsSuccess
            ? new OkResult()
            : new BadRequestObjectResult(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

    /// <summary>
    /// Converts a <see cref="Result"/> to an appropriate <see cref="IActionResult"/> based on the result state,
    /// using custom delegate functions to create success and error responses.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <param name="onSuccess">A function that creates a success response.</param>
    /// <param name="onFailure">A function that creates a failure response.</param>
    /// <returns>The appropriate action result.</returns>
    public static IActionResult ToActionResult(
        this Result result,
        Func<IActionResult> onSuccess,
        Func<string, IActionResult> onFailure) =>
        result.IsSuccess
            ? onSuccess()
            : onFailure(result.Error);

    /// <summary>
    /// Returns a <see cref="CreatedResult"/> for a successful result, or a <see cref="BadRequestObjectResult"/> for a failed result.
    /// </summary>
    /// <typeparam name="T">The type of the value in the result.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <param name="uri">The URI at which the content has been created.</param>
    /// <returns>
    /// <see cref="CreatedResult"/> with the value and URI if successful;
    /// <see cref="BadRequestObjectResult"/> with the error message if failed.
    /// </returns>
    public static IActionResult ToCreatedResult<T>(this Result<T> result, string uri) =>
        result.IsSuccess
            ? new CreatedResult(uri, result.Value)
            : new BadRequestObjectResult(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

    /// <summary>
    /// Returns a <see cref="NotFoundObjectResult"/> for a failed result with "not found" message,
    /// or the appropriate success result based on the provided function.
    /// </summary>
    /// <typeparam name="T">The type of the value in the result.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <param name="notFoundMessage">The error message to check for in the result.</param>
    /// <param name="onSuccess">A function that creates a success response (defaults to OkObjectResult).</param>
    /// <returns>The appropriate action result.</returns>
    public static IActionResult ToActionResultWithNotFound<T>(
        this Result<T> result, 
        string notFoundMessage = "not found",
        Func<T, IActionResult>? onSuccess = null)
    {
        onSuccess ??= value => new OkObjectResult(value);

        return result.IsSuccess
            ? onSuccess(result.Value)
            : result.Error.Contains(notFoundMessage, StringComparison.OrdinalIgnoreCase)
                ? new NotFoundObjectResult(new ProblemDetails
                {
                    Title = "Not Found",
                    Detail = result.Error,
                    Status = StatusCodes.Status404NotFound
                })
                : new BadRequestObjectResult(new ProblemDetails
                {
                    Title = "Bad Request",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
    }
}
#endif