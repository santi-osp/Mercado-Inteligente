using System.Data;
using System.Text.RegularExpressions;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure;

public static class ProductSearchService
{
    public static Task<IReadOnlyList<ExternalProduct>> SearchAsync(
        MarketDb db,
        string query,
        CancellationToken ct) => SearchAsync(db, query, null, null, ct);

    public static async Task<IReadOnlyList<ExternalProduct>> SearchAsync(
        MarketDb db,
        string query,
        int? limit = null,
        int? offset = null,
        CancellationToken ct = default)
    {
        var terms = Regex.Matches(query, @"[\p{L}\p{N}]+")
            .Select(match => match.Value).Take(8).ToArray();
        if (terms.Length == 0) return [];
        var expression = string.Join(" AND ", terms.Select(term => $"\"{term}\"*"));
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed) await db.Database.OpenConnectionAsync(ct);
        var ids = new List<Guid>();
        try
        {
            await using var command = connection.CreateCommand();
            var sql = "SELECT productId FROM ProductSearch WHERE ProductSearch MATCH @query ORDER BY bm25(ProductSearch, 0, 8, 3, 1)";
            if (limit.HasValue)
            {
                sql += $" LIMIT {limit.Value}";
                if (offset.HasValue)
                {
                    sql += $" OFFSET {offset.Value}";
                }
            }
            command.CommandText = sql;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@query";
            parameter.Value = expression;
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) ids.Add(Guid.Parse(reader.GetString(0)));
        }
        finally
        {
            if (wasClosed) await db.Database.CloseConnectionAsync();
        }

        if (ids.Count == 0) return [];

        var products = await db.ExternalProducts.AsNoTracking().Where(product => ids.Contains(product.Id)).ToListAsync(ct);
        var rank = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        return products.OrderBy(product => string.Equals(product.Name, query, StringComparison.OrdinalIgnoreCase) ? -2 :
                product.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? -1 : rank[product.Id])
            .ToList();
    }

    public static async Task<int> CountAsync(MarketDb db, string query, CancellationToken ct = default)
    {
        var terms = Regex.Matches(query, @"[\p{L}\p{N}]+")
            .Select(match => match.Value).Take(8).ToArray();
        if (terms.Length == 0) return 0;
        var expression = string.Join(" AND ", terms.Select(term => $"\"{term}\"*"));
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ProductSearch WHERE ProductSearch MATCH @query";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@query";
            parameter.Value = expression;
            command.Parameters.Add(parameter);
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result);
        }
        finally
        {
            if (wasClosed) await db.Database.CloseConnectionAsync();
        }
    }
}
