using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EmailComposer.Backend.Services;

public sealed class SqlRecipientService
{
    private const string ConnectionStringName = "AzureSqlDatabase";

    private const string OrganizationRoleQuery = """
        SELECT distinct [OrganizationRole]
        FROM [dbo].[WorkerRole]
        ORDER BY [OrganizationRole]
        """;

    private const string RecipientEmailQuery = """
        WITH WorkerRoles AS (
            SELECT
                Worker.[Id] AS WorkerId,
                WR.[OrganizationRole] AS OrganizationRole
            FROM [dbo].[Worker] Worker
            LEFT JOIN [dbo].[WorkerRoleAssignment] WRA ON WRA.[WorkerId] = Worker.[Id]
            INNER JOIN [dbo].[WorkerRole] WR ON WR.[Id] = WRA.[RoleId]
        ),
        ActiveWorkersForOrganizationRole AS (
            SELECT
                Worker.[EmailAddress]
            FROM [dbo].[Worker] Worker
            INNER JOIN WorkerRoles ON WorkerRoles.WorkerId = Worker.[Id]
            WHERE WorkerRoles.OrganizationRole = @OrganizationRole AND Worker.[Status] = 'A'
        )
        SELECT DISTINCT
            EmailAddress
        FROM ActiveWorkersForOrganizationRole
        WHERE EmailAddress IS NOT NULL AND LTRIM(RTRIM(EmailAddress)) <> ''
        ORDER BY EmailAddress;
        """;

    private readonly IConfiguration _configuration;

    public SqlRecipientService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<string>> GetOrganizationRolesAsync(CancellationToken cancellationToken)
    {
        var roles = new List<string>();
        var seenRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await ExecuteSqlAsync(
            OrganizationRoleQuery,
            async command =>
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (reader.IsDBNull(0))
                    {
                        continue;
                    }

                    var organizationRole = reader.GetString(0).Trim();
                    if (organizationRole.Length > 0 && seenRoles.Add(organizationRole))
                    {
                        roles.Add(organizationRole);
                    }
                }
            },
            "Unable to load organization roles from Azure SQL.",
            cancellationToken);

        return roles;
    }

    public async Task<IReadOnlyList<string>> GetRecipientEmailAddressesAsync(
        string organizationRole,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationRole))
        {
            throw new InvalidOperationException(
                "Select an organization role before including SQL recipients.");
        }

        var recipients = new List<string>();
        var seenRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await ExecuteSqlAsync(
            RecipientEmailQuery,
            async command =>
            {
                command.Parameters.AddWithValue("@OrganizationRole", organizationRole.Trim());

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var emailAddress = reader.GetString(0).Trim();
                    if (seenRecipients.Add(emailAddress))
                    {
                        recipients.Add(emailAddress);
                    }
                }
            },
            "Unable to load recipient email addresses from Azure SQL.",
            cancellationToken);

        return recipients;
    }

    private async Task ExecuteSqlAsync(
        string query,
        Func<SqlCommand, Task> executeCommand,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Azure SQL connection string is missing. Configure ConnectionStrings:{ConnectionStringName}.");
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(query, connection);
            await executeCommand(command);
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                errorMessage,
                ex);
        }
    }
}
