using Api.Domain.Orders.AggregateRoots.ValueObjects;

namespace Api.Modules.Sales.Features.Orders.CreateOrderRepr;

public sealed record OrderCreated(Guid OrderId);

public sealed record MissingOrderLines
{
    public string Code => "orders.missing_order_lines";
}

public sealed record MissingOrderLine(int Index)
{
    public string Code => "orders.missing_order_line";
}

public union CreateOrderError(MissingOrderLines, MissingOrderLine, OrderLineError);
