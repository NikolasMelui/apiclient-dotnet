using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Voximplant.API.Request;
using Voximplant.API.Response;

namespace Voximplant.API
{
    public partial class VoximplantAPI
    {
        private readonly ClientConfiguration _configuration;
        private readonly Credentials _credentials;
        private readonly IDictionary<ILogger, LogSeverity> _loggers;

        /// <summary>
        /// 
        /// </summary>
        public VoximplantAPI() : this(new ClientConfiguration())
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="configuration"></param>
        public VoximplantAPI(ClientConfiguration configuration)
        {
            _loggers = new Dictionary<ILogger, LogSeverity>();
            _configuration = configuration;
            _credentials = Credentials.Load(_configuration.KeyFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="severity"></param>
        /// <param name="logger"></param>
        public void AddLogger(LogSeverity severity, ILogger logger)
        {
            if (_loggers.ContainsKey(logger))
            {
                _loggers[logger] = severity;
            }
            else
            {
                _loggers.Add(logger, severity);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public void RemoveLogger(ILogger logger)
        {
            if (_loggers.ContainsKey(logger))
            {
                _loggers.Remove(logger);
            }
        }

        private void Log(LogSeverity severity, string message)
        {
            foreach (var logger in _loggers)
            {
                if ((int)logger.Value >= (int)severity)
                {
                    logger.Key.Log(severity, message);
                }
            }
        }

        private string GetAuthorizationHeader()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var credentials = new SigningCredentials(_credentials.PrivateKey, SecurityAlgorithms.RsaSha256);

            var token = tokenHandler.CreateJwtSecurityToken(issuer: _credentials.AccountId.ToString(),
                signingCredentials: credentials);

            var tokenString = tokenHandler.WriteToken(token);

            return $"Bearer {tokenString}";
        }

        private Uri RequestUri(string action)
        {
            var uri = $"http{(_configuration.UseHTTPS ? "s" : "")}://{_configuration.APIHost}/platform_api/{action}";
            return new Uri(uri);
        }

        private async Task<TResponse> PerformRequest<TResponse, TRequest>(string node, TRequest request)
            where TRequest : BaseRequest
            where TResponse : BaseResponse
        {
            using (var client = new HttpClient())
            {
                request.SetAccountId(_credentials.AccountId);
                var requestBody = new FormUrlEncodedContent(request);

                var uri = RequestUri(node);
                var authHeader = GetAuthorizationHeader();
                Log(LogSeverity.Info, $"Requesting: {uri}, Authorization: {authHeader}");
                Log(LogSeverity.Verbose, $"Body: {string.Join(",", request)}");

                client.DefaultRequestHeaders.Add("Authorization", authHeader);
                var response = await client.PostAsync(uri, requestBody);
                Log(LogSeverity.Info, $"Response code: {(int)response.StatusCode}");
                var responseString = await response.Content.ReadAsStringAsync();
                Log(LogSeverity.Verbose, $"Response: {responseString}");

                var result = JsonConvert.DeserializeObject<TResponse>(responseString, new JsonSerializerSettings
                {
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                    TraceWriter = _configuration.TraceWriter,
                });

                if (result == null)
                {
                    Log(LogSeverity.Error, "Empty response");
                    throw new APIException("Empty response", 500);
                }

                if (result.HasError())
                {
                    Log(LogSeverity.Error, result.GetError().Msg);
                    throw new APIException(result.GetError().Msg, result.GetError().Code);
                }

                return result;
            }
        }
    }
}