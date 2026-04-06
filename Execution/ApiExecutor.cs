using CHRONIQ.Domain;
using Microsoft.Extensions.Logging;

namespace CHRONIQ.Execution;

/// <summary>
/// Executor for HTTP API requests.
/// Supports GET, POST, PUT, DELETE methods with optional authentication.
/// Command format: https://api.example.com/endpoint?method=POST&amp;headers=Authorization:Bearer+token&amp;body=json
/// </summary>
public class ApiExecutor : ITaskExecutor
{
    private readonly ILogger<ApiExecutor> _logger;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the ApiExecutor class.
    /// </summary>
    public ApiExecutor(ILogger<ApiExecutor> logger, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _timeout = timeout > TimeSpan.Zero ? timeout : TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Executes an HTTP API request.
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(TaskDefinition task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(task.Command);

        try
        {
            _logger.LogInformation("Starting API request: {Command}", task.Command);

            var request = ParseApiCommand(task.Command);
            
            using var timeoutCts = new CancellationTokenSource(_timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var result = await ExecuteHttpRequestAsync(request, linkedCts.Token);

            _logger.LogInformation(
                "API request completed with status code {StatusCode}",
                (int)result.StatusCode);

            return new ExecutionResult
            {
                ExitCode = result.IsSuccessStatusCode ? 0 : (int)result.StatusCode,
                Output = result.ResponseBody,
                Error = result.ErrorMessage ?? ""
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed: {Message}", ex.Message);
            return new ExecutionResult
            {
                ExitCode = -1,
                Output = "",
                Error = ex.Message
            };
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "API request timed out after {Timeout}ms", _timeout.TotalMilliseconds);
            return new ExecutionResult
            {
                ExitCode = -1,
                Output = "",
                Error = $"Request timeout after {_timeout.TotalSeconds}s"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in API request: {Message}", ex.Message);
            return new ExecutionResult
            {
                ExitCode = -1,
                Output = "",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Parses the API command string into structured request data.
    /// </summary>
    private ApiRequest ParseApiCommand(string command)
    {
        // Parse URL from beginning
        var urlMatch = System.Text.RegularExpressions.Regex.Match(
            command,
            @"^(https?://[^\s?]+)");

        if (!urlMatch.Success)
            throw new ArgumentException("Invalid URL format in API command");

        var url = urlMatch.Groups[1].Value;
        var request = new ApiRequest { Url = url };

        // Parse query parameters
        var methodMatch = System.Text.RegularExpressions.Regex.Match(
            command,
            @"method=([A-Z]+)");
        request.Method = methodMatch.Success ? methodMatch.Groups[1].Value : "GET";

        // Parse headers
        var headersMatch = System.Text.RegularExpressions.Regex.Match(
            command,
            @"headers=([^&]+)");
        if (headersMatch.Success)
        {
            var headerString = System.Web.HttpUtility.UrlDecode(headersMatch.Groups[1].Value);
            var headerParts = headerString.Split(';');
            request.Headers = new Dictionary<string, string>();

            foreach (var header in headerParts)
            {
                var parts = header.Split(':');
                if (parts.Length == 2)
                {
                    request.Headers[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        // Parse body
        var bodyMatch = System.Text.RegularExpressions.Regex.Match(
            command,
            @"body=(.+?)(?:&|$)");
        if (bodyMatch.Success)
        {
            request.Body = System.Web.HttpUtility.UrlDecode(bodyMatch.Groups[1].Value);
        }

        return request;
    }

    /// <summary>
    /// Executes the HTTP request.
    /// </summary>
    private async Task<ApiResponse> ExecuteHttpRequestAsync(
        ApiRequest request,
        CancellationToken cancellationToken)
    {
        using var clientHandler = new HttpClientHandler();
        using var httpClient = new HttpClient(clientHandler) { Timeout = _timeout };

        // Add custom headers
        if (request.Headers != null)
        {
            foreach (var header in request.Headers)
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        try
        {
            HttpResponseMessage response = request.Method.ToUpper() switch
            {
                "GET" => await httpClient.GetAsync(request.Url, cancellationToken),
                "POST" => await httpClient.PostAsync(
                    request.Url,
                    new StringContent(request.Body ?? "", System.Text.Encoding.UTF8, "application/json"),
                    cancellationToken),
                "PUT" => await httpClient.PutAsync(
                    request.Url,
                    new StringContent(request.Body ?? "", System.Text.Encoding.UTF8, "application/json"),
                    cancellationToken),
                "DELETE" => await httpClient.DeleteAsync(request.Url, cancellationToken),
                _ => throw new NotSupportedException($"HTTP method {request.Method} not supported")
            };

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            return new ApiResponse
            {
                StatusCode = response.StatusCode,
                ResponseBody = content,
                IsSuccessStatusCode = response.IsSuccessStatusCode,
                ErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}: {content}"
            };
        }
        catch (HttpRequestException ex)
        {
            return new ApiResponse
            {
                StatusCode = System.Net.HttpStatusCode.InternalServerError,
                ResponseBody = "",
                IsSuccessStatusCode = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Represents an API request.
    /// </summary>
    private class ApiRequest
    {
        public string Url { get; set; } = "";
        public string Method { get; set; } = "GET";
        public Dictionary<string, string>? Headers { get; set; }
        public string? Body { get; set; }
    }

    /// <summary>
    /// Represents an API response.
    /// </summary>
    private class ApiResponse
    {
        public System.Net.HttpStatusCode StatusCode { get; set; }
        public string ResponseBody { get; set; } = "";
        public string? ErrorMessage { get; set; }
        public bool IsSuccessStatusCode { get; set; }
    }
}
