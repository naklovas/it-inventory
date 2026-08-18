using Microsoft.Data.SqlClient;
using PromptBuilder.Models;

namespace PromptBuilder.Services;

public class WizardOptionsRepository
{
    private readonly string _connectionString;

    public WizardOptionsRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PromptBuilderDb")
            ?? throw new InvalidOperationException(
                "appsettings.json: ConnectionStrings:PromptBuilderDb bos birakilamaz.");
    }

    public async Task<List<WizardFieldDefinition>> GetFieldsAsync(CancellationToken ct = default)
    {
        var fields = new List<(int FieldId, WizardFieldDefinition Definition)>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string fieldSql = """
            SELECT FieldId, FieldKey, Label, FieldType, AllowOther, SortOrder,
                   ConditionalOnFieldKey, ConditionalHiddenValue
            FROM dbo.WizardField
            ORDER BY SortOrder;
            """;

        await using (var command = new SqlCommand(fieldSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var definition = new WizardFieldDefinition
                {
                    FieldKey = reader.GetString(1),
                    Label = reader.GetString(2),
                    FieldType = Enum.Parse<WizardFieldType>(reader.GetString(3)),
                    AllowOther = reader.GetBoolean(4),
                    SortOrder = reader.GetInt32(5),
                    ConditionalOnFieldKey = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ConditionalHiddenValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                };
                fields.Add((reader.GetInt32(0), definition));
            }
        }

        const string optionSql = """
            SELECT OptionText
            FROM dbo.WizardOption
            WHERE FieldId = @FieldId
            ORDER BY SortOrder;
            """;

        foreach (var (fieldId, definition) in fields)
        {
            await using var command = new SqlCommand(optionSql, connection);
            command.Parameters.AddWithValue("@FieldId", fieldId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                definition.Options.Add(reader.GetString(0));
            }
        }

        return fields.Select(f => f.Definition).ToList();
    }
}
