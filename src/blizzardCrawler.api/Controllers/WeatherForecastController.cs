using blizzardCrawler.shared;
using Microsoft.AspNetCore.Mvc;

namespace blizzardCrawler.api.Controllers
{
    [ApiController]
    [Route("/api/v1/[controller]")]
    public class MatchInfoController : ControllerBase
    {
        public MatchInfoController()
        {
        }

        [HttpGet]
        [Route("matchinfos")]
        public async Task<List<MatchDto>> GetMatchInfos()
        {
            await Task.Delay(500);
            return new();
        }
    }
}
