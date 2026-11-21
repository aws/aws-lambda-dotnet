using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("api/largeresponse")]
    public class LargeResponseController : Controller
    {
        [HttpGet]
        public IActionResult Get()
        {
            // Larger than the 6 MB Lambda response payload limit.
            return Content(new string('*', 7 * 1024 * 1024), "text/plain");
        }
    }
}
