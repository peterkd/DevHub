using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EmailComposer.Backend.Services;

public sealed class SqlRecipientService
{
    private const string ConnectionStringName = "AzureSqlDatabase";

    private const string OrganizationRolesQuery = """
        SELECT distinct [OrganizationRole]
        FROM [dbo].[WorkerRole]
        ORDER BY [OrganizationRole];
        """;

    private const string RecipientEmailQuery = """
        SELECT DISTINCT
            Worker.[EmailAddress]
        FROM [dbo].[Worker] Worker
        INNER JOIN [dbo].[WorkerRoleAssignment] WRA ON WRA.[WorkerId] = Worker.[Id]
        INNER JOIN [dbo].[WorkerRole] WR ON WR.[Id] = WRA.[RoleId]
        WHERE WR.[OrganizationRole] = @OrganizationRole
            AND Worker.[Status] = 'A'
            AND Worker.[EmailAddress] IS NOT NULL
            AND LTRIM(RTRIM(Worker.[EmailAddress])) <> ''
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

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand(OrganizationRolesQuery, connection);
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
        if (string.IsNullOrWhiteSpace(organizationRole))
        {
            throw new InvalidOperationException("Select an organization role before including SQL recipients.");
        }

        var recipients = new List<string>();
        var seenRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand(RecipientEmailQuery, connection);
            command.Parameters.Add("@OrganizationRole", SqlDbType.NVarChar, 256).Value =
                organizationRole.Trim();

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

        //if (recipients.Count == 0)
        //{
        //    throw new InvalidOperationException(
        //        "No active WFM Administrator recipient email addresses were found.");
        //}

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
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
