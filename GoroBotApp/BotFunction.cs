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
using Microsoft.Extensions.Options;
using GoroBotApp.Models;

namespace GoroBotApp
{
    public class BotFunction
    {
        private readonly MyOptions _options;
        private readonly HttpClient _lineClient;
        private readonly HttpClient _gourmetClient;

        public BotFunction(IOptions<MyOptions> options, IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _lineClient = httpClientFactory.CreateClient();
            _gourmetClient = httpClientFactory.CreateClient();
        }

        [FunctionName(nameof(BotFunction))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "v1/linebots")] HttpRequest req,
            ILogger log)
        {
            req.Headers.TryGetValue("X-Line-Signature", out var xlinesignature);
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogInformation("requestBody is : " + requestBody);
            if (!ValidateSignature(xlinesignature, requestBody, _options.LineChannelSecret))
            {
                log.LogInformation("Signature verification fail.");
                return new BadRequestResult();
            }

            var data = JsonConvert.DeserializeObject<WebhookObject>(requestBody);
            if (data.events[0].type == "message")
            {
                switch (data.events[0].message.type)
                {
                    case "text":
                        log.LogInformation("text is : " + data.events[0].message.text);
                        await SendQuickReplyForLocationAsync(data.events[0].replyToken);
                        break;
                    case "location":
                        log.LogInformation("title is : " + data.events[0].message.title);
                        await SendQuickReplyAsync(data.events[0].replyToken, data.events[0].message.latitude, data.events[0].message.longitude);
                        break;
                    default:
                        log.LogInformation("data.events[0].message.type is : " + data.events[0].message.type);
                        await SendMessageReplyAsync(data.events[0].replyToken, "なんて答えるのが正解なんだ？");
                        break;
                }
            }
            else if (data.events[0].type == "postback")
            {
                log.LogInformation("data is : " + data.events[0].postback.data);
                await SendMessageReplyAsync(data.events[0].replyToken, data.events[0].postback.data);
            }
            else
            {
                log.LogInformation("data.events[0].type is : " + data.events[0].type);
                await SendMessageReplyAsync(data.events[0].replyToken, "なんて答えるのが正解なんだ？");
            }
            return new OkResult();
        }

        private bool ValidateSignature(string signature, string requestBody, string channelSecret)
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

        private async Task SendQuickReplyForLocationAsync(string replyToken)
        {
            var actions = new List<QuickReplyAction>{
                new QuickReplyAction
                {
                    type = "location",
                    label = "ロケーション"
                }
            };
            await SendQuickReplyAsync(replyToken, "いまどこ？", actions);
        }

        private async Task SendQuickReplyAsync(string replyToken, float lat, float lng)
        {
            var quickReplyMessage = await GetQuickReplyMessageAsync(lat, lng);

            var actions = new List<QuickReplyAction>();
            for (int i = 0; i < quickReplyMessage.Gourmets.Count; i++)
            {
                actions.Add(new QuickReplyAction
                {
                    type = "postback",
                    label = quickReplyMessage.Gourmets[i].Restaurant,
                    data = quickReplyMessage.Gourmets[i].Matome,
                    displayText = "ん～いいねぇ"
                });
            }
            await SendQuickReplyAsync(replyToken, quickReplyMessage.Text, actions);
        }

        private async Task SendQuickReplyAsync(string replyToken, string text, List<QuickReplyAction> actions)
        {
            var items = new List<QuickReplyItem>();
            foreach (var action in actions)
            {
                items.Add(new QuickReplyItem
                {
                    type = "action",
                    action = action
                });
            }

            var message = new Message
            {
                type = "text",
                text = text,
                quickReply = new QuickReplyItems{ items = items }
            };
            await SendReplyAsync(replyToken, message);
        }

        private async Task<QuickReplyMessage> GetQuickReplyMessageAsync(float lat, float lng)
        {
            var response = await _gourmetClient.GetAsync($"{_options.GourmetApiUrl}/{lat}/{lng}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var gourmets = JsonConvert.DeserializeObject<List<Gourmet>>(json);

            var result = new QuickReplyMessage { Gourmets = new List<Gourmet>() };
            for (int i = 0; i < 3; i++)
            {
                if (i == 0)
                {
                    result.Text = "近くにいいお店があるよ。" + Environment.NewLine;
                }
                result.Text += $"{i + 1}：{gourmets[i].Title}（{gourmets[i].Restaurant}）" + Environment.NewLine;
                result.Gourmets.Add(gourmets[i]);
            }
            return result;
        }

        private async Task SendMessageReplyAsync(string replyToken, string text)
        {
            var message = new Message
            {
                type = "text",
                text = text
            };
            await SendReplyAsync(replyToken, message);
        }

        private async Task SendReplyAsync(string replyToken, Message message)
        {
            _lineClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _lineClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.LineAccessToken);

            var replyObject = new ReplyObject
            {
                replyToken = replyToken,
                messages = new List<Message> { message }
            };
            var response = await _lineClient.PostAsJsonAsync(_options.LineMessageApiUrl, replyObject);
            response.EnsureSuccessStatusCode();
        }
    }
}
