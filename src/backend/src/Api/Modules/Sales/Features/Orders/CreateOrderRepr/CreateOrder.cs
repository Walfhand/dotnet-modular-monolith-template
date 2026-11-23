using Api.Domain.Orders.AggregateRoots;
using Api.Domain.Orders.AggregateRoots.ValueObjects;
using Api.Infrastructure.Persistence;
using Engine.Outcomes;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine;
using Wolverine.Http;

namespace Api.Modules.Sales.Features.Orders.CreateOrderRepr;

public sealed class CreateOrder
{
    public sealed record Request
    {
        public RequestBody Body { get; set; } = null!;

        public sealed record RequestBody
        {
            public List<OrderLineDto?>? OrderLines { get; init; }
        }
    }

    public sealed record OrderLineDto(int ProductId, int Quantity);

    public static class CreateRequestHandler
    {
        public static async Task<Outcome<OrderCreated, CreateOrderError>> Handle(
            Request request, AppDbContext dbContext, CancellationToken cancellationToken)
        {
            if (request.Body?.OrderLines is not { } orderLines)
                return (CreateOrderError)new MissingOrderLines();

            var order = Order.Create();
            for (var index = 0; index < orderLines.Count; index++)
            {
                if (orderLines[index] is not { } orderLine)
                    return (CreateOrderError)new MissingOrderLine(index);
                if (order.AddLine(orderLine.ProductId, orderLine.Quantity) is OrderLineError error)
                    return (CreateOrderError)error;
            }

            await dbContext.Set<Order>().AddAsync(order, cancellationToken);
            return new OrderCreated(order.Id);
        }
    }
}

public static class CreateOrderEndpoint
{
    [WolverinePost("/api/v1/orders")]
    public static async Task<Results<
        Created<OrderCreated>,
        BadRequest<MissingOrderLines>,
        BadRequest<MissingOrderLine>,
        UnprocessableEntity<InvalidProductId>,
        UnprocessableEntity<InvalidQuantity>,
        UnprocessableEntity<QuantityOverflow>>> Post(
        CreateOrder.Request.RequestBody body, IMessageBus bus, CancellationToken cancellationToken)
    {
        var outcome = await bus.InvokeAsync<Outcome<OrderCreated, CreateOrderError>>(
            new CreateOrder.Request { Body = body }, cancellationToken);
        return outcome switch
        {
            OrderCreated order => TypedResults.Created("", order),
            CreateOrderError error => error switch
            {
                MissingOrderLines missing => TypedResults.BadRequest(missing),
                MissingOrderLine missing => TypedResults.BadRequest(missing),
                OrderLineError lineError => lineError switch
                {
                    InvalidProductId invalid => TypedResults.UnprocessableEntity(invalid),
                    InvalidQuantity invalid => TypedResults.UnprocessableEntity(invalid),
                    QuantityOverflow overflow => TypedResults.UnprocessableEntity(overflow)
                }
            }
        };
    }
}
