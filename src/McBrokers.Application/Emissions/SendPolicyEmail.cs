using McBrokers.Application.Ports;
using McBrokers.Domain.Emissions;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Emissions;

public sealed record SendPolicyEmailCommand(Guid EmissionId, string ToAddress);

// Caso de uso del botón "Enviar por correo" en /Emision/Confirmacion.
// Construye EmailMessage con el PDF (BlobRef = ruta relativa, no bytes —
// el sender los resolverá vía IBlobStore cuando integremos Graph/SendGrid).
// Por ahora la impl real es LogEmailSender que solo loguea.
public sealed class SendPolicyEmail
{
    private readonly IEmissionRepository _emissions;
    private readonly IEmailSender _sender;

    public SendPolicyEmail(IEmissionRepository emissions, IEmailSender sender)
    {
        _emissions = emissions;
        _sender = sender;
    }

    public async Task<Result<bool>> ExecuteAsync(SendPolicyEmailCommand command, CancellationToken ct)
    {
        if (!IsValidEmail(command.ToAddress))
        {
            return Result<bool>.Failure("Recipient email is invalid.");
        }

        var emission = await _emissions.GetByIdAsync(command.EmissionId, ct).ConfigureAwait(false);
        if (emission is null)
        {
            return Result<bool>.Failure("Emission not found.");
        }
        if (emission.Status != EmissionStatus.Issued)
        {
            return Result<bool>.Failure("Emission is not in Issued status.");
        }
        if (string.IsNullOrWhiteSpace(emission.PdfBlobRef))
        {
            return Result<bool>.Failure("Emission has no PDF associated.");
        }

        var message = new EmailMessage(
            ToAddress: command.ToAddress,
            Subject: $"Tu póliza {emission.PolicyNumber}",
            BodyHtml: BuildBody(emission.PolicyNumber!),
            Attachments: new[]
            {
                new EmailAttachment(
                    FileName: $"poliza-{emission.PolicyNumber}.pdf",
                    ContentType: "application/pdf",
                    BlobRef: emission.PdfBlobRef!),
            });

        await _sender.SendAsync(message, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    // Validación deliberadamente laxa: contiene @ y al menos un punto a la
    // derecha. El front ya valida con type="email"; esta capa es defensa
    // en profundidad, no la fuente de verdad.
    private static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var at = value.IndexOf('@');
        return at > 0 && value.IndexOf('.', at) > at + 1;
    }

    private static string BuildBody(string policyNumber) =>
        $"<p>Adjunto encontrarás tu póliza emitida <strong>{policyNumber}</strong>.</p>";
}
