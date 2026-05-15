using EmailComposer.Backend.Models;
using EmailComposer.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmailComposer.Backend.Controllers;

[ApiController]
[Route("api/mail")]
public sealed class MailController : ControllerBase
{
    private readonly GraphMailService _graphMailService;
    private readonly SqlRecipientService _sqlRecipientService;

    public MailController(
        GraphMailService graphMailService,
        SqlRecipientService sqlRecipientService)
    {
        _graphMailService = graphMailService;
        _sqlRecipientService = sqlRecipientService;
    }

    [HttpGet("sql-recipient-organization-roles")]
    public async Task<IActionResult> GetSqlRecipientOrganizationRoles(CancellationToken cancellationToken)
    {
        try
        {
            var roles = await _sqlRecipientService.GetOrganizationRolesAsync(cancellationToken);
            return Ok(roles);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMail(
        [FromBody] SendMailRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            if (request.IncludeSqlRecipients)
            {
                if (string.IsNullOrWhiteSpace(request.OrganizationRole))
                {
                    return BadRequest(new { message = "OrganizationRole is required when IncludeSqlRecipients is true." });
                }

                var sqlRecipients =
                    await _sqlRecipientService.GetRecipientEmailAddressesAsync(
                        request.OrganizationRole.Trim(),
                        cancellationToken);

                request.ToRecipients = request.ToRecipients
                    .Concat(sqlRecipients)
                    .Select(email => email.Trim())
                    .Where(email => email.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            await _graphMailService.SendMailAsync(request, cancellationToken);
            return Ok(new { message = "Mail sent." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
