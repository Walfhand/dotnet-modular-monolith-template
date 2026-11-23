namespace Api.Domain.Orders.AggregateRoots.ValueObjects;

public sealed record InvalidProductId(int ProductId)
{
    public string Code => "orders.invalid_product_id";
}

public sealed record InvalidQuantity(int ProductId, int Quantity)
{
    public string Code => "orders.invalid_quantity";
}

public sealed record QuantityOverflow(int ProductId, int CurrentQuantity, int AdditionalQuantity)
{
    public string Code => "orders.quantity_overflow";
}

public union OrderLineError(InvalidProductId, InvalidQuantity, QuantityOverflow);
