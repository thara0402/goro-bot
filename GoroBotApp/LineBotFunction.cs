using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GoroBotApp.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace GoroBotApp
{
    public class LineBotFunction
    {
        private const string _replyUrl = "https://api.line.me/v2/bot/message/reply";
        private const string _gourmetUrl = "https://goro-api.azurewebsites.net/api/Gourmet";
        private readonly MyOptions _options;
        private readonly HttpClient _lineClient;
        private readonly HttpClient _gourmetClient;

        public LineBotFunction(IOptions<MyOptions> options, IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _lineClient = httpClientFactory.CreateClient();
            _gourmetClient = httpClientFactory.CreateClient();
        }

        [FunctionName("LineBotFunction")]
        public async Task RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            string requestBody = context.GetInput<string>();
            var data = JsonConvert.DeserializeObject<WebhookObject>(requestBody);
            if (data.events[0].type == "message")
            {
                switch (data.events[0].message.type)
                {
                    case "text":
                        log.LogInformation("text is : " + data.events[0].message.text);
                        await context.CallActivityWithRetryAsync(
                            nameof(SendQuickReplyForLocationAsync), new RetryOptions(TimeSpan.FromSeconds(5), 3)
                            {
                                Handle = ex => ex.InnerException is RetriableException
                            }, data.events[0].replyToken);
                        break;
                    case "location":
                        log.LogInformation("title is : " + data.events[0].message.title);
                        await context.CallActivityWithRetryAsync(
                            nameof(SendQuickReplyAsync), new RetryOptions(TimeSpan.FromSeconds(5), 3)
                            {
                                Handle = ex => ex.InnerException is RetriableException
                            }, new QuickReplyParameter { ReplyToken = data.events[0].replyToken, Latitude = data.events[0].message.latitude, Longitude = data.events[0].message.longitude });
                        break;
                    default:
                        log.LogInformation("data.events[0].message.type is : " + data.events[0].message.type);
                        await context.CallActivityWithRetryAsync(
                            nameof(SendMessageReplyAsync), new RetryOptions(TimeSpan.FromSeconds(5), 3)
                            {
                                Handle = ex => ex.InnerException is RetriableException
                            }, new MessageReplyParameter { ReplyToken = data.events[0].replyToken, Text = "なんて答えるのが正解なんだ？" });
                        break;
                }
            }
            else if (data.events[0].type == "postback")
            {
                log.LogInformation("data is : " + data.events[0].postback.data);
                await context.CallActivityWithRetryAsync(
                    nameof(SendMessageReplyAsync), new RetryOptions(TimeSpan.FromSeconds(5), 3)
                    {
                        Handle = ex => ex.InnerException is RetriableException
                    }, new MessageReplyParameter { ReplyToken = data.events[0].replyToken, Text = data.events[0].postback.data });
            }
            else
            {
                log.LogInformation("data.events[0].type is : " + data.events[0].type);
                await context.CallActivityWithRetryAsync(
                    nameof(SendMessageReplyAsync), new RetryOptions(TimeSpan.FromSeconds(5), 3)
                    {
                        Handle = ex => ex.InnerException is RetriableException
                    }, new MessageReplyParameter { ReplyToken = data.events[0].replyToken, Text = "なんて答えるのが正解なんだ？" });
            }
        }

        [FunctionName(nameof(SendQuickReplyForLocationAsync))]
        public async Task SendQuickReplyForLocationAsync([ActivityTrigger] string replyToken, ILogger log)
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

        [FunctionName(nameof(SendQuickReplyAsync))]
        public async Task SendQuickReplyAsync([ActivityTrigger] QuickReplyParameter parameter, ILogger log)
        {
            var quickReplyMessage = await GetQuickReplyMessageAsync(parameter.Latitude, parameter.Longitude);

            var actions = new List<QuickReplyAction>();
            for (int i = 0; i < quickReplyMessage.Gourmets.Count; i++)
            {
                actions.Add(new QuickReplyAction
                {
                    type = "postback",
                    label = String.Format("{0}番目", i + 1),
                    data = quickReplyMessage.Gourmets[i].Matome,
                    displayText = "ん～いいねぇ"
                });
            }
            await SendQuickReplyAsync(parameter.ReplyToken, quickReplyMessage.Text, actions);
        }

        [FunctionName(nameof(SendMessageReplyAsync))]
        public async Task SendMessageReplyAsync([ActivityTrigger] MessageReplyParameter parameter, ILogger log)
        {
            var message = new Message
            {
                type = "text",
                text = parameter.Text
            };
            await SendReplyAsync(parameter.ReplyToken, message);
        }

        [FunctionName("LineBotFunction_HttpStart")]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            req.Headers.TryGetValues("X-Line-Signature", out var xlinesignature);
            var requestBody = await req.Content.ReadAsStringAsync();
            log.LogInformation("requestBody is : " + requestBody);
            if (!ValidateSignature(xlinesignature.First(), requestBody, _options.LineChannelSecret))
            {
                log.LogInformation("Signature verification fail.");
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("LineBotFunction", requestBody);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
//            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId);
        }

        private async Task<QuickReplyMessage> GetQuickReplyMessageAsync(float lat, float lng)
        {
            var response = await _gourmetClient.GetAsync(String.Format("{0}/{1}/{2}", _gourmetUrl, lat, lng));
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
                quickReply = new QuickReplyItems { items = items }
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
            var response = await _lineClient.PostAsJsonAsync(_replyUrl, replyObject);
            response.EnsureSuccessStatusCode();
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
    }

    public class MessageReplyParameter
    {
        public string ReplyToken { get; set; }
        public string Text { get; set; }
    }

    public class QuickReplyParameter
    {
        public string ReplyToken { get; set; }
        public float Latitude { get; set; }
        public float Longitude { get; set; }
    }
}