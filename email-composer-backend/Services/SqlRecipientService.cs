using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EmailComposer.Backend.Services;

public sealed class SqlRecipientService
{
    private const string ConnectionStringName = "AzureSqlDatabase";

    private const string OrganizationRoleQuery = """
        SELECT DISTINCT [OrganizationRole]
        FROM [dbo].[WorkerRole]
        ORDER BY [OrganizationRole];
        """;

    private const string RecipientEmailQuery = """
        SELECT DISTINCT
            Worker.[EmailAddress]
        FROM [dbo].[Worker] Worker
        INNER JOIN [dbo].[WorkerRoleAssignment] WRA ON WRA.[WorkerId] = Worker.[Id]
        INNER JOIN [dbo].[WorkerRole] WR ON WR.[Id] = WRA.[RoleId]
        WHERE Worker.[Status] = 'A'
          AND WR.[OrganizationRole] = @OrganizationRole
          AND Worker.[EmailAddress] IS NOT NULL
          AND LTRIM(RTRIM(Worker.[EmailAddress])) <> ''
        ORDER BY Worker.[EmailAddress];
        """;

    private readonly IConfiguration _configuration;

    public SqlRecipientService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<string>> GetOrganizationRolesAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Azure SQL connection string is missing. Configure ConnectionStrings:{ConnectionStringName}.");
        }

        var roles = new List<string>();
        var seenRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(OrganizationRoleQuery, connection);
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
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                "Unable to load organization roles from Azure SQL.",
                ex);
        }

        return roles;
    }

    public async Task<IReadOnlyList<string>> GetRecipientEmailAddressesAsync(
        string organizationRole,
        CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Azure SQL connection string is missing. Configure ConnectionStrings:{ConnectionStringName}.");
        }

        var recipients = new List<string>();
        var seenRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(RecipientEmailQuery, connection);
            command.Parameters.AddWithValue("@OrganizationRole", organizationRole);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var emailAddress = reader.GetString(0).Trim();
                if (seenRecipients.Add(emailAddress))
                {
                    recipients.Add(emailAddress);
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
}
