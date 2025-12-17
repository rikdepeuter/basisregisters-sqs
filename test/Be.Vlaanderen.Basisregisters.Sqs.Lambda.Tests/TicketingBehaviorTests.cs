using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Be.Vlaanderen.Basisregisters.AggregateSource;
using Be.Vlaanderen.Basisregisters.GrAr.Provenance;
using Be.Vlaanderen.Basisregisters.Sqs.Lambda.Handlers;
using Be.Vlaanderen.Basisregisters.Sqs.Lambda.Infrastructure;
using Be.Vlaanderen.Basisregisters.Sqs.Lambda.Requests;
using Moq;
using NodaTime;
using TicketingService.Abstractions;

namespace Be.Vlaanderen.Basisregisters.Sqs.Lambda.Tests;

public class TicketingBehaviorTests
{
    [Fact]
    public async Task WhenNone_ThenNoneAreCalled()
    {
        var ticketingMock = new Mock<ITicketing>();

        await ExecuteHandler(ticketingMock.Object, TicketingBehavior.None);

        ticketingMock
            .Verify(x => x.Pending(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        ticketingMock
            .Verify(x => x.Complete(It.IsAny<Guid>(), It.IsAny<TicketResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenOnlyPending_ThenOnlyPendingIsCalled()
    {
        var ticketingMock = new Mock<ITicketing>();

        await ExecuteHandler(ticketingMock.Object, TicketingBehavior.Pending);

        ticketingMock
            .Verify(x => x.Pending(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        ticketingMock
            .Verify(x => x.Complete(It.IsAny<Guid>(), It.IsAny<TicketResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenOnlyComplete_ThenOnlyCompleteIsCalled()
    {
        var ticketingMock = new Mock<ITicketing>();

        await ExecuteHandler(ticketingMock.Object, TicketingBehavior.Complete);

        ticketingMock
            .Verify(x => x.Pending(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        ticketingMock
            .Verify(x => x.Complete(It.IsAny<Guid>(), It.IsAny<TicketResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenAll_ThenPendingAndCompleteAreCalled()
    {
        var ticketingMock = new Mock<ITicketing>();

        await ExecuteHandler(ticketingMock.Object, TicketingBehavior.All);

        ticketingMock
            .Verify(x => x.Pending(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        ticketingMock
            .Verify(x => x.Complete(It.IsAny<Guid>(), It.IsAny<TicketResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private Task ExecuteHandler(ITicketing ticketing, TicketingBehavior ticketingBehavior)
    {
        var handler = new TestSqsLambdaHandler(ticketing, ticketingBehavior);
        var lambdaRequest = new SqsLambdaRequest(string.Empty,
            Guid.NewGuid(),
            null,
            new Provenance(Instant.MinValue, Application.Unknown, new Reason(string.Empty), new Operator(string.Empty), Modification.Unknown, Organisation.DigitaalVlaanderen),
            new Dictionary<string, object?>());
        return handler.Handle(lambdaRequest, CancellationToken.None);
    }

    private sealed class TestSqsLambdaHandler : SqsLambdaHandlerBase<SqsLambdaRequest>
    {
        public TestSqsLambdaHandler(
            ITicketing ticketing,
            TicketingBehavior ticketingBehavior)
            : base(new LambdaHandlerRetryPolicy(0, 0),
                ticketing,
                null!,
                ticketingBehavior)
        {
        }

        protected override Task<object> InnerHandle(SqsLambdaRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new object());
        }

        protected override TicketError? MapDomainException(DomainException exception, SqsLambdaRequest request)
        {
            return null;
        }

        protected override Task HandleAggregateIdIsNotFoundException(SqsLambdaRequest request, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected override Task ValidateIfMatchHeaderValue(SqsLambdaRequest request, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
