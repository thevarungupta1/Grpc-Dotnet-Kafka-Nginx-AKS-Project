using Confluent.Kafka;

namespace GrpcServiceA.Services
{
    public class KafkaProducer
    {
        private readonly IProducer<Null, string> _producer;
        private readonly string _topic = "greetings";

        public KafkaProducer()
        {
            // Use the Kafka service name from AKS.
            var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:9092";
            var config = new ProducerConfig { BootstrapServers = bootstrapServers };
            _producer = new ProducerBuilder<Null, string>(config).Build();
        }

        public async Task PublishMessageAsync(string name)
        {
            var message = $"Hello {name} from Service A at {DateTime.UtcNow}";
            await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = message });
        }
    }
}
