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

    public HttpClient(string host, int port = 8000, bool useHttps = false)
    {
        this.host = host;
        this.port = port;
        this.useHttps = useHttps;
        cookies = new Dictionary<string, string>();
    }

    public (Dictionary<string, string>? headers, string? body) SendRequest(string method, string path,
        Dictionary<string, string>? headers = null, string? body = null, string? username = null,
        string? password = null)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
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

            headers.TryAdd("Host", this.host+":"+port);

            headers.TryAdd("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/89.0.4389.114 Safari/537.36");
            
            headers.TryAdd("Accept", "*/*");

            var request = $"{method} {path} HTTP/1.1\r\n" +
                          string.Join("\r\n", headers.Select(x => $"{x.Key}: {x.Value}")) + "\r\n\r\n";

            if (body != null)
            {
                request += body;
            }

            var requestBytes = Encoding.ASCII.GetBytes(request);
            stream.Write(requestBytes, 0, requestBytes.Length);

            var response = ReceiveResponse(stream);
            var responseHeaders = response.headers;

            if (responseHeaders != null && responseHeaders["Status"].StartsWith("30"))
            {
                var newUrl = responseHeaders["Location"];
                stream.Close();
                socket.Close();
                stream.Dispose();
                return SendRequest(method, newUrl, headers, body);
            }

            stream.Close();
            socket.Close();
            stream.Dispose();
            return response;
        }
        catch (SocketException e)
        {
            Console.WriteLine("SocketException: {0}", e);
        }

        return (null,null);
    }

    private (Dictionary<string, string>? headers, string? body ) ReceiveResponse(Stream stream)
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
                var statusCode = GetStatus(line);
                headerDict["Status"] = statusCode;
            }
        }


        ProcessResponseHeaders(headerDict);
        body = ProcessResponseBody(stream, headerDict);

        return (headerDict, body);
    }

    private string GetStatus(string line)
    {
        return line.Substring(9, 3);

    }

    private string ProcessResponseBody(Stream stream, Dictionary<string, string> headerDict)
    {
        StringBuilder body = new StringBuilder();
        if (headerDict.ContainsKey("Transfer-Encoding") && headerDict["Transfer-Encoding"].Equals("chunked", StringComparison.OrdinalIgnoreCase))
        {
            bool readingChunks = true;
            while (readingChunks)
            {
                string line = ReadLineFromStream(stream);
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                if (!int.TryParse(line, System.Globalization.NumberStyles.HexNumber, null, out int chunkSize))
                {
                    throw new FormatException($"Invalid chunk size: {line}");
                }

                if (chunkSize == 0)
                {
                    while (!string.IsNullOrEmpty(ReadLineFromStream(stream))) {}
                    break;
                }

                byte[] chunkBytes = new byte[chunkSize];
                int bytesRead = 0;
                while (bytesRead < chunkSize)
                {
                    int read = stream.Read(chunkBytes, bytesRead, chunkSize - bytesRead);
                    if (read == 0)
                    {
                        throw new InvalidOperationException("Unexpected end of stream");
                    }
                    bytesRead += read;
                }

                body.Append(Encoding.UTF8.GetString(chunkBytes, 0, bytesRead));
            
                ReadLineFromStream(stream);
            }
        }
        else if (headerDict.ContainsKey("Content-Length"))
        {
            if (int.TryParse(headerDict["Content-Length"], out int contentLength))
            {
                byte[] buffer = new byte[contentLength];
                int totalBytesRead = 0;
                while (totalBytesRead < contentLength)
                {
                    int bytesRead = stream.Read(buffer, totalBytesRead, contentLength - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        throw new InvalidOperationException("Unexpected end of stream");
                    }
                    totalBytesRead += bytesRead;
                }
                body.Append(Encoding.UTF8.GetString(buffer, 0, totalBytesRead));
            }
            else
            {
                throw new FormatException($"Invalid Content-Length: {headerDict["Content-Length"]}");
            }
        }

        return body.ToString();
    }

    private string ReadLineFromStream(Stream stream)
    {
        StringBuilder stringBuilder = new StringBuilder();
        byte[] buffer = new byte[1];
        while (stream.Read(buffer, 0, 1) > 0)
        {
            char c = (char)buffer[0];
            if (c == '\n')
            {
                break;
            }
            if (c != '\r')
            {
                stringBuilder.Append(c);
            }
        }
        return stringBuilder.ToString();
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
    public (Dictionary<string, string>? headers, string? body) Get(string path,
        Dictionary<string, string>? headers = null)
    {
        return SendRequest("GET", path, headers);
    }

    public (Dictionary<string, string>? headers, string? body) Post(string path, string? body,
        Dictionary<string, string>? headers = null)
    {
        return SendRequest("POST", path, headers, body);
    }

    public (Dictionary<string, string>? headers, string? body) Put(string path, string? body,
        Dictionary<string, string>? headers = null)
    {
        return SendRequest("PUT", path, headers, body);
    }

    public (Dictionary<string, string>? headers, string? body) Delete(string path,
        Dictionary<string, string>? headers = null)
    {
        return SendRequest("DELETE", path, headers);
    }

    public (Dictionary<string, string>? headers, string? body) Trace(string path,
        Dictionary<string, string>? headers = null)
    {
        return SendRequest("TRACE", path, headers);
    } 
    public (Dictionary<string, string>? headers, string? body) Head(string path,
        Dictionary<string, string>? headers = null)
    {
        return SendRequest("HEAD", path, headers);
    }  
    public (Dictionary<string, string>? headers, string? body) Options(string path,
        Dictionary<string, string>? headers = null)
    {
        return SendRequest("OPTIONS", path, headers);
    }
}