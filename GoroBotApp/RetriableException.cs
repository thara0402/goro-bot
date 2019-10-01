using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace GoroBotApp
{
    [Serializable]
    public class RetriableException : Exception
    {
        public RetriableException()
            : base()
        {
        }

        public RetriableException(string message)
            : base(message)
        {
        }

        public RetriableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public RetriableException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }
    }
}
