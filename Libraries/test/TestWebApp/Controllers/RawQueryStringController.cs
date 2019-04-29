using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class RawQueryStringController : Controller
    {
        [HttpGet]
        public string Get()
        {
            return this.Request.QueryString.ToString();
        }

        [HttpGet]
        [Route("json")]
        public Results Get([FromQuery] string url, [FromQuery] DateTimeOffset testDateTimeOffset)
        {
            return new Results
            {
                Url = url,
                TestDateTimeOffset = testDateTimeOffset
            };
        }

        public class Results
        {
            public string Url { get; set; }
            public DateTimeOffset TestDateTimeOffset { get;set;}
        }
    }
}
