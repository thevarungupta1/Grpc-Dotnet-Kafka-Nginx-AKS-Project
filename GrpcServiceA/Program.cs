using GrpcServiceA.Services;

var builder = WebApplication.CreateBuilder(args);

// Add gRPC services to the container.
builder.Services.AddGrpc();

// Register KafkaProducer as a singleton.
builder.Services.AddSingleton<KafkaProducer>();

var app = builder.Build();

app.MapGrpcService<GreeterService>();

// Health check endpoint.
app.MapGet("/", () => "gRPC Service A is running.");

app.Run();
