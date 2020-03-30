using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class RequestServicesExampleController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            return this.HttpContext.RequestServices?.GetType().FullName;
        }
    }
}
