using McBrokers.Application.Emissions;
using McBrokers.Application.Ports;
using McBrokers.Domain.Emissions;
using Moq;

namespace McBrokers.Application.Tests.Emissions;

// Caso de uso para el botón "Enviar por correo" de la página de confirmación
// de emisión. Construye un EmailMessage con el PDF adjunto y delega en
// IEmailSender — en dev queda en log; en prod será Microsoft Graph/SendGrid.
public class SendPolicyEmailTests
{
    private readonly Mock<IEmissionRepository> _emissions = new();
    private readonly Mock<IEmailSender> _sender = new();

    private SendPolicyEmail BuildHandler() => new(_emissions.Object, _sender.Object);

    private static Emission BuildIssuedEmission(string? pdfBlobRef = "2024/ACURA/MDX/cid-abc/poliza-AxaDxn.pdf")
    {
        var emission = Emission.Start(
            quotationInsurerResultId: Guid.NewGuid(),
            agentId: Guid.NewGuid(),
            createdAt: DateTime.UtcNow).Value;
        emission.MarkIssued("4", pdfBlobRef, DateTime.UtcNow);
        return emission;
    }

    [Fact]
    public async Task Returns_failure_when_emission_not_found()
    {
        var id = Guid.NewGuid();
        _emissions.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Emission?)null);

        var result = await BuildHandler().ExecuteAsync(
            new SendPolicyEmailCommand(id, "cliente@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
        _sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Returns_failure_when_emission_is_not_issued()
    {
        var emission = Emission.Start(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow).Value;
        emission.MarkFailed("rechazada");
        _emissions.Setup(r => r.GetByIdAsync(emission.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emission);

        var result = await BuildHandler().ExecuteAsync(
            new SendPolicyEmailCommand(emission.Id, "cliente@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Returns_failure_when_pdf_blob_ref_missing()
    {
        var emission = BuildIssuedEmission(pdfBlobRef: null);
        _emissions.Setup(r => r.GetByIdAsync(emission.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emission);

        var result = await BuildHandler().ExecuteAsync(
            new SendPolicyEmailCommand(emission.Id, "cliente@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _sender.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-arroba")]
    public async Task Returns_failure_when_email_is_invalid(string email)
    {
        var emission = BuildIssuedEmission();
        _emissions.Setup(r => r.GetByIdAsync(emission.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emission);

        var result = await BuildHandler().ExecuteAsync(
            new SendPolicyEmailCommand(emission.Id, email), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sends_email_with_pdf_attachment_pointing_to_blob_ref()
    {
        var emission = BuildIssuedEmission("2024/ACURA/MDX/cid-abc/poliza-AxaDxn.pdf");
        _emissions.Setup(r => r.GetByIdAsync(emission.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emission);

        EmailMessage? captured = null;
        _sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => captured = m)
            .Returns(Task.CompletedTask);

        var result = await BuildHandler().ExecuteAsync(
            new SendPolicyEmailCommand(emission.Id, "cliente@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.ToAddress.Should().Be("cliente@example.com");
        captured.Subject.Should().Contain("4"); // PolicyNumber
        captured.Attachments.Should().ContainSingle();
        captured.Attachments[0].BlobRef.Should().Be("2024/ACURA/MDX/cid-abc/poliza-AxaDxn.pdf");
        captured.Attachments[0].ContentType.Should().Be("application/pdf");
    }
}
