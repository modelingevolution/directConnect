using System.Runtime.CompilerServices;


using Grpc.Net.Client;

namespace ModelingEvolution.DirectConnect;

class GrpcChannelScopeFactory
{
    internal Func<GrpcChannel> Factory;
}