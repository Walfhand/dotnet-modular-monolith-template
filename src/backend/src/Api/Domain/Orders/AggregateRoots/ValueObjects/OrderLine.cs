using Engine.Outcomes;

namespace Api.Domain.Orders.AggregateRoots.ValueObjects;

public record OrderLine
{
    private OrderLine()
    {
    }

    private OrderLine(int productId, int quantity)
    {
        Quantity = quantity;
        ProductId = productId;
    }

    public int Quantity { get; init; }
    public int ProductId { get; init; }

    public static Outcome<OrderLine, OrderLineError> Create(int productId, int quantity)
    {
        if (quantity < 1)
            return (OrderLineError)new InvalidQuantity(productId, quantity);
        if (productId < 1)
            return (OrderLineError)new InvalidProductId(productId);

        return new OrderLine(productId, quantity);
    }

    public Outcome<OrderLine, OrderLineError> AddQuantity(int quantity)
    {
        if (quantity < 1)
            return (OrderLineError)new InvalidQuantity(ProductId, quantity);
        if (Quantity > int.MaxValue - quantity)
            return (OrderLineError)new QuantityOverflow(ProductId, Quantity, quantity);

        return this with { Quantity = Quantity + quantity };
    }
}
