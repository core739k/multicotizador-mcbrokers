using McBrokers.Application.Validation;

namespace McBrokers.Application.Tests.Validation;

public class ValidationMessagesTests
{
    [Fact]
    public void Required_uses_DisplayName_placeholder_and_is_in_Spanish()
    {
        ValidationMessages.Required.Should().Contain("{0}",
            because: "Razor sustituye {0} por el DisplayName del campo");
        ValidationMessages.Required.Should().Contain("obligatorio");
    }

    [Theory]
    [InlineData("Rfc", "RFC")]
    [InlineData("Phone", "teléfono")]
    [InlineData("Email", "email")]
    [InlineData("PostalCode", "código postal")]
    [InlineData("StateCode", "estado")]
    [InlineData("SumInsuredPositive", "suma asegurada")]
    public void All_messages_mention_their_target_field_in_Spanish(string fieldName, string spanishHint)
    {
        var field = typeof(ValidationMessages).GetField(fieldName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        field.Should().NotBeNull($"the constant {fieldName} should exist");

        var value = (string)field!.GetValue(null)!;
        value.Should().Contain(spanishHint,
            because: $"the message for {fieldName} should mention '{spanishHint}' in Spanish");
    }

    [Fact]
    public void Rfc_message_mentions_length_bounds()
    {
        ValidationMessages.Rfc.Should().Contain("12").And.Contain("13");
    }

    [Fact]
    public void Phone_message_mentions_10_digits()
    {
        ValidationMessages.Phone.Should().Contain("10");
    }

    [Fact]
    public void PostalCode_message_mentions_5_digits()
    {
        ValidationMessages.PostalCode.Should().Contain("5");
    }
}
