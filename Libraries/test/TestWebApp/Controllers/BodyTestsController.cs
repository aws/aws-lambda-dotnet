using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;


namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class BodyTestsController : Controller
    {
        [HttpPut]
        public string PutBody([FromBody] Person body)
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
