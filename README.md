# Dotnet GRPC Services with Kafka & AKS

This sample project demonstrates two gRPC services built in .NET Core that communicate via Kafka. An Nginx API gateway fronts the services, and all components (including Kafka with its required Zookeeper) are deployed on Azure Kubernetes Service (AKS). For external testing, the API gateway is exposed via a NodePort service (which provides an external IP/port). Customize the image names, registry URLs, and any environment-specific settings as needed.

## Project Structure

```
MyGrpcProject/
├── GrpcServiceA/
│   ├── GrpcServiceA.csproj
│   ├── Program.cs
│   ├── Protos/
│   │   └── greet.proto
│   └── Services/
│       ├── GreeterService.cs
│       └── KafkaProducer.cs
├── GrpcServiceB/
│   ├── GrpcServiceB.csproj
│   ├── Program.cs
│   ├── Protos/
│   │   └── greet.proto
│   └── Services/
│       ├── GreeterService.cs
│       └── KafkaConsumer.cs
├── Gateway/
│   ├── Dockerfile
│   └── nginx.conf
└── k8s/
    ├── grpcservicea-deployment.yaml
    ├── grpcserviceb-deployment.yaml
    ├── gateway-deployment.yaml
    ├── gateway-nodeport.yaml
    └── kafka-zookeeper-deployment.yaml
```

## 1. gRPC Service A
### 1.1. Proto Definition
File: **GrpcServiceA/Protos/greet.proto**

```
syntax = "proto3";

option csharp_namespace = "MyGrpcProject.Protos";

package greet;

// The greeting service definition.
service Greeter {
  // Sends a greeting
  rpc SayHello (HelloRequest) returns (HelloReply);
}

// The request message containing the user's name.
message HelloRequest {
  string name = 1;
}

// The response message containing the greetings.
message HelloReply {
  string message = 1;
}

```

### 1.2. Project File
File: **GrpcServiceA/GrpcServiceA.csproj**

```
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Grpc.AspNetCore" Version="2.43.0" />
    <PackageReference Include="Confluent.Kafka" Version="1.9.2" />
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="Protos\greet.proto" GrpcServices="Server" />
  </ItemGroup>
</Project>
```

### 1.3. Program Entry Point
File: **GrpcServiceA/Program.cs**

```
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
```

### 1.4. Greeter Service Implementation
File: **GrpcServiceA/Services/GreeterService.cs**
```
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
```

### 1.5. Kafka Producer Implementation
File: **GrpcServiceA/Services/KafkaProducer.cs**
```
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
```

### 2. gRPC Service B
### 2.1. Proto Definition
File: **GrpcServiceB/Protos/greet.proto**
*(Identical to Service A’s proto file.)*
```
syntax = "proto3";

option csharp_namespace = "MyGrpcProject.Protos";

package greet;

service Greeter {
  rpc SayHello (HelloRequest) returns (HelloReply);
}

message HelloRequest {
  string name = 1;
}

message HelloReply {
  string message = 1;
}
```

### 2.2. Project File
File: **GrpcServiceB/GrpcServiceB.csproj**
```
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Grpc.AspNetCore" Version="2.43.0" />
    <PackageReference Include="Confluent.Kafka" Version="1.9.2" />
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="Protos\greet.proto" GrpcServices="Server" />
  </ItemGroup>
</Project>
```

### 2.3. Program Entry Point
File: **GrpcServiceB/Program.cs**
```
using GrpcServiceB.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

// Register KafkaConsumer as a hosted background service.
builder.Services.AddHostedService<KafkaConsumer>();

var app = builder.Build();

app.MapGrpcService<GreeterService>();

app.MapGet("/", () => "gRPC Service B is running.");

app.Run();
```

### 2.4. Greeter Service Implementation
File: **GrpcServiceB/Services/GreeterService.cs**
```
using Grpc.Core;
using MyGrpcProject.Protos;

namespace GrpcServiceB.Services
{
    public class GreeterService : Greeter.GreeterBase
    {
        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply { Message = $"Hello {request.Name} from Service B" });
        }
    }
}
```

