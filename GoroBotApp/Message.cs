using System;
using System.Collections.Generic;
using System.Text;

namespace GoroBotApp
{
    public class ReplyObject
    {
        public string replyToken { get; set; }
        public List<Message> messages { get; set; }
        public bool notificationDisabled { get; set; }
    }

    public class WebhookObject
    {
        public string destination { get; set; }
        public Event[] events { get; set; }
    }

    public class Event
    {
        public string replyToken { get; set; }
        public string type { get; set; }
        public long timestamp { get; set; }
        public Source source { get; set; }
        public Message message { get; set; }
    }

    public class Source
    {
        public string type { get; set; }
        public string userId { get; set; }
    }

    public class Message
    {
        public string id { get; set; }
        public string type { get; set; }
        public string text { get; set; }
        public string title { get; set; }
        public string address { get; set; }
        public float latitude { get; set; }
        public float longitude { get; set; }
        public QuickReplyItems quickReply { get; set; }
    }

    public class QuickReplyAction
    {
        public string type { get; set; }
        public string label { get; set; }
        public string text { get; set; }
        public string data { get; set; }
        public string displayText { get; set; }
    }

    public class QuickReplyItem
    {
        public string type { get; set; }
        public QuickReplyAction action { get; set; }
    }
    public class QuickReplyItems
    {
        public List<QuickReplyItem> items { get; set; }
    }

}
