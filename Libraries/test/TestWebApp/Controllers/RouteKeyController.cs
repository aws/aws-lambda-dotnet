using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;


namespace TestWebApp.Controllers
{
    [Route("/")]
    public class RouteKeyController : Controller
    {
        [HttpPost("$default")]
        public string PostBody([FromBody] Person body)
        {
            return $"{body.LastName}, {body.FirstName}";
        }

        public class Person
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }
    }
}
