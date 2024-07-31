# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy the project file and restore dependencies
COPY Cryptopia.Node/*.csproj .
RUN dotnet restore

# Copy the rest of the source code and build the application
COPY Cryptopia.Node/. .
RUN dotnet publish -c Release -o /app

# Use the official .NET runtime image for the final stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Install networking tools
RUN apt-get update && apt-get install -y iputils-ping && rm -rf /var/lib/apt/lists/*

# Copy the built application from the build stage
COPY --from=build /app .

# Define environment variables
ARG APPLICATION_INSIGHTS_CONNECTION_STRING
ARG PUBLIC_IP
ENV APPLICATION_INSIGHTS_CONNECTION_STRING=TestTestTest
ENV PUBLIC_IP=$PUBLIC_IP

# Define the entry point for the container
ENTRYPOINT ["dotnet", "Cryptopia.Node.dll"]
