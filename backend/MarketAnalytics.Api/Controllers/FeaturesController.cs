using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// Tells the website which optional parts are switched on.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FeaturesController : ControllerBase
{
    private readonly FeatureSettings _features;

    public FeaturesController(IOptions<FeatureSettings> features)
    {
        _features = features.Value;
    }

    [HttpGet]
    public ActionResult<FeatureSettings> Get() => Ok(_features);
}
