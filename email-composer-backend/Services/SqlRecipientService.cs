using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EmailComposer.Backend.Services;

public sealed class SqlRecipientService
{
    private const string ConnectionStringName = "AzureSqlDatabase";
    private const int MaxOrganizationRoleLength = 255;

    private const string RecipientEmailQuery = """
        WITH WorkerRoles AS (
            SELECT
                Worker.[Id] AS WorkerId,
                Worker.[Role] AS RoleName,
                CAST(NULL AS NVARCHAR(255)) AS OrganizationRole
            FROM [dbo].[Worker] Worker
            UNION
            SELECT
                Worker.[Id] AS WorkerId,
                WR.[Name] AS RoleName,
                WR.[OrganizationRole] AS OrganizationRole
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
            WHERE WorkerRoles.RoleName = 'WFM Administrator'
                AND Worker.[Status] = 'A'
                AND (
                    @OrganizationRole IS NULL
                    OR WorkerRoles.OrganizationRole = @OrganizationRole
                )
        )
        SELECT DISTINCT
            'peter.kiedrowski@hatch.com' AS EmailAddress
        FROM CompanyActiveWfmAdmins
        --WHERE EmailAddress IS NOT NULL AND LTRIM(RTRIM(EmailAddress)) <> ''
        --WHERE EmailAddress = 'peter.kiedrowski@hatch.com'
        ORDER BY EmailAddress;
        """;

    private const string OrganizationRoleQuery = """
        SELECT DISTINCT [OrganizationRole]
        FROM [dbo].[WorkerRole]
        WHERE [OrganizationRole] IS NOT NULL
            AND LTRIM(RTRIM([OrganizationRole])) <> ''
        ORDER BY [OrganizationRole];
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
        var recipients = new List<string>();
        var seenRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedOrganizationRole = NormalizeOrganizationRole(organizationRole);

        try
        {
            await using var connection = new SqlConnection(GetConnectionStringOrThrow());
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(RecipientEmailQuery, connection);
            command.Parameters.Add("@OrganizationRole", System.Data.SqlDbType.NVarChar, MaxOrganizationRoleLength)
                .Value = (object?)normalizedOrganizationRole ?? DBNull.Value;
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
        var organizationRoles = new List<string>();

        try
        {
            await using var connection = new SqlConnection(GetConnectionStringOrThrow());
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(OrganizationRoleQuery, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (reader.HasRows)
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var organizationRole = reader.GetString(0).Trim();
                    if (organizationRole.Length > 0)
                    {
                        organizationRoles.Add(organizationRole);
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

        return organizationRoles;
    }

    private string GetConnectionStringOrThrow()
    {
        var connectionString = _configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Azure SQL connection string is missing. Configure ConnectionStrings:{ConnectionStringName}.");
        }

        return connectionString;
    }

    private static string? NormalizeOrganizationRole(string? organizationRole)
    {
        if (string.IsNullOrWhiteSpace(organizationRole))
        {
            return null;
        }

        var trimmedOrganizationRole = organizationRole.Trim();
        return trimmedOrganizationRole[..Math.Min(trimmedOrganizationRole.Length, MaxOrganizationRoleLength)];
    }
}
