namespace ConsoleHttpClient;

class Program
{
    static void Main(string[] args)
    {
        // Create an instance of the HTTP client
        var client = new Client.HttpClient("localhost",5258);

        // Send a GET request
        var (headers, body) = client.SendRequest("GET", "/airports");
        Console.WriteLine("GET /get");
        Console.WriteLine("Headers: " + string.Join(", ", headers.Select(x => $"{x.Key}: {x.Value}")));
        Console.WriteLine("Body: " + body);

        // Send a POST request
        (headers, body) = client.SendRequest("POST", "/post", body: "Hello, world!");
        Console.WriteLine("POST /post");
        Console.WriteLine("Headers: " + string.Join(", ", headers.Select(x => $"{x.Key}: {x.Value}")));
        Console.WriteLine("Body: " + body);
        
        // Test PUT request
        (headers, body) = client.SendRequest("PUT", "/put", body: "This is a PUT request");
        Console.WriteLine("PUT /put");
        Console.WriteLine("Headers: " + string.Join(", ", headers.Select(x => $"{x.Key}: {x.Value}")));
        Console.WriteLine("Body: " + body);
        
        // Test DELETE request
        (headers, body) = client.SendRequest("DELETE", "/delete");
        Console.WriteLine("DELETE /delete");
        Console.WriteLine("Headers: " + string.Join(", ", headers.Select(x => $"{x.Key}: {x.Value}")));
        Console.WriteLine("Body: " + body);
        
        // Test PATCH request
        (headers, body) = client.SendRequest("PATCH", "/patch", body: "This is a PATCH request");
        Console.WriteLine("PATCH /patch");
        Console.WriteLine("Headers: " + string.Join(", ", headers.Select(x => $"{x.Key}: {x.Value}")));
        Console.WriteLine("Body: " + body);
    }
}