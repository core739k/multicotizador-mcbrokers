using System.ComponentModel.DataAnnotations;
using McBrokers.Application.Admin;
using McBrokers.Domain.Insurers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Admin.Insurers;

public class EditModel : PageModel
{
    private readonly GetInsurer _getInsurer;
    private readonly UpdateInsurer _updateInsurer;
    private readonly UpsertInsurerConfig _upsertConfig;

    public EditModel(GetInsurer getInsurer, UpdateInsurer updateInsurer, UpsertInsurerConfig upsertConfig)
    {
        _getInsurer = getInsurer;
        _updateInsurer = updateInsurer;
        _upsertConfig = upsertConfig;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public ConfigInputModel ConfigInput { get; set; } = new();

    public InsurerDetailView Detail { get; private set; } = null!;
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public IReadOnlyList<InsurerEnvironment> AllEnvironments { get; } = Enum.GetValues<InsurerEnvironment>();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _getInsurer.ExecuteAsync(id, cancellationToken);
        if (detail is null) return NotFound();

        Detail = detail;
        Input = new InputModel
        {
            Id = detail.Insurer.Id,
            Name = detail.Insurer.Name,
            DisplayOrder = detail.Insurer.DisplayOrder,
            IsEnabled = detail.Insurer.IsEnabled,
            LogoUrl = detail.Insurer.LogoUrl,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostGeneralAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return await Reload(Input.Id, cancellationToken);

        var result = await _updateInsurer.ExecuteAsync(
            new UpdateInsurerCommand(Input.Id, Input.Name, Input.DisplayOrder, Input.IsEnabled, Input.LogoUrl),
            cancellationToken);

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error;
            return await Reload(Input.Id, cancellationToken);
        }

        return RedirectToPage(new { id = Input.Id });
    }

    public async Task<IActionResult> OnPostConfigAsync(CancellationToken cancellationToken)
    {
        var result = await _upsertConfig.ExecuteAsync(
            new UpsertInsurerConfigCommand(
                ConfigInput.InsurerId,
                ConfigInput.Environment,
                ConfigInput.EndpointUrl,
                ConfigInput.BusinessNumber,
                ConfigInput.AgentCode,
                ConfigInput.KeyVaultSecretName,
                ConfigInput.TimeoutSeconds,
                ConfigInput.MaxRetries),
            cancellationToken);

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error;
            return await Reload(ConfigInput.InsurerId, cancellationToken);
        }

        SuccessMessage = $"Configuración {ConfigInput.Environment} guardada.";
        return await Reload(ConfigInput.InsurerId, cancellationToken);
    }

    private async Task<IActionResult> Reload(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _getInsurer.ExecuteAsync(id, cancellationToken);
        if (detail is null) return NotFound();
        Detail = detail;
        Input = new InputModel
        {
            Id = detail.Insurer.Id,
            Name = detail.Insurer.Name,
            DisplayOrder = detail.Insurer.DisplayOrder,
            IsEnabled = detail.Insurer.IsEnabled,
            LogoUrl = detail.Insurer.LogoUrl,
        };
        return Page();
    }

    public class InputModel
    {
        public Guid Id { get; set; }

        [Required, StringLength(200, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }

        public bool IsEnabled { get; set; }

        public string? LogoUrl { get; set; }
    }

    public class ConfigInputModel
    {
        public Guid InsurerId { get; set; }
        public InsurerEnvironment Environment { get; set; }
        public string EndpointUrl { get; set; } = string.Empty;
        public string BusinessNumber { get; set; } = string.Empty;
        public string AgentCode { get; set; } = string.Empty;
        public string KeyVaultSecretName { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
    }
}
