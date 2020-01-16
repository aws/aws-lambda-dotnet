using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class CompressResponseController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            var response = "[\"value1\",\"value2\"]";
            return Content(response, "application/json-compress");
        }
    }
}
