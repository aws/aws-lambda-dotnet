using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class AuthTestController : Controller
    {
        // GET: api/<controller>
        [HttpGet]
        [Authorize(Policy = "YouAreSpecial")]
        public string Get()
        {
            return "You Have Access";
        }
    }
}
