using System;
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
    /// get_my_orders: the signed-in user's recent orders.
    ///
    /// SECURITY: the email comes from context.UserEmail (the JWT), NEVER from the arguments,
    /// so the model can't be tricked into reading someone else's orders. IRequiresSignIn makes
    /// the engine hide this tool from anonymous users and refuse to run it for them.
    /// </summary>
    public class GetMyOrdersTool : ITool, IRequiresSignIn
    {
        private const int MaxLimit = 10;
        private const int DefaultLimit = 5;

        private readonly IGenericRepository<Order> _orders;

        public GetMyOrdersTool(IGenericRepository<Order> orders)
        {
            _orders = orders;
        }

        public string Name => "get_my_orders";

        public string Description =>
            "List the signed-in customer's own orders, newest first: id, date, status, total and item count. " +
            "The customer is already signed in whenever this tool is available, so call it directly. " +
            "Use get_order for the items of one order.";

        public JsonObject ParametersSchema => Schema.Object(
            new ToolParameter("limit", "How many orders to return, 1 to 10. Default 5.", false, ToolParameterTypes.Integer));

        public async Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            // Second line of defence: the registry already refuses anonymous calls.
            if (!context.IsSignedIn)
                return "Error: the user must sign in to see their orders.";

            int limit = Math.Min(Math.Max(arguments.GetInt("limit") ?? DefaultLimit, 1), MaxLimit);

            var orders = await _orders.ListAsync(new OrdersWithItemsAndOrderingSpecification(context.UserEmail));

            var items = orders.Take(limit).Select(o => new
            {
                id = o.Id,
                date = o.OrderDate.ToString("yyyy-MM-dd"),
                status = o.Status.ToString(),
                total = o.Subtotal + (o.DeliveryMethod?.Price ?? 0),
                itemCount = o.OrderItems?.Sum(i => i.Quantity) ?? 0
            }).ToList();

            foreach (var order in items)
                context.AddSource("order", order.id.ToString(), "Order #" + order.id);

            return ToolHelpers.ToJson(new { totalOrders = orders.Count, returned = items.Count, orders = items });
        }
    }
}
