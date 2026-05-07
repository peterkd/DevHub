using EmailComposer.Backend.Models;
using EmailComposer.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmailComposer.Backend.Controllers;

[ApiController]
[Route("api/mail")]
public sealed class MailController : ControllerBase
{
    private readonly GraphMailService _graphMailService;

    public MailController(GraphMailService graphMailService)
    {
        _graphMailService = graphMailService;
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
            await _graphMailService.SendMailAsync(request, cancellationToken);
            return Ok(new { message = "Mail sent." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
