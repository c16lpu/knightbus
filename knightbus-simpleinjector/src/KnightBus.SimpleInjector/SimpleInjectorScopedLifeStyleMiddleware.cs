using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using SimpleInjector;

namespace KnightBus.SimpleInjector
{
    public class SimpleInjectorScopedLifeStyleMiddleware : IMessageScopeProviderMiddleware
    {
        private readonly Container _container;

        public SimpleInjectorScopedLifeStyleMiddleware(Container container)
        {
            _container = container;
        }

        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            using (var scope = messageStateHandler.MessageScope.GetScope())
            {
                messageStateHandler.MessageScope = scope;
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}