using FluentValidation;
using MediatR;
using PastryManager.Application.Common.Models;

namespace PastryManager.Application.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Count != 0)
        {
            var errors = failures.Select(f => f.ErrorMessage).ToList();
            
            // Return Result.Failure instead of throwing exception
            var resultType = typeof(TResponse);
            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var dataType = resultType.GetGenericArguments()[0];
                var failureMethod = typeof(Result<>)
                    .MakeGenericType(dataType)
                    .GetMethod(nameof(Result<object>.Failure), new[] { typeof(List<string>) });
                
                if (failureMethod != null)
                {
                    return (TResponse)failureMethod.Invoke(null, new object[] { errors })!;
                }
            }
            
            // Fallback to exception only if Result pattern is not used
            var errorMessage = string.Join("; ", errors);
            throw new ValidationException(errorMessage);
        }

        return await next();
    }
}
