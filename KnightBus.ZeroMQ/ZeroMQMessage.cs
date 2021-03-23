using KnightBus.ZeroMQ.Messages;
//using NetMQ; ? för att definiera meddelande -> fungerar att bara skicka sträng som "value"?

namespace KnightBus.ZeroMQ
{
    internal class ZeroMQMessage<T> where T : class, IZeroMQMessage
    {
        public T Message { get; }
        public string ZeroMQValue { get; }
        public ZeroMQMessage(string zeroMQValue, T message)
        {
            ZeroMQValue = zeroMQValue;
            Message = message;
        }
    }

}
