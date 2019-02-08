using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class RedirectTestController : Controller
    {

        // GET api/values
        [HttpGet]
        public ActionResult Get()
        {
            return this.Redirect("redirecttarget");
        }
    }


    [Route("api/[controller]")]
    public class RedirectTargetController : Controller
    {

        // GET api/values
        [HttpGet]
        public string Get()
        {
            return "You have been redirected";
        }
    }
}
