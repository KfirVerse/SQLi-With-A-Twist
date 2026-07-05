using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace SqliRceLab.Pages;

// ============================================================================
//  /search?price=X  —  flat CSV data endpoint (text/plain).
//  Prefix search by price. Returns matching PRICES only, comma-separated:
//      /search?price=10  ->  "10.50,100.00,101.00,105.00,1000.00,1050.00"
//  No rows -> empty body.
//  Vulnerable parameter: `price` (concatenated into the LIKE). The selected
//  column is numeric (price), so:
//    * number extraction  (prices, IS_SRVROLEMEMBER, COUNT ...) returns as CSV
//    * string extraction  (@@version, DB_NAME, table names) needs CONVERT and
//      leaks in the 500 error body.
// ============================================================================
public class SearchModel : PageModel
{
    private readonly IConfiguration _configuration;

    public SearchModel(IConfiguration configuration) => _configuration = configuration;

    public IActionResult OnGet(string? price)
    {
        if (price is null)
        {
            return Content(string.Empty, "text/plain; charset=utf-8");
        }

        var connectionString = _configuration.GetConnectionString("StoreDb")
            ?? throw new InvalidOperationException("Connection string 'StoreDb' is not configured.");

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        // ====================================================================
        //  !!  VULNERABLE ON PURPOSE  -  DO NOT COPY INTO REAL CODE  !!
        //  `price` concatenated straight into the LIKE; no parameters.
        // ====================================================================
        var sql = $"SELECT TOP 100 price FROM dbo.products " +
                  $"WHERE active = 1 AND CAST(price AS varchar(30)) LIKE '{price}%'";

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        var values = new List<string>();

        // BLIND: return only the FIRST result set. Stacked statements still run
        // (INSERT..EXEC xp_cmdshell, or a direct EXEC xp_cmdshell), but result
        // sets they produce are drained and DISCARDED — command output never
        // comes back in the response. Exfiltration must use the error channel.
        while (reader.Read())
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                values.Add(reader.IsDBNull(i)
                    ? string.Empty
                    : Convert.ToString(reader.GetValue(i), CultureInfo.Inva