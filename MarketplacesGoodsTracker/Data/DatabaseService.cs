using Microsoft.Data.Sqlite;
using Dapper;
using MarketplacesGoodsTracker.Models;
using Microsoft.Extensions.Configuration;

namespace MarketplacesGoodsTracker.Data;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=bot.db";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Users (
                ChatId INTEGER PRIMARY KEY,
                IsActive BOOLEAN NOT NULL,
                RegistrationDate DATETIME NOT NULL
            );

            CREATE TABLE IF NOT EXISTS TrackedProducts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL,
                ProductUrl TEXT NOT NULL,
                SKU TEXT NOT NULL,
                Name TEXT NOT NULL,
                TargetPrice DECIMAL NOT NULL,
                LastKnownPrice DECIMAL NOT NULL,
                FOREIGN KEY(ChatId) REFERENCES Users(ChatId)
            );
        ");
    }

    public async Task<User?> GetUserAsync(long chatId)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<User>("SELECT * FROM Users WHERE ChatId = @chatId", new { chatId });
    }

    public async Task AddUserAsync(User user)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("INSERT OR IGNORE INTO Users (ChatId, IsActive, RegistrationDate) VALUES (@ChatId, @IsActive, @RegistrationDate)", user);
    }

    public async Task AddTrackedProductAsync(TrackedProduct product)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(@"
            INSERT INTO TrackedProducts (ChatId, ProductUrl, SKU, Name, TargetPrice, LastKnownPrice)
            VALUES (@ChatId, @ProductUrl, @SKU, @Name, @TargetPrice, @LastKnownPrice)", product);
    }

    public async Task<IEnumerable<TrackedProduct>> GetTrackedProductsAsync(long chatId)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryAsync<TrackedProduct>("SELECT * FROM TrackedProducts WHERE ChatId = @chatId", new { chatId });
    }

    public async Task<IEnumerable<TrackedProduct>> GetAllTrackedProductsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryAsync<TrackedProduct>("SELECT * FROM TrackedProducts");
    }

    public async Task UpdateProductPriceAsync(int id, decimal newPrice)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("UPDATE TrackedProducts SET LastKnownPrice = @newPrice WHERE Id = @id", new { id, newPrice });
    }

    public async Task RemoveTrackedProductAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM TrackedProducts WHERE Id = @id", new { id });
    }
}
