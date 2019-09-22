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
                        var quickReplyMessage = GetQuickReplyMessage(0, 0);
                        await SendQuickReplyAsync(data.events[0].replyToken, quickReplyMessage);
                        break;
                    default:
                        log.LogInformation("data.events[0].message.type is : " + data.events[0].message.type);
                        await SendReplyAsync(data.events[0].replyToken, "�Ȃ�ē�����̂������Ȃ񂾁H");
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
                await SendReplyAsync(data.events[0].replyToken, "�Ȃ�ē�����̂������Ȃ񂾁H");
            }
            return new OkResult();
        }

        private static string GetQuickReplyMessage(float lat, float lng)
        {
            var result = new StringBuilder();
            result.Append("�߂��ɂ������X�������B�ǂ��ɍs�������H" + Environment.NewLine);
            result.Append(String.Format("�P�F{0}�i{1}�j", "���{���͉��̂��D�ݏĂ���H�ƕ���̋�����", "�Ðh��") + Environment.NewLine);
            result.Append(String.Format("�Q�F{0}�i{1}�j", "�����s�V�h�旄���s��̓؃o�����I�Ē�H", "�ɐ����H��") + Environment.NewLine);
            result.Append(String.Format("�R�F{0}�i{1}�j", "�����s�ڍ���O�c�̃`�L���Ɩ�؂̖�V�X�[�v�J���[", "�V���i�C�A") + Environment.NewLine);
            return result.ToString();
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
                        text = "���܂ǂ��H",
                        quickReply = new QuickReplyItems{
                            items = new List<QuickReplyItem>
                            {
                                new QuickReplyItem{
                                    type = "action",
                                    action = new QuickReplyAction
                                    {
                                        type = "location",
                                        label = "���P�[�V����"
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

        private static async Task SendQuickReplyAsync(string replyToken, string message)
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
                        text = message,
                        quickReply = new QuickReplyItems{
                            items = new List<QuickReplyItem>
                            {
                                new QuickReplyItem{
                                    type = "action",
                                    action = new QuickReplyAction
                                    {
                                        type = "postback",
                                        label = "�P�Ԗ�",
                                        data = "https://tabelog.com/tokyo/A1318/A131801/13020966/",
                                        displayText = "��`�����˂�"
                                    }
                                },
                                new QuickReplyItem{
                                    type = "action",
                                    action = new QuickReplyAction
                                    {
                                        type = "postback",
                                        label = "�Q�Ԗ�",
                                        data = "https://tabelog.com/tokyo/A1318/A131801/13020966/",
                                        displayText = "��`�����˂�"
                                    }
                                },
                                new QuickReplyItem{
                                    type = "action",
                                    action = new QuickReplyAction
                                    {
                                        type = "postback",
                                        label = "�R�Ԗ�",
                                        data = "https://tabelog.com/tokyo/A1318/A131801/13020966/",
                                        displayText = "��`�����˂�"
                                    }
                                },
                            }
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
