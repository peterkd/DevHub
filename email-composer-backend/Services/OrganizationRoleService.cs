using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EmailComposer.Backend.Services;

public sealed class OrganizationRoleService
{
    private const string ConnectionStringName = "AzureSqlDatabase";

    private const string OrganizationRolesQuery = """
        SELECT DISTINCT [OrganizationRole]
        FROM [dbo].[WorkerRole]
        ORDER BY [OrganizationRole];
        """;

    private const string UserRolesQuery = """
        SELECT DISTINCT [Name]
        FROM [dbo].[WorkerRole]
        WHERE [OrganizationRole] = @OrganizationRole
        ORDER BY [Name];
        """;

    private readonly IConfiguration _configuration;

    public OrganizationRoleService(IConfiguration configuration)
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

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(OrganizationRolesQuery, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(0))
                {
                    continue;
                }

                var role = reader.GetString(0).Trim();
                if (role.Length > 0)
                {
                    roles.Add(role);
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

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(
        string organizationRole,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationRole))
        {
            throw new InvalidOperationException("Organization role is required.");
        }

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

            await using var command = new SqlCommand(UserRolesQuery, connection);
            command.Parameters.AddWithValue("@OrganizationRole", organizationRole.Trim());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(0))
                {
                    continue;
                }

                var role = reader.GetString(0).Trim();
                if (role.Length > 0)
                {
                    roles.Add(role);
                }
            }
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                "Unable to load user roles from Azure SQL.",
                ex);
        }

        return roles;
    }
}
