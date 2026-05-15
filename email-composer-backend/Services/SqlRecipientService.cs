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

    private const string UserRoleQuery = """
        SELECT distinct [Name]
        FROM [dbo].[WorkerRole]
        WHERE [OrganizationRole] = @OrganizationRole
        ORDER BY [Name]
        """;

    private const string RecipientEmailQuery = """
        WITH WorkerRoles AS (
            SELECT
                Worker.[Id] AS WorkerId,
                WR.[OrganizationRole] AS OrganizationRole,
                WR.[Name] AS UserRole
            FROM [dbo].[Worker] Worker
            LEFT JOIN [dbo].[WorkerRoleAssignment] WRA ON WRA.[WorkerId] = Worker.[Id]
            INNER JOIN [dbo].[WorkerRole] WR ON WR.[Id] = WRA.[RoleId]
        ),
        ActiveWorkers AS (
            SELECT
                Worker.[EmailAddress]
            FROM [dbo].[Worker] Worker
            INNER JOIN WorkerRoles ON WorkerRoles.WorkerId = Worker.[Id]
            WHERE @OrganizationRole IS NOT NULL AND @OrganizationRole <> ''
                AND @UserRole IS NOT NULL AND @UserRole <> ''
                AND @WorkerFunction IS NOT NULL AND @WorkerFunction <> ''
                AND @Position IS NOT NULL AND @Position <> ''
                AND (WorkerRoles.OrganizationRole = @OrganizationRole OR @OrganizationRole IS NULL OR @OrganizationRole = '')
                AND (WorkerRoles.UserRole = @UserRole OR @UserRole IS NULL OR @UserRole = '')
                AND (Worker.[Function] = @WorkerFunction OR @WorkerFunction IS NULL OR @WorkerFunction = '')
                AND (Worker.[Position] = @Position OR @Position IS NULL OR @Position = '')
                AND Worker.[Status] = 'A'
        )
        SELECT DISTINCT
            EmailAddress
        FROM ActiveWorkers
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

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(
        string organizationRole,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationRole))
        {
            throw new InvalidOperationException(
                "Select an organization role before loading user roles.");
        }

        var roles = new List<string>();
        var seenRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await ExecuteSqlAsync(
            UserRoleQuery,
            async command =>
            {
                command.Parameters.AddWithValue("@OrganizationRole", organizationRole.Trim());

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (reader.IsDBNull(0))
                    {
                        continue;
                    }

                    var userRole = reader.GetString(0).Trim();
                    if (userRole.Length > 0 && seenRoles.Add(userRole))
                    {
                        roles.Add(userRole);
                    }
                }
            },
            "Unable to load user roles from Azure SQL.",
            cancellationToken);

        return roles;
    }

    public async Task<IReadOnlyList<string>> GetRecipientEmailAddressesAsync(
        string organizationRole,
        string userRole,
        string workerFunction,
        string position,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationRole) ||
            string.IsNullOrWhiteSpace(userRole) ||
            string.IsNullOrWhiteSpace(workerFunction) ||
            string.IsNullOrWhiteSpace(position))
        {
            return [];
        }

        var recipients = new List<string>();
        var seenRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await ExecuteSqlAsync(
            RecipientEmailQuery,
            async command =>
            {
                command.Parameters.AddWithValue("@OrganizationRole", organizationRole.Trim());
                command.Parameters.AddWithValue("@UserRole", userRole.Trim());
                command.Parameters.AddWithValue("@WorkerFunction", workerFunction.Trim());
                command.Parameters.AddWithValue("@Position", position.Trim());

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
