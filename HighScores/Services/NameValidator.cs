using HighScores.Options;
using Microsoft.Extensions.Options;

namespace HighScores.Services;

public class NameValidator
{
    private readonly IOptions<NameConstraints> _constraintsOptions;

    public NameValidator(IOptions<NameConstraints> constraintsOptions)
    {
        _constraintsOptions = constraintsOptions;
    }

    public bool IsValidName(string name)
    {
        var constraints = _constraintsOptions.Value;

        return name.Length >= constraints.MinLength
               && name.Length <= constraints.MaxLength
               && name.All(constraints.AllowedCharacters.Contains);
    }
}