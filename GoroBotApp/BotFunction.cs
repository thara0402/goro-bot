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
        private static HttpClient _httpClient2 = new HttpClient();

        [FunctionName("BotFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            req.Headers.TryGetValue("X-Line-Signature", out var xlinesignature);
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogInformation("requestBody is : " + requestBody);
            if (!ValidateSignature(xlinesignature, requestBody, Settings.ChannelSecret))
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
                        var quickReplyMessage = await GetQuickReplyMessageAsync(data.events[0].message.latitude, data.events[0].message.longitude);
                        await SendQuickReplyAsync(data.events[0].replyToken, quickReplyMessage);
                        break;
                    default:
                        log.LogInformation("data.events[0].message.type is : " + data.events[0].message.type);
                        await SendReplyAsync(data.events[0].replyToken, "なんて答えるのが正解なんだ？");
                        break;
                }
            }
            else if (data.events[0].type == "postback")
            {
                log.LogInformation("data is : " + data.events[0].postback.data);
                await SendReplyAsync(data.events[0].replyToken, data.events[0].postback.data);
            }
            else
            {
                log.LogInformation("data.events[0].type is : " + data.events[0].type);
                await SendReplyAsync(data.events[0].replyToken, "なんて答えるのが正解なんだ？");
            }
            return new OkResult();
        }

        private static async Task<QuickReplyMessage> GetQuickReplyMessageAsync(float lat, float lng)
        {
            var response = await _httpClient2.GetAsync(String.Format("https://goro-api.azurewebsites.net/api/Gourmet/{0}/{1}", lat, lng));
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var gourmets = JsonConvert.DeserializeObject<List<Gourmet>>(json);

            var result = new QuickReplyMessage { Gourmets = new List<Gourmet>() };
            for (int i = 0; i < 3; i++)
            {
                if (i == 0)
                {
                    result.Text = "近くにいいお店があるよ。どこに行きたい？" + Environment.NewLine;
                }
                result.Text += String.Format("{0}：{1}（{2}）", i + 1, gourmets[i].Title, gourmets[i].Restaurant) + Environment.NewLine;
                result.Gourmets.Add(gourmets[i]);
            }
            return result;
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

        private static async Task SendQuickReplyForLocationAsync(string replyToken)
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
                        text = "いまどこ？",
                        quickReply = new QuickReplyItems{
                            items = new List<QuickReplyItem>
                            {
                                new QuickReplyItem{
                                    type = "action",
                                    action = new QuickReplyAction
                                    {
                                        type = "location",
                                        label = "ロケーション"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(_replyUrl, replyBody);
            response.EnsureSuccessStatusCode();
        }

        private static async Task SendQuickReplyAsync(string replyToken, QuickReplyMessage message)
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.AccessToken);

            var items = new List<QuickReplyItem>();
            for (int i = 0; i < message.Gourmets.Count; i++)
            {
                items.Add(new QuickReplyItem
                {
                    type = "action",
                    action = new QuickReplyAction
                    {
                        type = "postback",
                        label = String.Format("{0}番目", i + 1),
                        data = message.Gourmets[i].Matome,
                        displayText = "ん～いいねぇ"
                    }
                });
            }

            var replyBody = new ReplyObject
            {
                replyToken = replyToken,
                messages = new List<Message>()
                {
                    new Message{
                        type = "text",
                        text = message.Text,
                        quickReply = new QuickReplyItems{
                            items = items
                        }
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(_replyUrl, replyBody);
            response.EnsureSuccessStatusCode();
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
