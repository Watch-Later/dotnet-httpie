using System;
using System.Net.Http;
using System.Threading.Tasks;
using HTTPie.Abstractions;
using HTTPie.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HTTPie.Implement
{
    public class RequestExecutor : IRequestExecutor
    {
        private readonly Func<HttpClientHandler, Task> _httpHandlerPipeline;
        private readonly IRequestMapper _requestMapper;
        private readonly Func<HttpRequestModel, Task> _requestPipeline;
        private readonly IResponseMapper _responseMapper;
        private readonly Func<HttpContext, Task> _responsePipeline;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RequestExecutor> _logger;

        public RequestExecutor(
            IRequestMapper requestMapper,
            IResponseMapper responseMapper,
            Func<HttpClientHandler, Task> httpHandlerPipeline,
            Func<HttpRequestModel, Task> requestPipeline,
            Func<HttpContext, Task> responsePipeline,
            IServiceProvider serviceProvider,
            ILogger<RequestExecutor> logger
        )
        {
            _requestMapper = requestMapper;
            _responseMapper = responseMapper;
            _httpHandlerPipeline = httpHandlerPipeline;
            _requestPipeline = requestPipeline;
            _responsePipeline = responsePipeline;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<HttpResponseModel> ExecuteAsync(HttpRequestModel requestModel)
        {
            using var httpClientHandler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };
            await _httpHandlerPipeline(httpClientHandler);
            await _requestPipeline(requestModel);
            using var requestMessage = await _requestMapper.ToRequestMessage(requestModel);
            _logger.LogDebug($"Request message: {requestMessage.Method.Method.ToUpper()} {requestMessage.RequestUri.AbsoluteUri} HTTP/{requestMessage.Version.ToString(2)}");
            using var httpClient = new HttpClient(httpClientHandler);
            using var responseMessage = await httpClient.SendAsync(requestMessage);
            _logger.LogDebug($"Response message: HTTP/{responseMessage.Version.ToString(2)} {(int)responseMessage.StatusCode} {responseMessage.StatusCode}");
            var responseModel = await _responseMapper.ToResponseModel(responseMessage);
            using var scope = _serviceProvider.CreateScope();
            var context = new HttpContext(requestModel, responseModel, scope.ServiceProvider);
            await _responsePipeline(context);
            return responseModel;
        }
    }
}