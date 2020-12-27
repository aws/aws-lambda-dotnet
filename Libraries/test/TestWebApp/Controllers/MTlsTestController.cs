using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class MTlsTestController : Controller
    {
        // GET: api/<controller>
        [HttpGet]
        public string Get()
        {
            return Request.HttpContext.Connection.ClientCertificate.Subject;
        }
    }
}