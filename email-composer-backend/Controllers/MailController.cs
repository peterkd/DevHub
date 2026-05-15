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

    [HttpGet("organization-roles")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetOrganizationRoles(
        CancellationToken cancellationToken)
    {
        try
        {
            var organizationRoles =
                await _sqlRecipientService.GetOrganizationRolesAsync(cancellationToken);

            return Ok(organizationRoles);
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
            var recipients = (request.ToRecipients ?? [])
                .Select(email => email.Trim())
                .Where(email => email.Length > 0);

            if (request.IncludeSqlRecipients)
            {
                var sqlRecipients =
                    await _sqlRecipientService.GetRecipientEmailAddressesAsync(
                        request.OrganizationRole!,
                        cancellationToken);

                recipients = recipients.Concat(sqlRecipients);
            }

            request.ToRecipients = recipients
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (request.ToRecipients.Count == 0)
            {
                return BadRequest(new
                {
                    message = request.IncludeSqlRecipients
                        ? "No recipients were found for the selected organization role."
                        : "At least one recipient is required."
                });
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
