using Shared.Models.Licenses;

namespace Collector.Services.Implementation.Bridge.Helpers;

public static class ProductFeatureHelper
{
    public static ProductFeature GetFeature()
    {
        return ProductFeature.CommunityEdition;
    }
}