using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class QueryStringController : Controller
    {

        [HttpGet]
        public string Get([FromQuery] string firstName, [FromQuery] string lastName)
        {
            if (HttpContext.Request.Query.TryGetValue("mv-test", out var values))
            {
                return string.Join(",", values);
            }
            return $"{firstName}, {lastName}";
        }
    }
}
