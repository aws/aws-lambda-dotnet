using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class ResourcePathController : Controller
    {
        // GET api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(string id)
        {
            return "value=" + id;
        }

        [Route("/api/[controller]/string/{*value}")]
        [HttpGet]
        public string GetString(string value)
        {
            var path = this.HttpContext.Request.Path;
            return "value=" + value;
        }

        [HttpGet("/api/[controller]/encoding/{first}/{second}", Name = "Multi")]
        public ActionResult Multi(string first, string second) => Ok(new { first, second });

        [HttpGet("/api/[controller]/encoding/{only}", Name = "Single")]
        public ActionResult Single(string only) => Ok(new { only });

    }
}
