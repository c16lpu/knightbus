using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;
using KnightBus.ZeroMQ.Messages;
using NetMQ;
using NetMQ.Sockets;
namespace KnightBus.ZeroMQ
{
    public interface IZeroMQBus
    {
        Task SendAsync<T>(T message) where T : IZeroMQCommand;
        Task SendAsync<T>(IEnumerable<T> messages) where T : IZeroMQCommand;
        Task PublishAsync<T>(T message) where T : IZeroMQEvent;
        Task PublishAsync<T>(IEnumerable<T> messages) where T : IZeroMQEvent;
    }

    public class ZeroMQBus : IZeroMQBus
    {
        private readonly PublisherSocket _socket;
        private readonly IZeroMQConfiguration _configuration;
        private IMessageAttachmentProvider _attachmentProvider;

        public ZeroMQBus(string connectionString) : this(new ZeroMQConfiguration(connectionString))
        { }
        public ZeroMQBus(IZeroMQConfiguration configuration)
        {
            _socket = new PublisherSocket();
            _socket.Bind(configuration.ConnectionString);
            _configuration = configuration;
        }

        public void EnableAttachments(IMessageAttachmentProvider attachmentProvider)
        {
            _attachmentProvider = attachmentProvider;
        }

        public Task SendAsync<T>(T message) where T : IZeroMQCommand
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            return SendAsync(message, queueName);
        }
        public Task SendAsync<T>(IEnumerable<T> messages) where T : IZeroMQCommand
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            return SendAsync<T>(messages.ToList(), queueName);
        }

        private Task SendAsync<T>(IList<T> messages, string queueName) where T : IZeroMQMessage
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var listItems = messages.Select(m => new ZeroMQListItem<T>(Guid.NewGuid().ToString("N"), m));
            var serialized = listItems.Select(m => (ZeroMQValue)_configuration.MessageSerializer.Serialize(m)).ToArray();
            return Task.WhenAll(
                UploadAttachments(listItems, queueName, db),
                db.ListLeftPushAsync(queueName, serialized),
                db.PublishAsync(queueName, 0, CommandFlags.FireAndForget)
            );
        }

        private Task SendAsync<T>(T message, string queueName) where T : IZeroMQMessage
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var zeroMQListItem = new ZeroMQListItem<T>(Guid.NewGuid().ToString("N"), message);
            return Task.WhenAll(
                UploadAttachment(zeroMQListItem, queueName, db),
                db.ListLeftPushAsync(queueName, _configuration.MessageSerializer.Serialize(zeroMQListItem)),
                db.PublishAsync(queueName, 0, CommandFlags.FireAndForget)
            );
        }

        public async Task PublishAsync<T>(T message) where T : IRedisEvent
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var queueName = AutoMessageMapper.GetQueueName<T>();
            var subscriptions = await db.SetMembersAsync(RedisQueueConventions.GetSubscriptionKey(queueName)).ConfigureAwait(false);
            if (subscriptions == null) return;
            await Task.WhenAll(subscriptions.Select(sub => SendAsync(message, RedisQueueConventions.GetSubscriptionQueueName(queueName, sub)))).ConfigureAwait(false);
        }

        public async Task PublishAsync<T>(IEnumerable<T> messages) where T : IRedisEvent
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var queueName = AutoMessageMapper.GetQueueName<T>();
            var subscriptions = await db.SetMembersAsync(RedisQueueConventions.GetSubscriptionKey(queueName)).ConfigureAwait(false);
            if (subscriptions == null) return;
            var messageList = messages.ToList();
            await Task.WhenAll(subscriptions.Select(sub => SendAsync<T>(messageList, RedisQueueConventions.GetSubscriptionQueueName(queueName, sub)))).ConfigureAwait(false);
        }


        private async Task UploadAttachment<T>(RedisListItem<T> message, string queueName, IDatabase db) where T : IRedisMessage
        {
            if (typeof(ICommandWithAttachment).IsAssignableFrom(typeof(T)))
            {
                if (_attachmentProvider == null) throw new AttachmentProviderMissingException();
                var attachmentMessage = (ICommandWithAttachment)message;
                if (attachmentMessage.Attachment != null)
                {
                    var attachmentId = Guid.NewGuid().ToString("N");
                    await db.HashSetAsync(RedisQueueConventions.GetMessageHashKey(queueName, message.Id), AttachmentUtility.AttachmentKey, attachmentId).ConfigureAwait(false);
                    await _attachmentProvider.UploadAttachmentAsync(queueName, attachmentId, attachmentMessage.Attachment).ConfigureAwait(false);
                }
            }
        }

        private Task UploadAttachments<T>(IEnumerable<RedisListItem<T>> messages, string queueName, IDatabase db) where T : IRedisMessage
        {
            if (typeof(ICommandWithAttachment).IsAssignableFrom(typeof(T)))
            {
                return Task.WhenAll(messages.Select(m => UploadAttachment(m, queueName, db)));
            }

            return Task.CompletedTask;
        }
    }
}