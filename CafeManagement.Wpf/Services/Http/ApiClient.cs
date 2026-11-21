using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace CafeManagement.Wpf.Services.Http;

public class ApiClient
{
    private static readonly Lazy<ApiClient> _instance = new(() => new ApiClient());
    public static ApiClient Instance => _instance.Value;

    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;

    public string BaseUrl { get; set; } = "http://localhost:5000";

    public ApiClient()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public void SetBaseUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            BaseUrl = url.TrimEnd('/');
            _http.BaseAddress = new Uri(BaseUrl);
        }
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        try
        {
            var response = await _http.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API lỗi ({response.StatusCode}): {error}");
            }
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw new HttpRequestException($"Không thể kết nối đến máy chủ API ({_http.BaseAddress}): {ex.Message}", ex);
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(endpoint, data, _jsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(ParseErrorMessage(error, response.StatusCode.ToString()), null, response.StatusCode);
            }
            return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw new HttpRequestException($"Không thể kết nối đến máy chủ API ({_http.BaseAddress}): {ex.Message}", ex);
        }
    }

    public async Task<bool> PostAsync<TRequest>(string endpoint, TRequest data)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(endpoint, data, _jsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(ParseErrorMessage(error, response.StatusCode.ToString()), null, response.StatusCode);
            }
            return true;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw new HttpRequestException($"Không thể kết nối đến máy chủ API ({_http.BaseAddress}): {ex.Message}", ex);
        }
    }

    public async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
    {
        try
        {
            var response = await _http.PutAsJsonAsync(endpoint, data, _jsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(ParseErrorMessage(error, response.StatusCode.ToString()), null, response.StatusCode);
            }
            return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw new HttpRequestException($"Không thể kết nối đến máy chủ API ({_http.BaseAddress}): {ex.Message}", ex);
        }
    }

    public async Task<bool> PutAsync<TRequest>(string endpoint, TRequest data)
    {
        try
        {
            var response = await _http.PutAsJsonAsync(endpoint, data, _jsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(ParseErrorMessage(error, response.StatusCode.ToString()), null, response.StatusCode);
            }
            return true;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw new HttpRequestException($"Không thể kết nối đến máy chủ API ({_http.BaseAddress}): {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteAsync(string endpoint)
    {
        try
        {
            var response = await _http.DeleteAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(ParseErrorMessage(error, response.StatusCode.ToString()), null, response.StatusCode);
            }
            return true;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw new HttpRequestException($"Không thể kết nối đến máy chủ API ({_http.BaseAddress}): {ex.Message}", ex);
        }
    }

    public static string ParseErrorMessage(string rawContent, string defaultStatus)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawContent);
            if (doc.RootElement.TryGetProperty("message", out var msgProp))
            {
                return msgProp.GetString() ?? $"Lỗi {defaultStatus}";
            }
        }
        catch { }

        return !string.IsNullOrWhiteSpace(rawContent) ? rawContent : $"Lỗi máy chủ ({defaultStatus})";
    }
}
