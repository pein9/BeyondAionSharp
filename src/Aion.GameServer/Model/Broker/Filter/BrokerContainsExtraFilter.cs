using System.Linq;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Model.Broker.Filter;

/// <summary>Java parity: model/broker/filter/BrokerContainsExtraFilter (ATracer).</summary>
public class BrokerContainsExtraFilter : BrokerFilter
{
    private readonly int[] masks;

    public BrokerContainsExtraFilter(params int[] masks)
    {
        this.masks = masks;
    }

    public override bool Accept(ItemTemplate template)
    {
        int mask = template.GetTemplateId() / 10000;
        return masks.Any(i => i == mask);
    }
}
