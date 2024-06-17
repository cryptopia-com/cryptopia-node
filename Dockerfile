# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory in the container
WORKDIR /source

# Copy the project file and restore dependencies
COPY Cryptopia.Node/*.csproj .
RUN dotnet restore

# Copy the rest of the source code and build the application
COPY Cryptopia.Node/. .
RUN dotnet publish -c Release -o /app

# Use the official .NET runtime image for the final stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

# Set the working directory in the container
WORKDIR /app

# Install networking tools
RUN apt-get update && apt-get install -y iputils-ping

# Copy the built application from the build stage
COPY --from=build /app .

# Expose ports
EXPOSE 8000 3478 5349 3033 59000-65000

# Define the entry point for the container
ENTRYPOINT ["dotnet", "Cryptopia.Node.dll"]
