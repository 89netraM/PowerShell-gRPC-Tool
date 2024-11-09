using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace PowerShellGrpcTool.ExampleService.Services;

public class GreeterService(ILogger<GreeterService> logger) : Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        logger.LogInformation("Responding hello to a request from {Name}", request.Name);
        return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
    }
}
