using Microsoft.Extensions.DependencyInjection;
using org.bidib.netbidibc.netbidib.Controllers;
using org.bidib.netbidibc.netbidib.Message;
using org.bidib.netbidibc.netbidib.Services;
using org.bidib.netbidibc.core.Controllers.Interfaces;
using org.bidib.netbidibc.core.Message;

namespace org.bidib.netbidibc.core
{
    public static class DependencyExtensions
    {
        public static void AddNetBiDiB(this IServiceCollection services)
        {
            services.AddSingleton<INetBiDiBParticipantsService, NetBiDiBParticipantsService>();

            services.AddSingleton<IConnectionControllerFactory, ConnectionControllerFactory>();
            //services.AddSingleton<IConnectionControllerFactory, ServerControllerFactory>();

            services.AddTransient<INetBiDiBMessageProcessor, NetBiDiBMessageProcessor>();
            services.AddSingleton<IMessageReceiver, NetBiDiBMessageReceiver>();
            services.AddSingleton<IBiDiBMessageService, BiDiBMessageService>();
            services.AddSingleton<INetBiDiBServerController, NetBiDiBServerController>();
        }
    }
}
