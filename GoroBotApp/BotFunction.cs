using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace GoroBotApp
{
    public static class BotFunction
    {
        private const string _replyUrl = "https://api.line.me/v2/bot/message/reply";
        private static HttpClient _httpClient = new HttpClient();

        [FunctionName("BotFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            req.Headers.TryGetValue("X-Line-Signature", out var xlinesignature);
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (!ValidateSignature(xlinesignature, requestBody, Settings.ChannelSecret))
            {
                log.LogInformation("Signature verification fail.");
                return new BadRequestResult();
            }

            var data = JsonConvert.DeserializeObject<WebhookObject>(requestBody);
            if (data.events[0].type != "message")
            {
                log.LogInformation("Data type was not appropriate.");
                return new BadRequestResult();
            }
            log.LogInformation("Message is : " + data.events[0].message.text);

            await SendReplyAsync(data.events[0].replyToken, data.events[0].message.text);
            return new OkResult();
        }

        private static bool ValidateSignature(string signature, string requestBody, string channelSecret)
        {
            var textBytes = Encoding.UTF8.GetBytes(requestBody);
            var keyBytes = Encoding.UTF8.GetBytes(channelSecret);

            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                var hash = hmac.ComputeHash(textBytes, 0, textBytes.Length);
                var hash64 = Convert.ToBase64String(hash);

                return signature == hash64;
            }
        }

        private static async Task SendReplyAsync(string replyToken, string message)
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.AccessToken);

            var replyBody = new ReplyObject
            {
                replyToken = replyToken,
                messages = new List<Message>()
                {
                    new Message{
                        type = "text",
                        text = message
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(_replyUrl, replyBody);
            response.EnsureSuccessStatusCode();
        }
    }
}
