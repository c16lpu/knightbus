using KnightBus.Messages;

namespace KnightBus.ZeroMQ.Messages
{
    public interface IZeroMQMessage : IMessage
    {

    }

    public interface IZeroMQCommand : ICommand, IZeroMQMessage
    {

    }

    public interface IZeroMQEvent : IEvent, IZeroMQMessage
    {

    }
}
