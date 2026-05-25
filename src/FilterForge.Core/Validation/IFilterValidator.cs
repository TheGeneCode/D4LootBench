using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.Core.Validation;

/// <summary>
/// Validates a <see cref="FilterRuleset"/> against game-enforced constraints. The same
/// service is consumed by the export path (Copy Code / Save JSON), the Raw Editor's
/// Validate command, and the AI assistant before showing proposed edits.
/// </summary>
public interface IFilterValidator
{
    ValidationResult Validate(FilterRuleset ruleset);
}
