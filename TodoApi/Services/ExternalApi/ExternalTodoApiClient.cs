using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TodoApi.Dtos.ExternalApi;

namespace TodoApi.Services.ExternalApi;

public class ExternalTodoApiClient : IExternalTodoApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalTodoApiClient> _logger;
    private readonly ExternalApiSettings _settings;

    public ExternalTodoApiClient(
        HttpClient httpClient,
        IOptions<ExternalApiSettings> settings,
        ILogger<ExternalTodoApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TodoApi-SyncClient/1.0");
    }

    // TodoList operations
    public async Task<IEnumerable<ExternalTodoListDto>> GetTodoListsAsync()
    {
        _logger.LogDebug("Fetching all TodoLists from external API");
        
        var response = await ExecuteWithRetryAsync(
            () => _httpClient.GetAsync("/api/todolists")
        );
        
        await EnsureSuccessStatusCodeAsync(response);
        
        var lists = await response.Content.ReadFromJsonAsync<IEnumerable<ExternalTodoListDto>>()
            ?? Enumerable.Empty<ExternalTodoListDto>();
        
        _logger.LogInformation("Fetched {Count} TodoLists from external API", lists.Count());
        return lists;
    }

    public async Task<ExternalTodoListDto> GetTodoListAsync(long id)
    {
        _logger.LogDebug("Fetching TodoList {Id} from external API", id);
        
        var response = await ExecuteWithRetryAsync(
            () => _httpClient.GetAsync($"/api/todolists/{id}")
        );
        
        await EnsureSuccessStatusCodeAsync(response);
        
        var list = await response.Content.ReadFromJsonAsync<ExternalTodoListDto>()
            ?? throw new InvalidOperationException($"Failed to deserialize TodoList {id}");
        
        _logger.LogDebug("Fetched TodoList {Id} from external API", id);
        return list;
    }

    public async Task<ExternalTodoListDto> CreateTodoListAsync(CreateExternalTodoListDto dto)
    {
        _logger.LogDebug("Creating TodoList '{Name}' on external API", dto.Name);
        
        var response = await ExecuteWithRetryAsync(
            () => _httpClient.PostAsJsonAsync("/api/todolists", dto)
        );
        
        await EnsureSuccessStatusCodeAsync(response);
        
        var list = await response.Content.ReadFromJsonAsync<ExternalTodoListDto>()
            ?? throw new InvalidOperationException("Failed to deserialize created TodoList");
        
        _logger.LogInformation("Created TodoList {Id} on external API", list.Id);
        return list;
    }

    public async Task UpdateTodoListAsync(long id, UpdateExternalTodoListDto dto)
    {
        _logger.LogDebug("Updating TodoList {Id} on external API", id);
        
        var response = await ExecuteWithRetryAsync(
            () => _httpClient.PutAsJsonAsync($"/api/todolists/{id}", dto)
        );
        
        await EnsureSuccessStatusCodeAsync(response);
        
        _logger.LogInformation("Updated TodoList {Id} on external API", id);
    }

    public async Task DeleteTodoListAsync(long id)
    {
        _logger.LogDebug("Deleting TodoList {Id} on external API", id);
        
        var response = await ExecuteWithRetryAsync(
            () => _httpClient.DeleteAsync($"/api/todolists/{id}")
        );
        
        await EnsureSuccessStatusCodeAsync(response);
        
        _logger.LogInformation("Deleted TodoList {Id} on external API", id);
    }

    // TodoItem operations
    public async Task<IEnumerable<ExternalTodoItemDto>> GetTodoItemsAsync(long todoListId)
    {
        _logger.LogDebug("Fetching TodoItems for list {TodoListId} from external API", todoListId);
        
        var response = await ExecuteWithRetryAsync(
            () => _httpClient.GetAsync($"/api/todolists/{todoListId}/items")
        );
        
        await EnsureSuccessStatusCodeAsync(response);
        
        var items = await response.Content.ReadFromJsonAsync<IEnumerable<ExternalTodoItemDto>>()
            ?? Enumerable.Empty<ExternalTodoItemDto>();
        
        _logger.LogInformation(
            "Fetched {Count} TodoItems for list {TodoListId} from external API",
            items.Count(),
            todoListId
        );
        
        return items;
    }

    public async Task<ExternalTodoItemDto> GetTodoItemAsync(long todoListId, long itemId)
    {
        _logger.LogDebug(
            "Fetching TodoItem {ItemId} from list {TodoListId} on external API",
            itemId,
            todoListId
        );
        
        var response = await ExecuteWithRetryAsync(
            () => _httpClient.GetAsync($"/api/todolists/{todoListId}/items/{itemId}")
        );
        
        await EnsureSuccessStatusCodeAsync(response);
        
        var item = await response.Content.ReadFromJsonAsync<ExternalTodoItemDto>()
            ?? throw new InvalidOperationException($"Failed to deserialize TodoItem {itemId}");
        
        return item;
    }

    public async Task<ExternalTodoItemDto> CreateTodoItemAsync(
        long todoListId,
        CreateExternalTodoItemDto dto)
    {
        _logger.LogDebug(
            "Creating TodoItem '{Title}' in list {TodoListId} on external API",
            dto.Title,
            todoListId
        );
        
        var response = await ExecuteWithRetryAsync(
            () => _httpClient.PostAsJsonAsync($"/api/todolists/{todoListId}/items", dto)
        );
        
        await EnsureSuccessStatusCodeAsync(response);
        
        var item = await response.Content.ReadFromJsonAsync<ExternalTodoItemDto>()
            ?? throw new InvalidOperationException("Failed to deserialize created TodoItem");
        
        _logger.LogInformation(
            "Created TodoItem {ItemId} in list {TodoListId} on external API",
            item.Id,
            todoListId
        );
        
        return item;
    }

    public async Task UpdateTodoItemAsync(
        long todoListId,
        long itemId,
        UpdateExternalTodoItemDto dto)
    {
        _logger.LogDebug(
            "Updating TodoItem {ItemId} in list {TodoListId} on external API",
            itemId,
            todoListId
        );
        
        var response = await ExecuteWithRetryAsync(
            () => _httpClient.PutAsJsonAsync($"/api/todolists/{todoListId}/items/{itemId}", dto)
        );
        
        await EnsureSuccessStatusCodeAsync(response);
        
        _logger.LogInformation(
            "Updated TodoItem {ItemId} in list {TodoListId} on external API",
            itemId,
            todoListId
        );
    }

    public async Task DeleteTodoItemAsync(long todoListId, long itemId)
    {
        _logger.LogDebug(
            "Deleting TodoItem {ItemId} from list {TodoListId} on external API",
            itemId,
            todoListId
        );
        
        var response = await ExecuteWithRetryAsync(
            () => _httpClient.DeleteAsync($"/api/todolists/{todoListId}/items/{itemId}")
        );
        
        await EnsureSuccessStatusCodeAsync(response);
        
        _logger.LogInformation(
            "Deleted TodoItem {ItemId} from list {TodoListId} on external API",
            itemId,
            todoListId
        );
    }

    // Helper methods
    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> action)
    {
        var retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= _settings.MaxRetries)
        {
            try
            {
                var response = await action();
                
                // If it's a transient error, retry
                if (IsTransientError(response.StatusCode) && retryCount < _settings.MaxRetries)
                {
                    retryCount++;
                    await DelayForRetryAsync(retryCount);
                    continue;
                }
                
                return response;
            }
            catch (HttpRequestException ex) when (retryCount < _settings.MaxRetries)
            {
                lastException = ex;
                retryCount++;
                
                _logger.LogWarning(
                    ex,
                    "HTTP request failed. Retry {RetryCount}/{MaxRetries}",
                    retryCount,
                    _settings.MaxRetries
                );
                
                await DelayForRetryAsync(retryCount);
            }
            catch (TaskCanceledException ex) when (retryCount < _settings.MaxRetries)
            {
                lastException = ex;
                retryCount++;
                
                _logger.LogWarning(
                    ex,
                    "HTTP request timed out. Retry {RetryCount}/{MaxRetries}",
                    retryCount,
                    _settings.MaxRetries
                );
                
                await DelayForRetryAsync(retryCount);
            }
        }

        throw new HttpRequestException(
            $"Request failed after {_settings.MaxRetries} retries",
            lastException
        );
    }

    private static bool IsTransientError(System.Net.HttpStatusCode statusCode)
    {
        return statusCode == System.Net.HttpStatusCode.RequestTimeout ||
               statusCode == System.Net.HttpStatusCode.TooManyRequests ||
               statusCode == System.Net.HttpStatusCode.InternalServerError ||
               statusCode == System.Net.HttpStatusCode.BadGateway ||
               statusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
               statusCode == System.Net.HttpStatusCode.GatewayTimeout;
    }

    private async Task DelayForRetryAsync(int retryCount)
    {
        // Exponential backoff: 2^retryCount seconds with jitter
        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
        var delay = baseDelay + jitter;
        
        _logger.LogDebug("Waiting {Delay}ms before retry {RetryCount}", delay.TotalMilliseconds, retryCount);
        await Task.Delay(delay);
    }

    private async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogError(
                "External API request failed with status {StatusCode}: {Content}",
                response.StatusCode,
                content
            );
            
            throw new HttpRequestException(
                $"External API returned {response.StatusCode}: {content}",
                null,
                response.StatusCode
            );
        }
    }
}