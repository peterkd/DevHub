using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EmailComposer.Backend.Services;

public sealed class SqlRecipientService
{
    private const string ConnectionStringName = "AzureSqlDatabase";

    private const string OrganizationRoleQuery = """
        SELECT DISTINCT [OrganizationRole]
        FROM [dbo].[WorkerRole]
        ORDER BY [OrganizationRole]
        """;

    private const string RecipientEmailQuery = """
        WITH WorkerRoles AS (
            SELECT
                Worker.[Id] AS WorkerId,
                Worker.[Role] AS RoleName
            FROM [dbo].[Worker] Worker
            UNION
            SELECT
                Worker.[Id] AS WorkerId,
                WR.[Name] AS RoleName
            FROM [dbo].[Worker] Worker
            LEFT JOIN [dbo].[WorkerRoleAssignment] WRA ON WRA.[WorkerId] = Worker.[Id]
            INNER JOIN [dbo].[WorkerRole] WR ON WR.[Id] = WRA.[RoleId]
        ),
        WorkerRolesAgg AS (
            SELECT
                WorkerId,
                STRING_AGG(RoleName, ', ') AS Roles
            FROM WorkerRoles
            GROUP BY WorkerId
        ),
        CompanyActiveWfmAdmins AS (
            SELECT
                Worker.[EmailAddress]
            FROM [dbo].[Worker] Worker
            INNER JOIN WorkerRoles ON WorkerRoles.WorkerId = Worker.[Id]
            WHERE WorkerRoles.RoleName = 'WFM Administrator' AND Worker.[Status] = 'A'
        )
        SELECT DISTINCT
            'peter.kiedrowski@hatch.com' AS EmailAddress
        FROM CompanyActiveWfmAdmins
        --WHERE EmailAddress IS NOT NULL AND LTRIM(RTRIM(EmailAddress)) <> ''
        --WHERE EmailAddress = 'peter.kiedrowski@hatch.com'
        ORDER BY EmailAddress;
        """;

    private readonly IConfiguration _configuration;

    public SqlRecipientService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<string>> GetRecipientEmailAddressesAsync(CancellationToken cancellationToken)
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

    public async Task<IReadOnlyList<string>> GetOrganizationRolesAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Azure SQL connection string is missing. Configure ConnectionStrings:{ConnectionStringName}.");
        }

        var roles = new List<string>();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(OrganizationRoleQuery, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    var role = reader.GetString(0).Trim();
                    if (role.Length > 0)
                    {
                        roles.Add(role);
                    }
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
}
