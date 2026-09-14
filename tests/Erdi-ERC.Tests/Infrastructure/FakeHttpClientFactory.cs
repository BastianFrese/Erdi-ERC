using System.Net;
using System.Text;
using System.Text.Json;

namespace Erdi_ERC.Tests.Infrastructure;

/// <summary>
/// Test-Ersatz für <see cref="IHttpClientFactory"/>: liefert einen Client, dessen
/// <see cref="HttpMessageHandler"/> pro Request eine konfigurierbare Antwort zurückgibt
/// (z. B. Discord /users/@me oder /users/@me/guilds). Der letzte Request wird festgehalten,
/// damit Tests URL, Header und Body prüfen können.
/// </summary>
public sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public FakeHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        _responder = responder;

    public HttpRequestMessage? LastRequest { get; private set; }

    public HttpClient CreateClient(string name) =>
        new(new RecordingHandler(this));

    public static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    public static HttpResponseMessage Error(HttpStatusCode status) => new(status);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly FakeHttpClientFactory _owner;

        public RecordingHandler(FakeHttpClientFactory owner) => _owner = owner;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _owner.LastRequest = request;
            return Task.FromResult(_owner._responder(request));
        }
    }
}
