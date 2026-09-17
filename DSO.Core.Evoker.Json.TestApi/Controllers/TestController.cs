using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DSO.Core.Evoker.Json.TestApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {

        [HttpGet("Test1JsonTests")]
        public void Test1JsonTests()
        {
            JsonTests.RunAll();
        }
    }
}
