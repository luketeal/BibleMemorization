using System.Net;
using System.Text;

namespace BibleMemorization.Core.Tests.Storage;

/// <summary>
/// Records requests and replays canned responses, so the REST provider can be
/// tested without a server.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses = new(StringComparer.OrdinalIgnoreCase);

    public List<RecordedRequest> Requests { get; } = [];

    public HttpStatusCode DefaultStatus { get; set; } = HttpStatusCode.OK;

    public sealed record RecordedRequest(HttpMethod Method, string Path, string? Body);

    public StubHttpMessageHandler Respond(HttpMethod method, string path, HttpStatusCode status, string body = "")
    {
        _responses[Key(method, path)] = (status, body);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedRequest(request.Method, path, body));

        if (_responses.TryGetValue(Key(request.Method, path), out var canned))
        {
            return new HttpResponseMessage(canned.Status)
            {
                Content = new StringContent(canned.Body, Encoding.UTF8, "application/json"),
            };
        }

        return new HttpResponseMessage(DefaultStatus)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        };
    }

    private static string Key(HttpMethod method, string path) => $"{method} {path.TrimStart('/')}";
}
