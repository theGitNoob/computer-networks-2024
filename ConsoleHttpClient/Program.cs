namespace ConsoleHttpClient;

class Program
{
    static void Main(string[] args)
    {
        // Create an instance of the HTTP client
        var client = new Client.HttpClient("github.com", 443, true);
        
        // Send a GET request
        var (headers, body) = client.Get("/");
        Console.WriteLine("GET /get");
        Console.WriteLine("Headers: " + string.Join(", ", headers.Select(x => $"{x.Key}: {x.Value}")));
        Console.WriteLine("Body: " + body);

        // Send a POST request
        (headers, body) = client.Post("/post", "This is a POST request");
        Console.WriteLine("POST /post");
        Console.WriteLine("Headers: " + string.Join(", ", headers.Select(x => $"{x.Key}: {x.Value}")));
        Console.WriteLine("Body: " + body);
        
        // Test PUT request
        (headers, body) = client.Put("/put", "This is a PUT request");
        Console.WriteLine("PUT /put");
        Console.WriteLine("Headers: " + string.Join(", ", headers.Select(x => $"{x.Key}: {x.Value}")));
        Console.WriteLine("Body: " + body);
        
        // Test DELETE request
        (headers, body) = client.Delete("/delete");
        Console.WriteLine("DELETE /delete");
        Console.WriteLine("Headers: " + string.Join(", ", headers.Select(x => $"{x.Key}: {x.Value}")));
        Console.WriteLine("Body: " + body);
        
        // Test PATCH request
        (headers, body) = client.Patch("/patch","This is a PATCH request");
        Console.WriteLine("PATCH /patch");
        Console.WriteLine("Headers: " + string.Join(", ", headers.Select(x => $"{x.Key}: {x.Value}")));
        Console.WriteLine("Body: " + body);
    }
}