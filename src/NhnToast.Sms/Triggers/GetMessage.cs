using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

using Toast.Common.Configurations;
using Toast.Sms.Configurations;
using Toast.Sms.Models;

namespace Toast.Sms.Triggers
{
    public class GetMessage
    {
        private readonly ToastSettings<SmsEndpointSettings> _settings;
        private readonly HttpClient _http;
        private readonly ILogger<GetMessage> _logger;

        public GetMessage(ToastSettings<SmsEndpointSettings> settings, IHttpClientFactory factory, ILogger<GetMessage> log)
        {
            this._settings = settings.ThrowIfNullOrDefault();
            this._http = factory.ThrowIfNullOrDefault().CreateClient("messages");
            this._logger = log.ThrowIfNullOrDefault();
        }

        [FunctionName(nameof(GetMessage))]
        [OpenApiOperation(operationId: "Messages.Get", tags: new[] { "messages" })]
        [OpenApiSecurity("app_key", SecuritySchemeType.ApiKey, Name = "x-app-key", In = OpenApiSecurityLocationType.Header)]
        [OpenApiSecurity("secret_key", SecuritySchemeType.ApiKey, Name = "x-secret-key", In = OpenApiSecurityLocationType.Header)]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header)]
        [OpenApiParameter(name: "requestId", Type = typeof(string), In = ParameterLocation.Path, Required = true, Description = "SMS request ID")]
        [OpenApiParameter(name: "recipientSeq", Type = typeof(string), In = ParameterLocation.Query, Required = true, Description = "SMS request sequence number")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "GET", Route = "messages/{requestId:regex(^\\d+\\w+$)}")] HttpRequest req,
            string requestId)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var appKey = req.Headers["x-app-key"].ToString();
            var secretKey = req.Headers["x-secret-key"].ToString();
            var baseUrl = this._settings.BaseUrl;
            var version = this._settings.Version;
            var endpoint = this._settings.Endpoints.GetMessage;
            var options = new GetMessageRequestUrlOptions()
            {
                Version = version,
                AppKey = appKey,
                RequestId = requestId,
                RecipientSeq = int.TryParse(req.Query["recipientSeq"].ToString(), out int recipientSeqVal) ? recipientSeqVal : 0,
            };
            var requestUrl = this._settings.Formatter.Format($"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}", options);

            this._http.DefaultRequestHeaders.Add("X-Secret-Key", secretKey);
            var result = await this._http.GetAsync(requestUrl).ConfigureAwait(false);

            var payload = await result.Content.ReadAsAsync<object>().ConfigureAwait(false);

            return new OkObjectResult(payload);
        }
    }
}