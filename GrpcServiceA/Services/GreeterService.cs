using Grpc.Core;
using MyGrpcProject.Protos;

namespace GrpcServiceA.Services
{
    public class GreeterService : Greeter.GreeterBase
    {
        private readonly KafkaProducer _producer;
        public GreeterService(KafkaProducer producer)
        {
            _producer = producer;
        }

        public override async Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            // Publish message to Kafka
            await _producer.PublishMessageAsync(request.Name);
            return new HelloReply { Message = $"Hello {request.Name} from Service A" };
        }
    }
}
