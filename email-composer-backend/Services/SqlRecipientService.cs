using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EmailComposer.Backend.Services;

public sealed class SqlRecipientService
{
    private const string ConnectionStringName = "AzureSqlDatabase";

    private const string OrganizationRolesQuery = """
        SELECT DISTINCT
            LTRIM(RTRIM([OrganizationRole])) AS OrganizationRole
        FROM [dbo].[WorkerRole]
        WHERE [OrganizationRole] IS NOT NULL
          AND LTRIM(RTRIM([OrganizationRole])) <> ''
        ORDER BY OrganizationRole;
        """;

    private const string RecipientEmailQuery = """
        WITH WorkerOrganizationRoles AS (
            SELECT
                Worker.[Id] AS WorkerId,
                Worker.[EmailAddress],
                Worker.[Status],
                WR.[OrganizationRole]
            FROM [dbo].[Worker] Worker
            INNER JOIN [dbo].[WorkerRole] WR ON WR.[Name] = Worker.[Role]
            UNION
            SELECT
                Worker.[Id] AS WorkerId,
                Worker.[EmailAddress],
                Worker.[Status],
                WR.[OrganizationRole]
            FROM [dbo].[Worker] Worker
            INNER JOIN [dbo].[WorkerRoleAssignment] WRA ON WRA.[WorkerId] = Worker.[Id]
            INNER JOIN [dbo].[WorkerRole] WR ON WR.[Id] = WRA.[RoleId]
        )
        SELECT DISTINCT
            LTRIM(RTRIM([EmailAddress])) AS EmailAddress
        FROM WorkerOrganizationRoles
        WHERE [Status] = 'A'
          AND [OrganizationRole] = @OrganizationRole
          AND [EmailAddress] IS NOT NULL
          AND LTRIM(RTRIM([EmailAddress])) <> ''
        ORDER BY EmailAddress;
        """;

    private readonly IConfiguration _configuration;

    public SqlRecipientService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<string>> GetOrganizationRolesAsync(CancellationToken cancellationToken)
    {
        return await ExecuteStringListQueryAsync(
            OrganizationRolesQuery,
            columnOrdinal: 0,
            configureCommand: null,
            errorMessage: "Unable to load organization roles from Azure SQL.",
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetRecipientEmailAddressesAsync(
        string organizationRole,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationRole))
        {
            throw new InvalidOperationException(
                "Organization role is required to load SQL recipients.");
        }

        return await ExecuteStringListQueryAsync(
            RecipientEmailQuery,
            columnOrdinal: 0,
            command => command.Parameters.AddWithValue("@OrganizationRole", organizationRole.Trim()),
            errorMessage: "Unable to load recipient email addresses from Azure SQL.",
            cancellationToken);
    }

    private async Task<IReadOnlyList<string>> ExecuteStringListQueryAsync(
        string query,
        int columnOrdinal,
        Action<SqlCommand>? configureCommand,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Azure SQL connection string is missing. Configure ConnectionStrings:{ConnectionStringName}.");
        }

        var values = new List<string>();
        var seenValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(query, connection);
            configureCommand?.Invoke(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (reader.HasRows)
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var value = reader.GetString(columnOrdinal).Trim();
                    if (seenValues.Add(value))
                    {
                        values.Add(value);
                    }
                }
            }
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                errorMessage,
                ex);
        }

        return values;
    }
}
