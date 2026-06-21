# STAGE 1: Compile and Build the Application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

#Copy the project file and restore dependencies (Optimizes caching)
COPY src/*.csproj ./src/
RUN dotnet restore ./src/*.csproj

# Copy the remaining source files and compile
COPY src/ ./src/
RUN dotnet publish ./src/*.csproj -c Release -o out

# STAGE 2: Package the Optimized Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy the compiled binaries from Stage 1 into our lightweight runtime image
COPY --from=build-env /app/out .

# Expose port 8080 inside the container environment
EXPOSE 8080

# Define the execution entrypoint
ENTRYPOINT ["dotnet", "UrlShortenerApi.dll"]
