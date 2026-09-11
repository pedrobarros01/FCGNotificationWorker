

using Amazon.SQS;
using Amazon.Runtime;
using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;


public class CustomHttpClientFactory : HttpClientFactory
{
    private readonly HttpClientHandler _handler;

    public CustomHttpClientFactory(HttpClientHandler handler)
    {
        _handler = handler;
    }

    // Corrigido: Agora aceita IClientConfig e utiliza a assinatura correta do SDK moderno
    public override HttpClient CreateHttpClient(IClientConfig config)
    {
        return new HttpClient(_handler);
    }
}
