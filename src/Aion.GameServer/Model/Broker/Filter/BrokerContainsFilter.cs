using System.Linq;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Model.Broker.Filter;

/// <summary>Java parity: model/broker/filter/BrokerContainsFilter (ATracer).</summary>
public class BrokerContainsFilter : BrokerFilter
{
    private readonly int[] masks;

    public BrokerContainsFilter(params int[] masks)
    {
        this.masks = masks;
    }

    public override bool Accept(ItemTemplate template)
    {
        int mask = template.GetTemplateId() / 100000;
        return masks.Any(i => i == mask);
    }
}
