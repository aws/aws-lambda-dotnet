using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class TraceTestsController : Controller
    {
        [HttpGet]
        public string GetTraceId()
        {
            return this.HttpContext.TraceIdentifier;
        }
    }
}
