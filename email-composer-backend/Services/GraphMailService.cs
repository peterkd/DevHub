using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EmailComposer.Backend.Models;
using EmailComposer.Backend.Options;
using Microsoft.Extensions.Options;

namespace EmailComposer.Backend.Services;

public sealed class GraphMailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GraphOptions _graphOptions;
    private readonly SqlRecipientService _recipientService;

    public GraphMailService(
        IHttpClientFactory httpClientFactory,
        IOptions<GraphOptions> graphOptions,
        SqlRecipientService recipientService)
    {
        _httpClientFactory = httpClientFactory;
        _graphOptions = graphOptions.Value;
        _recipientService = recipientService;
    }

    public async Task SendMailAsync(SendMailRequest request, CancellationToken cancellationToken)
    {
        ValidateSettings();

        var recipients = await _recipientService.GetRecipientEmailAddressesAsync(cancellationToken);
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var graphClient = _httpClientFactory.CreateClient();
        graphClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var graphPayload = new
        {
            message = new
            {
                subject = request.Subject,
                body = new
                {
                    contentType = "HTML",
                    content = request.BodyHtml
                },
                toRecipients = recipients.Select(email => new
                {
                    emailAddress = new
                    {
                        address = email
                    }
                })
            },
            saveToSentItems = true
        };

        var response = await graphClient.PostAsync(
            $"https://graph.microsoft.com/v1.0/users/{_graphOptions.SenderUserId}/sendMail",
            new StringContent(JsonSerializer.Serialize(graphPayload), Encoding.UTF8, "application/json"),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Graph sendMail failed with status {(int)response.StatusCode}: {responseContent}");
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var authority = $"https://login.microsoftonline.com/{_graphOptions.TenantId}/oauth2/v2.0/token";
        var tokenRequest = new Dictionary<string, string>
        {
            ["client_id"] = _graphOptions.ClientId,
            ["client_secret"] = _graphOptions.ClientSecret,
            ["scope"] = _graphOptions.Scope,
            ["grant_type"] = "client_credentials"
        };

        var authClient = _httpClientFactory.CreateClient();
        var tokenResponse = await authClient.PostAsync(
            authority,
            new FormUrlEncodedContent(tokenRequest),
            cancellationToken);

        var tokenContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Unable to acquire access token: {(int)tokenResponse.StatusCode} {tokenContent}");
        }

        using var jsonDocument = JsonDocument.Parse(tokenContent);
        if (!jsonDocument.RootElement.TryGetProperty("access_token", out var tokenElement))
        {
            throw new InvalidOperationException("Token response did not contain access_token.");
        }

        return tokenElement.GetString() ?? throw new InvalidOperationException("access_token is empty.");
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_graphOptions.TenantId) ||
            string.IsNullOrWhiteSpace(_graphOptions.ClientId) ||
            string.IsNullOrWhiteSpace(_graphOptions.ClientSecret) ||
            string.IsNullOrWhiteSpace(_graphOptions.SenderUserId))
        {
            throw new InvalidOperationException(
                $"Graph settings are incomplete. Configure {GraphOptions.SectionName}:TenantId, ClientId, ClientSecret, and SenderUserId.");
        }
    }
}
