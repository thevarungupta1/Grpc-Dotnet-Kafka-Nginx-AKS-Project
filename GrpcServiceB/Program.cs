using GrpcServiceB.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

// Register KafkaConsumer as a hosted background service.
builder.Services.AddHostedService<KafkaConsumer>();

var app = builder.Build();

app.MapGrpcService<GreeterService>();

app.MapGet("/", () => "gRPC Service B is running.");

app.Run();
