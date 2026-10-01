using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Quayside.Infrastructure.Configuration;

public sealed class QuaysideOptionsFactory(IConfiguration configuration, IEnumerable<IValidateOptions<QuaysideOptions>> validators)
    : IOptionsFactory<QuaysideOptions>
{
    public QuaysideOptions Create(string name)
    {
        var options = QuaysideOptions.From(configuration);

        var failures = validators
            .Select(validator => validator.Validate(name, options))
            .Where(result => result.Failed)
            .SelectMany(result => result.Failures ?? [])
            .ToArray();

        if (failures.Length > 0)
            throw new OptionsValidationException(name, typeof(QuaysideOptions), failures);

        return options;
    }
}
