using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace SqliRceLab.Pages;

public class InventoryModel : PageModel
{
    private readonly IConfiguration _configuration;

    public InventoryModel(IConfiguration configuration) => _configuration = configuration;

    // Full product list (id, price, name) as JSON, embedded in the page so the
    // UI can render the 3-column table. Loaded with a SAFE query — NOT the
    // vulnerable path (only /search is vulnerable).
    public string ProductsJson { get; private set; } = "[]";

    public void OnGet()
    {
        var connectionString = _configuration.GetConnectionString("StoreDb");
        if (connectionString is null)
        {
            return;
        }

        var list = new List<object>();
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand(
            "SELECT id, price, name FROM dbo.products WHERE active = 1 ORDER BY price", connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new
            {
                id = reader.GetInt32(0),
                price = reader.GetInt32(1).ToString(CultureInfo.InvariantCulture),
                name = reader.GetString(2)
            });
        }
        ProductsJson = JsonSerializer.Serialize(list);
    }
}
