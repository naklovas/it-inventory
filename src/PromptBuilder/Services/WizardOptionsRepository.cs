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
            SELECT FieldId, FieldKey, Label, LabelEn, FieldType, AllowOther, SortOrder,
                   ConditionalOnFieldKey, ConditionalHiddenValue
            FROM dbo.WizardField
            ORDER BY SortOrder;
            """;

        await using (var command = new SqlCommand(fieldSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var labelTr = reader.GetString(2);
                var definition = new WizardFieldDefinition
                {
                    FieldKey = reader.GetString(1),
                    LabelTr = labelTr,
                    LabelEn = reader.IsDBNull(3) ? labelTr : reader.GetString(3),
                    FieldType = Enum.Parse<WizardFieldType>(reader.GetString(4)),
                    AllowOther = reader.GetBoolean(5),
                    SortOrder = reader.GetInt32(6),
                    ConditionalOnFieldKey = reader.IsDBNull(7) ? null : reader.GetString(7),
                    ConditionalHiddenValue = reader.IsDBNull(8) ? null : reader.GetString(8),
                };
                fields.Add((reader.GetInt32(0), definition));
            }
        }

        const string optionSql = """
            SELECT OptionText, OptionTextEn
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
                var tr = reader.GetString(0);
                definition.Options.Add(new WizardOptionText
                {
                    Tr = tr,
                    En = reader.IsDBNull(1) ? tr : reader.GetString(1),
                });
            }
        }

        return fields.Select(f => f.Definition).ToList();
    }
}
