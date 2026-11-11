using System.ComponentModel.DataAnnotations;

namespace AgentForge.Api.Filters;

public sealed class ValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var argument in context.Arguments.Where(argument => argument is not null))
        {
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(argument!);
            if (!Validator.TryValidateObject(argument!, validationContext, validationResults, validateAllProperties: true))
            {
                foreach (var result in validationResults)
                {
                    var members = result.MemberNames.Any() ? result.MemberNames : [argument!.GetType().Name];
                    foreach (var member in members)
                    {
                        errors[member] = [.. errors.GetValueOrDefault(member, []), result.ErrorMessage ?? "The value is invalid."];
                    }
                }
            }
        }

        return errors.Count == 0
            ? await next(context)
            : TypedResults.ValidationProblem(errors);
    }
}
