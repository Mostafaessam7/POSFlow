using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PosFlow.Api.Filters;

/// <summary>
/// Looks up an IValidator&lt;T&gt; for every action argument (if one is
/// registered in DI) and runs it before the action executes. This keeps
/// controllers free of manual validation calls without needing MediatR.
/// </summary>
public sealed class ValidationFilter(
    IServiceProvider serviceProvider)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>)
                .MakeGenericType(argument.GetType());

            if (serviceProvider.GetService(validatorType)
                is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);

            var result = await validator.ValidateAsync(
                validationContext,
                context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                var errors = result.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .Select(error => error.ErrorMessage)
                            .ToArray());

                context.Result = new BadRequestObjectResult(new
                {
                    message = "بيانات غير صالحة.",
                    errors
                });

                return;
            }
        }

        await next();
    }
}
