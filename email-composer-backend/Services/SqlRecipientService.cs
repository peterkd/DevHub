using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EmailComposer.Backend.Services;

public sealed class SqlRecipientService
{
    private const string ConnectionStringName = "AzureSqlDatabase";

    private const string RecipientEmailQueryBase = """
        WITH WorkerRoles AS (
            SELECT
                Worker.[Id] AS WorkerId,
                Worker.[Role] AS RoleName,
                WR.[OrganizationRole] AS OrganizationRole
            FROM [dbo].[Worker] Worker
            LEFT JOIN [dbo].[WorkerRoleAssignment] WRA ON WRA.[WorkerId] = Worker.[Id]
            LEFT JOIN [dbo].[WorkerRole] WR ON WR.[Id] = WRA.[RoleId]
            UNION
            SELECT
                Worker.[Id] AS WorkerId,
                Worker.[Role] AS RoleName,
                NULL AS OrganizationRole
            FROM [dbo].[Worker] Worker
        ),
        CompanyActiveWorkers AS (
            SELECT
                Worker.[EmailAddress]
            FROM [dbo].[Worker] Worker
            INNER JOIN WorkerRoles ON WorkerRoles.WorkerId = Worker.[Id]
            WHERE Worker.[Status] = 'A'
                AND WorkerRoles.RoleName = 'WFM Administrator'
                AND (@OrganizationRole IS NULL OR WorkerRoles.OrganizationRole = @OrganizationRole)
        )
        SELECT DISTINCT
            'peter.kiedrowski@hatch.com' AS EmailAddress
        FROM CompanyActiveWorkers
        --WHERE EmailAddress IS NOT NULL AND LTRIM(RTRIM(EmailAddress)) <> ''
        --WHERE EmailAddress = 'peter.kiedrowski@hatch.com'
        ORDER BY EmailAddress;
        """;

    private readonly IConfiguration _configuration;

    public SqlRecipientService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<string>> GetRecipientEmailAddressesAsync(
        string? organizationRole,
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

            await using var command = new SqlCommand(RecipientEmailQueryBase, connection);
            command.Parameters.AddWithValue("@OrganizationRole",
                string.IsNullOrWhiteSpace(organizationRole) ? DBNull.Value : organizationRole);
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
}
