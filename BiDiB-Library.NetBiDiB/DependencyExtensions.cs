using Microsoft.Extensions.DependencyInjection;
using org.bidib.Net.Core.Controllers.Interfaces;
using org.bidib.Net.Core.Message;
using org.bidib.Net.NetBiDiB.Controllers;
using org.bidib.Net.NetBiDiB.Message;
using org.bidib.Net.NetBiDiB.Services;

[assembly: System.Runtime.InteropServices.ComVisible(false)]

namespace org.bidib.Net.NetBiDiB;

public static class DependencyExtensions
{
    public static void AddNetBiDiB(this IServiceCollection services)
    {
        services.AddSingleton<INetBiDiBParticipantsService, NetBiDiBParticipantsService>();

        services.AddSingleton<IConnectionControllerFactory, ConnectionControllerFactory>();

        services.AddTransient<INetBiDiBMessageProcessor, NetBiDiBMessageProcessor>();
        services.AddSingleton<IMessageReceiver, NetBiDiBMessageReceiver>();
        services.AddSingleton<IBiDiBMessageService, BiDiBMessageService>();
    }
}