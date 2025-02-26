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
