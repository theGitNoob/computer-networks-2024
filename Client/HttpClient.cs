using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace Client;

public class HttpClient
{
    private string host;
    private int port;
    private bool useHttps;
    private Dictionary<string, string> cookies;
    private Dictionary<string, string> lastResponseHeaders;

    public HttpClient(string host, int port = 8000, bool useHttps = false)
    {
        this.host = host;
        this.port = port;
        this.useHttps = useHttps;
        cookies = new Dictionary<string, string>();
        lastResponseHeaders = new Dictionary<string, string>();
    }

    public (Dictionary<string, string>? headers, string? body) SendRequest(string method, string path,
        Dictionary<string, string>? headers = null, string? body = null, string? username = null,
        string? password = null)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // socket.SendTimeout = 1;
            socket.Connect(host, port);

            Stream stream = new NetworkStream(socket);
            if (useHttps)
            {
                var sslStream = new SslStream(stream);
                sslStream.AuthenticateAsClient(host);
                stream = sslStream;
            }

            headers ??= new Dictionary<string, string>();

            if (username != null && password != null)
            {
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password));
                headers["Authorization"] = "Basic " + credentials;
            }

            if (cookies.Count > 0)
            {
                var cookieHeader = string.Join("; ", cookies.Select(x => $"{x.Key}={x.Value}"));
                headers["Cookie"] = cookieHeader;
            }

            headers.TryAdd("Host", this.host);

            headers.TryAdd("User-Agent", "MyHttpClient/1.0");

            headers.TryAdd("Accept", "*/*");

            var request = $"{method} {path} HTTP/1.1\r\n" +
                          string.Join("\r\n", headers.Select(x => $"{x.Key}: {x.Value}")) + "\r\n\r\n";

            if (body != null)
            {
                request += "\r\n" + body;
            }

            var requestBytes = Encoding.ASCII.GetBytes(request);
            stream.Write(requestBytes, 0, requestBytes.Length);

            ReceiveResponse(stream);

            // if (headers["Status"].StartsWith("30"))
            // {
            //     var newUrl = headers["Location"];
            //     stream.Close();
            //     socket.Close();
            //     stream.Dispose();
            //     socket.Dispose();
            //     return SendRequest(method, newUrl, headers, body);
            // }

            stream.Close();
            socket.Close();
            stream.Dispose();
            socket.Dispose();
        }
        catch (SocketException e)
        {
            Console.WriteLine("SocketException: {0}", e);
        }

        return (headers, body);
    }

    private void ReceiveResponse(Stream stream)
    {
        // Read the response headers
        var headers = "";
        var buffer = new byte[1];
        while (true)
        {
            var read = stream.Read(buffer, 0, 1);
            headers += Encoding.ASCII.GetString(buffer);
            if (read == 0 || headers.EndsWith("\r\n\r\n"))
                break;
        }

        // Split the headers from the body
        var parts = headers.Split(["\r\n\r\n"], StringSplitOptions.None);
        headers = parts[0];
        var body = parts.Length > 1 ? parts[1] : "";

        // Convert the headers into a dictionary
        var headerDict = new Dictionary<string, string>();
        var headerLines = headers.Split(["\r\n"], StringSplitOptions.None);
        foreach (var line in headerLines)
        {
            if (line.Contains(":"))
            {
                var headerParts = line.Split(new string[] { ": " }, StringSplitOptions.None);
                headerDict[headerParts[0]] = headerParts[1];
            }
            else
            {
                headerDict["Status"] = line;
            }
        }

        ProcessResponseHeaders(headerDict);
        body = ProcessResponseBody(stream, headerDict, body);
        // Print the response headers and body
        Console.WriteLine("Received response headers: " +
                          string.Join(", ", headerDict.Select(x => $"{x.Key}: {x.Value}")));
        Console.WriteLine("Received response body: " + body);
    }

    private string ProcessResponseBody(Stream stream, Dictionary<string, string> headerDict, string body)
    {
        if (headerDict.ContainsKey("Transfer-Encoding") && headerDict["Transfer-Encoding"] == "chunked")
        {
            // If the response is chunked, read the chunks and join them to form the full response body
            var chunks = new List<string>();
            while (true)
            {
                var line = "";
                while (!line.EndsWith("\r\n"))
                {
                    var buffer = new byte[1];
                    stream.Read(buffer, 0, 1);
                    line += Encoding.ASCII.GetString(buffer);
                }

                var chunkLength = int.Parse(line.TrimEnd('\r', '\n'), System.Globalization.NumberStyles.HexNumber);

                if (chunkLength == 0)
                {
                    break;
                }

                var chunk = "";
                while (chunk.Length < chunkLength)
                {
                    var buffer = new byte[Math.Min(chunkLength - chunk.Length, 1024)];
                    stream.Read(buffer, 0, buffer.Length);
                    chunk += Encoding.ASCII.GetString(buffer);
                }

                chunks.Add(chunk);

                stream.Read(new byte[2], 0, 2); // Read the trailing \r\n after each chunk
            }

            body = string.Join("", chunks);
        }
        else if (headerDict.ContainsKey("Content-Length"))
        {
            // If the response includes a 'Content-Length' header, read the response body until the specified amount of data has been read
            var remaining = int.Parse(headerDict["Content-Length"]) - body.Length;
            while (remaining > 0)
            {
                var buffer = new byte[Math.Min(remaining, 1024)];
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    break;
                }

                body += Encoding.ASCII.GetString(buffer, 0, bytesRead);
                remaining -= bytesRead;
            }
        }

        return body;
    }

    private void ProcessResponseHeaders(Dictionary<string, string> headerDict)
    {
        foreach (var header in headerDict)
        {
            if (header.Key.ToLower() == "set-cookie")
            {
                // If the response includes a cookie, store it
                var cookieParts = header.Value.Split(new string[] { "; " }, StringSplitOptions.None);
                var cookieNameValue = cookieParts[0].Split(new string[] { "=" }, StringSplitOptions.None);
                cookies[cookieNameValue[0]] = cookieNameValue[1];
            }
            else if (header.Key.ToLower() == "content-encoding")
            {
                // Support for the 'Content-Encoding' header
                Console.WriteLine($"The content is encoded with: {header.Value}");
            }
            else if (header.Key.ToLower() == "last-modified")
            {
                // Support for the 'Last-Modified' header
                Console.WriteLine($"The content was last modified on: {header.Value}");
            }
        }
    }

  
}