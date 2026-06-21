using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using MongoDB.Driver;
using UrlShortenerApi;

var builder = WebApplication.CreateBuilder(args);

// 1. Initialize Distributed Infrastructure Connections
var redisConnection = Environment.GetEnvironmentVariable("RedisConnection") ?? "localhost:6379";
var mongoConnection = Environment.GetEnvironmentVariable("MongoConnection") ?? "mongodb://db-user:db-pass@localhost:27017/";

// Inject Redis Multiplexer into DI Container
var redis = ConnectionMultiplexer.Connect(redisConnection);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

// Inject MongoDB Driver into DI Container
var mongoClient = new MongoClient(mongoConnection);
var mongoDatabase = mongoClient.GetDatabase("UrlShortenerDb");
builder.Services.AddSingleton(mongoDatabase.GetCollection<UrlMapping>("Mappings"));

// Register our Hashing Engine Service
builder.Services.AddSingleton<UrlShorteningService>();

var app = builder.Build();

// =========================================================================
// ENDPOINT 1: THE WRITE PATH (Shorten a new URL)
// =========================================================================
app.MapPost("/api/v1/shorten", async (
    [FromBody] ShortenRequest request, 
    IMongoCollection<UrlMapping> collection,
    UrlShorteningService shortener) =>
{
    if (string.IsNullOrEmpty(request.LongUrl) || !Uri.IsWellFormedUriString(request.LongUrl, UriKind.Absolute))
    {
        return Results.BadRequest("A valid, absolute long URL must be provided.");
    }

    // Generate unique 7-character code based on the URL string
    string shortCode = shortener.Generate7CharacterCode(request.LongUrl);

    // Save permanently to the Source of Truth (Database)
    var mapping = new UrlMapping { Id = shortCode, ShortCode = shortCode, LongUrl = request.LongUrl, CreatedAt = DateTime.UtcNow };
    
    // Upsert mechanism to prevent duplicate crash errors
    await collection.ReplaceOneAsync(x => x.Id == shortCode, mapping, new ReplaceOptions { IsUpsert = true });

    return Results.Ok(new { shortUrl = $"http://localhost:5000/{shortCode}", code = shortCode });
});

// =========================================================================
// ENDPOINT 2: THE CACHE-ASIDE READ PATH (Redirect Short Code to Long URL)
// =========================================================================
app.MapGet("/{shortCode}", async (
    string shortCode, 
    IConnectionMultiplexer redisMux, 
    IMongoCollection<UrlMapping> collection) =>
{
    var cache = redisMux.GetDatabase();

    // STEP A: Query the Read-Accelerator (Redis Cache)
    string? cachedUrl = await cache.StringGetAsync(shortCode);
    if (!string.IsNullOrEmpty(cachedUrl))
    {
        // Cache Hit: Instantly perform HTTP 302 Redirect (Sub-5ms response)
        return Results.Redirect(cachedUrl, permanent: false);
    }

    // STEP B: Cache Miss - Fallback to the Database
    var filter = Builders<UrlMapping>.Filter.Eq(x => x.ShortCode, shortCode);
    var mapping = await collection.Find(filter).FirstOrDefaultAsync();

    if (mapping == null)
    {
        return Results.NotFound("The requested short link does not exist or has expired.");
    }

    // STEP C: Async Write-Back to Redis Cache with a 24-hour Time-To-Live (TTL)
    // This shields the database from all subsequent hits for this link
    await cache.StringSetAsync(shortCode, mapping.LongUrl, TimeSpan.FromHours(24));

    // Perform HTTP 302 Redirect
    return Results.Redirect(mapping.LongUrl, permanent: false);
});

app.Run();

// =========================================================================
// DATA STRUCTURES & SCHEMAS
// =========================================================================
public record ShortenRequest(string LongUrl);

public class UrlMapping
{
    public string Id { get; set; } = string.Empty; // MongoDB _id identifier string
    public string ShortCode { get; set; } = string.Empty;
    public string LongUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// Placeholder service for Hashing Engine logic
//public class UrlShorteningService
//{
//    public string Generate7CharacterCode(string longUrl)
//    {
//        // Real Base62/Murmur Hash implementation details go here
//        // Providing a functional stable 7-char hash for testing:
//        return Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(longUrl)))
//            .Replace("+", "").Replace("/", "").Substring(0, 7);
//    }
//}