### 2.5. Kafka Consumer Implementation
File: **GrpcServiceB/Services/KafkaConsumer.cs**
```
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;

namespace GrpcServiceB.Services
{
    public class KafkaConsumer : BackgroundService
    {
        private readonly IConsumer<Null, string> _consumer;
        private readonly string _topic = "greetings";

        public KafkaConsumer()
        {
            // Use the Kafka service name from AKS.
            var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:9092";
            var config = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = "serviceB-group",
                AutoOffsetReset = AutoOffsetReset.Earliest
            };
            _consumer = new ConsumerBuilder<Null, string>(config).Build();
            _consumer.Subscribe(_topic);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var cr = _consumer.Consume(stoppingToken);
                        Console.WriteLine($"Service B received Kafka message: {cr.Message.Value}");
                    }
                    catch (ConsumeException e)
                    {
                        Console.WriteLine($"Error consuming message: {e.Error.Reason}");
                    }
                }
            }, stoppingToken);
        }

        public override void Dispose()
        {
            _consumer.Close();
            _consumer.Dispose();
            base.Dispose();
        }
    }
}
```
### 3. Containerization with Docker
### 3.1. Dockerfile for gRPC Services
Create a similar Dockerfile for each service (adjust paths as needed).

For **GrpcServiceA**, for example, create `GrpcServiceA/Dockerfile`:


```
# Use the ASP.NET runtime image as base.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Build stage.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["GrpcServiceA.csproj", "./"]
RUN dotnet restore "GrpcServiceA.csproj"
COPY . .
RUN dotnet publish "GrpcServiceA.csproj" -c Release -o /app/publish

# Final stage.
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GrpcServiceA.dll"]

```
Repeat a similar Dockerfile for GrpcServiceB (adjusting project names).

### 3.2. Nginx API Gateway
### 3.2.1. Nginx Configuration (`Gateway/nginx.conf`)
This config routes incoming HTTP/2 (gRPC) requests to the backend services.

```
worker_processes 1;

events { worker_connections 1024; }

http {
    include       mime.types;
    default_type  application/grpc;

    # Define upstreams for the two gRPC services.
    upstream grpc_service_a {
        server grpcservicea:80;
    }
    upstream grpc_service_b {
        server grpcserviceb:80;
    }

    server {
        listen 80 http2;

        # Route gRPC calls based on service name in the URL.
        location /GrpcServiceA.Greeter/ {
            grpc_pass grpc://grpc_service_a;
        }
        location /GrpcServiceB.Greeter/ {
            grpc_pass grpc://grpc_service_b;
        }
    }
}
```

### 3.2. Dockerfile for Gateway
A simple Dockerfile to build the Nginx container:
File: **Gateway/Dockerfile**
```
FROM nginx:alpine
COPY nginx.conf /etc/nginx/nginx.conf
```


### 4. Kubernetes Manifests for AKS
All manifests below should be placed in the k8s/ folder.

### 4.1. GrpcServiceA Deployment and Service
File: **k8s/grpcservicea-deployment.yaml**
```
apiVersion: apps/v1
kind: Deployment
metadata:
  name: grpcservicea
spec:
  replicas: 2
  selector:
    matchLabels:
      app: grpcservicea
  template:
    metadata:
      labels:
        app: grpcservicea
    spec:
      containers:
      - name: grpcservicea
        image: varungupta2809/grpcservicea:latest
        ports:
        - containerPort: 80
        env:
        - name: KAFKA_BOOTSTRAP_SERVERS
          value: "kafka:9092"
---
apiVersion: v1
kind: Service
metadata:
  name: grpcservicea
spec:
  selector:
    app: grpcservicea
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
```

### 4.2. gRPC Service B Deployment and Service
File: **k8s/grpcserviceb-deployment.yaml**
```
apiVersion: apps/v1
kind: Deployment
metadata:
  name: grpcserviceb
spec:
  replicas: 2
  selector:
    matchLabels:
      app: grpcserviceb
  template:
    metadata:
      labels:
        app: grpcserviceb
    spec:
      containers:
      - name: grpcserviceb
        image: varungupta2809/grpcserviceb:latest
        ports:
        - containerPort: 80
        env:
        - name: KAFKA_BOOTSTRAP_SERVERS
          value: "kafka:9092"
---
apiVersion: v1
kind: Service
metadata:
  name: grpcserviceb
spec:
  selector:
    app: grpcserviceb
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
```

