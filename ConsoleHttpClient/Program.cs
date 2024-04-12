using System.Text;
using System.Text.Json;
using Spectre.Console;
using HttpClient = Client.HttpClient;

namespace ConsoleHttpClient
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            // Prompt for the host URL and port
            var host = AnsiConsole.Ask<string>("Please enter the host URL:");
            var port = AnsiConsole.Ask<int>("Please enter the port:");

            var useHttps = port == 443;

            // Create an instance of HttpClient
            var httpClient = new HttpClient(host, port, useHttps);

            // Prompt for the HTTP method
            var httpMethod = AnsiConsole.Prompt(
                new SelectionPrompt<HttpMethod>()
                    .Title("Please select an HTTP method:")
                    .PageSize(10)
                    .AddChoices(Enum.GetValues<HttpMethod>())
            );

            AnsiConsole.MarkupLine($"You selected [bold green]{httpMethod}[/]");

            // Prompt for the request headers
            var headers = new Dictionary<string, string>();
            while (AnsiConsole.Confirm("Do you want to add a request header?"))
            {
                var headerName = AnsiConsole.Ask<string>("Enter the header name:");
                var headerValue = AnsiConsole.Ask<string>("Enter the header value:");
                headers[headerName] = headerValue;
            }

            string? body = null;
            if (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put)
            {
                // Prompt for the body and 'Content-Type' header if the method is POST or PUT
                body = AnsiConsole.Ask<string>("Please enter the request body:");
                var contentType = AnsiConsole.Ask<string>("Please enter the 'Content-Type' header:");
                headers["Content-Type"] = contentType;

                // If the 'Content-Type' is 'application/json', check if the body is a valid JSON
                if (contentType.Equals("application/json"))
                {
                    try
                    {
                        JsonDocument.Parse(body);
                    }
                    catch (JsonException)
                    {
                        AnsiConsole.WriteLine("The body is not a valid JSON.");
                        return 1; // Exit the program with an error code
                    }
                }

                // Calculate the Content-Length and add it to the headers
                byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                int contentLength = bodyBytes.Length;
                headers["Content-Length"] = contentLength.ToString();
            }

            // Send the request
            var (responseHeaders, responseBody) = httpMethod switch
            {
                HttpMethod.Get => httpClient.Get("/", headers),
                HttpMethod.Post => httpClient.Post("/", body, headers),
                HttpMethod.Put => httpClient.Put("/", body, headers),
                HttpMethod.Delete => httpClient.Delete("/", headers),
                HttpMethod.Trace => httpClient.Trace("/", headers),
                HttpMethod.Head => httpClient.Head("/", headers),
                HttpMethod.Options => httpClient.Options("/", headers),
                _ => throw new NotSupportedException($"Unsupported HTTP method: {httpMethod}")
            };

            // Print the response headers
            AnsiConsole.WriteLine(
                $"Response headers:\n {string.Join("\n", responseHeaders!.Select(h => $"{h.Key}: {h.Value}"))}");

            // Check if the response is JSON
            if (responseHeaders!.ContainsKey("Content-Type") &&
                responseHeaders["Content-Type"].Equals("application/json"))
            {
                var jsonDocument = JsonDocument.Parse(responseBody);
                responseBody =
                    JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions { WriteIndented = true });
            }

            AnsiConsole.WriteLine($"Response body:\n {responseBody}");
            return 0;
        }
    }

    enum HttpMethod
    {
        Get,
        Post,
        Put,
        Delete,
        Head,
        Trace,
        Options
    }
}