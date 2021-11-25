using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using NetPro;
using NetPro.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcServer
{
    [GrpcService]
    public class GreeterService : Greeter.GreeterBase
    {
        private readonly ILogger<GreeterService> _logger;
        public GreeterService(ILogger<GreeterService> logger)
        {
            _logger = logger;
        }

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            //using var channel = GrpcChannel.ForAddress("https://localhost:5001");
            //var client = new Greeter.GreeterClient(channel);
            //var reply = client.SayHelloAsync(
            //                  new HelloRequest { Name = "GreeterClient" });
            ////context.GetHttpContext().User
            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }

        public override async Task SubSayHello(IAsyncStreamReader<BathTheCatReq> requestStream, IServerStreamWriter<BathTheCatResp> responseStream, ServerCallContext context)
        {
            var bathQueue = new Queue<int>();
            while (await requestStream.MoveNext())
            {
                //��Ҫϴ���è�������
                bathQueue.Enqueue(requestStream.Current.Id);

                _logger.LogInformation($"Cat {requestStream.Current.Id} Enqueue.");
            }

            //�������п�ʼϴ��
            while (bathQueue.TryDequeue(out var catId))
            {
                await responseStream.WriteAsync(new BathTheCatResp() { Message = $"��ʺ�ĳɹ���һֻ{catId}ϴ���裡" });

                await Task.Delay(500);//�˴���Ҫ��Ϊ�˷���ͻ����ܿ��������õ�Ч��
            }
        }
    }
}
