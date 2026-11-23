using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agent;
using Agent.Tools;
using Core.Entities.OrderAggregate;
using Core.Interfaces;
using Core.Specifications;

namespace Infrastructure.AgentTools
{
    /// <summary>
    /// get_order: one order with its items, ONLY if it belongs to the signed-in user.
    ///
    /// The query filters on BOTH the order id and context.UserEmail. Someone else's order looks
    /// exactly like a missing one ("not found"), so the tool never reveals that it exists.
    /// </summary>
    public class GetOrderTool : ITool, IRequiresSignIn
    {
        private readonly IGenericRepository<Order> _orders;

        public GetOrderTool(IGenericRepository<Order> orders)
        {
            _orders = orders;
        }

        public string Name => "get_order";

        public string Description =>
            "Get one of the signed-in customer's orders with its items, delivery method and status.";

        public JsonObject ParametersSchema => Schema.Object(
            new ToolParameter("orderId", "The order's id, e.g. from get_my_orders.", true, ToolParameterTypes.Integer));

        public async Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            if (!context.IsSignedIn)
                return "Error: the user must sign in to see their orders.";

            int? id = arguments.GetInt("orderId");
            if (!id.HasValue || id.Value < 1)
                return "Error: orderId must be a whole number, e.g. 3.";

            Order order = await _orders.GetEntityWithSpec(new OrdersWithItemsAndOrderingSpecification(id.Value, context.UserEmail));
            if (order == null)
                return "Error: order " + id.Value + " was not found in this customer's orders.";

            context.AddSource("order", order.Id.ToString(), "Order #" + order.Id);

            return ToolHelpers.ToJson(new
            {
                id = order.Id,
                date = order.OrderDate.ToString("yyyy-MM-dd"),
                status = order.Status.ToString(),
                delivery = order.DeliveryMethod == null ? null : new
                {
                    name = order.DeliveryMethod.ShortName,
                    deliveryTime = order.DeliveryMethod.DeliveryTime,
                    price = order.DeliveryMethod.Price
                },
                items = order.OrderItems?.Select(i => new
                {
                    productId = i.ItemOrdered?.ProductItemId,
                    name = i.ItemOrdered?.ProductName,
                    price = i.Price,
                    quantity = i.Quantity
                }),
                subtotal = order.Subtotal,
                total = order.Subtotal + (order.DeliveryMethod?.Price ?? 0)
            });
        }
    }
}
