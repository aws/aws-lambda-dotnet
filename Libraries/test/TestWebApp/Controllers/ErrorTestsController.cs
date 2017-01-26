using System;
using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class ErrorTestsController
    {
        [HttpGet]
        public string Get([FromQuery]string id)
        {
            throw new Exception("Unit test exception, for test conditions.");
        }
    }
}
