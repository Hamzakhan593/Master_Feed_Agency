using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Master_Feed_Agency.Services;

public sealed record WhatsAppSendResult(bool Succeeded, string? ProviderReference, string? Error)
{
    public static WhatsAppSendResult Success(string? reference) => new(true, reference, null);
    public static WhatsAppSendResult Fail(string error) => new(false, null, error);
}

public sealed class WhatsAppCloudGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<WhatsAppOptions> _options;
    private readonly ILogger<WhatsAppCloudGateway> _logger;

    public WhatsAppCloudGateway(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<WhatsAppOptions> options,
        ILogger<WhatsAppCloudGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public WhatsAppOptions CurrentOptions => _options.CurrentValue;
    public bool IsConfigured => CurrentOptions.IsConfigured;

    public async Task<WhatsAppSendResult> SendTemplateAsync(
        string recipient,
        string templateName,
        IReadOnlyList<string> bodyParameters,
        CancellationToken cancellationToken = default)
    {
        var options = CurrentOptions;
        if (!options.IsConfigured)
        {
            return WhatsAppSendResult.Fail("WhatsApp Cloud API setup complete nahi hai.");
        }

        var phone = NormalizePhone(recipient);
        if (string.IsNullOrWhiteSpace(phone))
        {
            return WhatsAppSendResult.Fail("WhatsApp phone number sahi format mein nahi hai.");
        }

        if (string.IsNullOrWhiteSpace(templateName))
        {
            return WhatsAppSendResult.Fail("WhatsApp template name configured nahi hai.");
        }

        var parameters = bodyParameters
            .Select(x => new { type = "text", text = x ?? string.Empty })
            .ToArray();

        var payload = new
        {
            messaging_product = "whatsapp",
            to = phone,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = string.IsNullOrWhiteSpace(options.LanguageCode) ? "en_US" : options.LanguageCode },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters
                    }
                }
            }
        };

        var apiVersion = options.ApiVersion.Trim();
        var url = $"https://graph.facebook.com/{apiVersion}/{options.PhoneNumberId.Trim()}/messages";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken.Trim());
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var client = _httpClientFactory.CreateClient("WhatsAppCloud");
            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = ReadError(content) ?? $"WhatsApp API error {(int)response.StatusCode}.";
                _logger.LogWarning("WhatsApp API rejected a message: {Error}", error);
                return WhatsAppSendResult.Fail(error);
            }

            return WhatsAppSendResult.Success(ReadMessageId(content));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WhatsAppSendResult.Fail("WhatsApp request timeout ho gaya.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp API request failed.");
            return WhatsAppSendResult.Fail("WhatsApp se connection nahi ho saka.");
        }
    }

    public static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
        if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length >= 10) digits = "92" + digits[1..];
        else if (digits.Length == 10 && digits.StartsWith("3", StringComparison.Ordinal)) digits = "92" + digits;

        return digits.Length is >= 10 and <= 15 ? digits : string.Empty;
    }

    private static string? ReadMessageId(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("messages", out var messages) &&
                messages.ValueKind == JsonValueKind.Array &&
                messages.GetArrayLength() > 0 &&
                messages[0].TryGetProperty("id", out var id))
            {
                return id.GetString();
            }
        }
        catch (JsonException) { }

        return null;
    }

    private static string? ReadError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }
        }
        catch (JsonException) { }

        return null;
    }
}