### 4.3. Gateway Deployment
File: **k8s/gateway-deployment.yaml**
```
apiVersion: apps/v1
kind: Deployment
metadata:
  name: gateway
spec:
  replicas: 1
  selector:
    matchLabels:
      app: gateway
  template:
    metadata:
      labels:
        app: gateway
    spec:
      containers:
      - name: gateway
        image: varungupta2809/gateway:latest
        ports:
        - containerPort: 80
```

### 4.4. Gateway NodePort Service (External IP for Testing)
File: **k8s/gateway-nodeport.yaml**
```
apiVersion: v1
kind: Service
metadata:
  name: gateway-nodeport
spec:
  type: NodePort
  selector:
    app: gateway
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
    nodePort: 30080  # Adjust if needed.
```

### 4.5. Kafka & Zookeeper Deployment and Services
File: **k8s/kafka-zookeeper-deployment.yaml**
```
apiVersion: apps/v1
kind: Deployment
metadata:
  name: zookeeper
spec:
  replicas: 1
  selector:
    matchLabels:
      app: zookeeper
  template:
    metadata:
      labels:
        app: zookeeper
    spec:
      containers:
      - name: zookeeper
        image: bitnami/zookeeper:latest
        env:
        - name: ALLOW_ANONYMOUS_LOGIN
          value: "yes"
        ports:
        - containerPort: 2181
---
apiVersion: v1
kind: Service
metadata:
  name: zookeeper
spec:
  ports:
  - port: 2181
    targetPort: 2181
  selector:
    app: zookeeper
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: kafka
spec:
  replicas: 1
  selector:
    matchLabels:
      app: kafka
  template:
    metadata:
      labels:
        app: kafka
    spec:
      containers:
      - name: kafka
        image: bitnami/kafka:latest
        env:
        - name: KAFKA_BROKER_ID
          value: "1"
        - name: KAFKA_ZOOKEEPER_CONNECT
          value: "zookeeper:2181"
        - name: ALLOW_PLAINTEXT_LISTENER
          value: "yes"
        ports:
        - containerPort: 9092
---
apiVersion: v1
kind: Service
metadata:
  name: kafka
spec:
  ports:
  - port: 9092
    targetPort: 9092
  selector:
    app: kafka
```


### 4.4. Ingress Configuration (`k8s/ingress.yaml`)
Assuming you have an Nginx Ingress Controller installed in your AKS cluster:
```
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: grpc-ingress
  annotations:
    kubernetes.io/ingress.class: "nginx"
    nginx.ingress.kubernetes.io/backend-protocol: "GRPC"
spec:
  rules:
  - host: grpc.yourdomain.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: gateway
            port:
              number: 80
```


### 5. Deployment & Testing Steps
Build & Publish the .NET Projects:
In each service folder, run:
```
dotnet restore

dotnet build

dotnet publish -c Release
```
### Dockerize the Applications:
Build Docker images for each service and the gateway (adjust image names/registry as needed):
```
# For Service A
docker build -t varungupta2809/grpcservicea:latest -f GrpcServiceA/Dockerfile GrpcServiceA/

# For Service B
docker build -t varungupta2809/grpcserviceb:latest -f GrpcServiceB/Dockerfile GrpcServiceB/

# For Gateway
docker build -t varungupta2809/gateway:latest Gateway/
```

### Push Images to Your Registry:

```
docker push varungupta2809/grpcservicea:latest
docker push varungupta2809/grpcserviceb:latest
docker push varungupta2809/gateway:latest
```

### Deploy Kafka & Zookeeper on AKS:
```

kubectl apply -f k8s/kafka-zookeeper-deployment.yaml
```
Deploy the gRPC Services and Gateway:

```
kubectl apply -f k8s/grpcservicea-deployment.yaml
kubectl apply -f k8s/grpcserviceb-deployment.yaml
kubectl apply -f k8s/gateway-deployment.yaml
kubectl apply -f k8s/gateway-nodeport.yaml
```

### Testing External Access:
With the NodePort service in place, you can access the API gateway externally using any node’s IP address on port 30080 (for example, `http://<node-ip>:30080`). The Nginx gateway will route incoming gRPC calls to Service A or Service B based on the URL path defined in nginx.conf.

This complete sample project sets up two gRPC services communicating via Kafka, an Nginx API gateway, and a Kafka+Zookeeper deployment—all hosted on AKS with an external endpoint for testing. Customize settings such as Kafka connection strings, image names, and nodePort values as needed for your environment.
