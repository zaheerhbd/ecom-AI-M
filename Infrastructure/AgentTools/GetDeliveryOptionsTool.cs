using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agent;
using Agent.Tools;
using Core.Entities.OrderAggregate;
using Core.Interfaces;

namespace Infrastructure.AgentTools
{
    /// <summary>
    /// get_delivery_options: the delivery methods a customer can choose at checkout,
    /// with price and delivery time. Straight from the DeliveryMethods table.
    /// </summary>
    public class GetDeliveryOptionsTool : ITool
    {
        private readonly IGenericRepository<DeliveryMethod> _deliveryMethods;

        public GetDeliveryOptionsTool(IGenericRepository<DeliveryMethod> deliveryMethods)
        {
            _deliveryMethods = deliveryMethods;
        }

        public string Name => "get_delivery_options";

        public string Description =>
            "List the delivery options available at checkout, with price and delivery time. " +
            "Use it for questions about delivery speed, delivery cost or free delivery.";

        public JsonObject ParametersSchema => Schema.Object();

        public async Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            var methods = await _deliveryMethods.ListAllAsync();

            var options = methods
                .OrderBy(m => m.Price)
                .Select(m => new
                {
                    name = m.ShortName,
                    description = m.Description,
                    deliveryTime = m.DeliveryTime,
                    price = m.Price
                })
                .ToList();

            context.AddSource("delivery", "delivery-methods", "Delivery options");
            return ToolHelpers.ToJson(new { options });
        }
    }
}
