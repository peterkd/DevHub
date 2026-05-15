using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EmailComposer.Backend.Services;

public sealed class SqlRecipientService
{
    private const string ConnectionStringName = "AzureSqlDatabase";

    private const string OrganizationRoleQuery = """
        SELECT DISTINCT [OrganizationRole]
        FROM [dbo].[WorkerRole]
        WHERE [OrganizationRole] IS NOT NULL
          AND LTRIM(RTRIM([OrganizationRole])) <> ''
        ORDER BY [OrganizationRole];
        """;

    private const string RecipientEmailQuery = """
        WITH WorkerOrganizationRoles AS (
            SELECT
                Worker.[Id] AS WorkerId,
                Worker.[EmailAddress],
                Worker.[Status],
                WorkerRole.[OrganizationRole]
            FROM [dbo].[Worker] Worker
            INNER JOIN [dbo].[WorkerRole] WorkerRole ON WorkerRole.[Name] = Worker.[Role]
            UNION
            SELECT
                Worker.[Id] AS WorkerId,
                Worker.[EmailAddress],
                Worker.[Status],
                WR.[OrganizationRole]
            FROM [dbo].[Worker] Worker
            LEFT JOIN [dbo].[WorkerRoleAssignment] WRA ON WRA.[WorkerId] = Worker.[Id]
            INNER JOIN [dbo].[WorkerRole] WR ON WR.[Id] = WRA.[RoleId]
        )
        SELECT DISTINCT
            [EmailAddress]
        FROM WorkerOrganizationRoles
        WHERE [Status] = 'A'
          AND [OrganizationRole] = @OrganizationRole
          AND [EmailAddress] IS NOT NULL
          AND LTRIM(RTRIM([EmailAddress])) <> ''
        ORDER BY [EmailAddress];
        """;

    private readonly IConfiguration _configuration;

    public SqlRecipientService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<string>> GetOrganizationRolesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand(OrganizationRoleQuery, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var organizationRoles = new List<string>();
            var seenOrganizationRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (reader.HasRows)
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var organizationRole = reader.GetString(0).Trim();
                    if (seenOrganizationRoles.Add(organizationRole))
                    {
                        organizationRoles.Add(organizationRole);
                    }
                }
            }

            return organizationRoles;
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                "Unable to load organization roles from Azure SQL.",
                ex);
        }
    }

    public async Task<IReadOnlyList<string>> GetRecipientEmailAddressesAsync(
        string? organizationRole,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationRole))
        {
            throw new InvalidOperationException(
                "An organization role must be selected before loading SQL recipients.");
        }

        var recipients = new List<string>();
        var seenRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand(RecipientEmailQuery, connection);
            command.Parameters.Add(
                new SqlParameter("@OrganizationRole", SqlDbType.NVarChar)
                {
                    Value = organizationRole.Trim()
                });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (reader.HasRows)
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var emailAddress = reader.GetString(0).Trim();
                    if (seenRecipients.Add(emailAddress))
                    {
                        recipients.Add(emailAddress);
                    }
                }
            }
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                "Unable to load recipient email addresses from Azure SQL.",
                ex);
        }

        return recipients;
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Azure SQL connection string is missing. Configure ConnectionStrings:{ConnectionStringName}.");
        }

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
