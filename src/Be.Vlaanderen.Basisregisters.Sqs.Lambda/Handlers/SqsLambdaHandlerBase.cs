namespace Be.Vlaanderen.Basisregisters.Sqs.Lambda.Handlers;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AggregateSource;
using CommandHandling.Idempotency;
using Exceptions;
using FluentValidation;
using Infrastructure;
using MediatR;
using Requests;
using TicketingService.Abstractions;

public abstract class SqsLambdaHandlerBase<TSqsLambdaRequest> : IRequestHandler<TSqsLambdaRequest>
    where TSqsLambdaRequest : SqsLambdaRequest
{
    protected readonly ICustomRetryPolicy RetryPolicy;
    protected readonly ITicketing Ticketing;

    protected IIdempotentCommandHandler IdempotentCommandHandler { get; }

    protected SqsLambdaHandlerBase(
        ICustomRetryPolicy retryPolicy,
        ITicketing ticketing,
        IIdempotentCommandHandler idempotentCommandHandler)
    {
        RetryPolicy = retryPolicy;
        Ticketing = ticketing;
        IdempotentCommandHandler = idempotentCommandHandler;
    }

    protected abstract Task<object> InnerHandle(TSqsLambdaRequest request, CancellationToken cancellationToken);

    protected abstract TicketError? MapDomainException(DomainException exception, TSqsLambdaRequest request);

    protected virtual TicketError? MapValidationException(ValidationException exception, TSqsLambdaRequest request)
    {
        if (exception.Errors is not null)
        {
            return exception.Errors.Count() == 1
                ? new TicketError(exception.Errors.Single().ErrorMessage, exception.Errors.Single().ErrorCode)
                : new TicketError(exception.Errors.Select(error => new TicketError(error.ErrorMessage, error.ErrorCode)).ToList());
        }

        return null;
    }

    public async Task Handle(TSqsLambdaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await ValidateIfMatchHeaderValue(request, cancellationToken);

            await Ticketing.Pending(request.TicketId, cancellationToken);

            object? innerHandleResult = null;

            await RetryPolicy.Retry(async () => innerHandleResult = await InnerHandle(request, cancellationToken));

            await Ticketing.Complete(
                request.TicketId,
                new TicketResult(innerHandleResult),
                cancellationToken);
        }
        catch (AggregateIdIsNotFoundException)
        {
            await HandleAggregateIdIsNotFoundException(request, cancellationToken);
        }
        catch (Exception exception)
        {
            var ticketError = TryConvertToTicketError(exception, request);
            await Ticketing.Error(
                request.TicketId,
                ticketError,
                cancellationToken);
        }
    }

    protected TicketError TryConvertToTicketError(Exception exception, TSqsLambdaRequest request)
    {
        return exception switch
        {
            IfMatchHeaderValueMismatchException =>
                new TicketError("Als de If-Match header niet overeenkomt met de laatste ETag.", "PreconditionFailed"),
            ValidationException validationException =>
                MapValidationException(validationException, request)
                ?? new TicketError(exception.Message, ""),
            DomainException domainException =>
                MapDomainException(domainException, request)
                ?? new TicketError(exception.Message, ""),
            _ => throw exception
        };
    }

    protected abstract Task HandleAggregateIdIsNotFoundException(TSqsLambdaRequest request, CancellationToken cancellationToken);

    protected abstract Task ValidateIfMatchHeaderValue(TSqsLambdaRequest request, CancellationToken cancellationToken);
}
