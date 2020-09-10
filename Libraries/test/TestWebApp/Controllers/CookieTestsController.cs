using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;


namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class CookieTestsController : Controller
    {
        [HttpGet]
        public string Get()
        {
            var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions
            {
                Expires = DateTime.Now.AddMinutes(5)
            };
            Response.Cookies.Append("TestCookie", "TestValue", cookieOptions);
            return String.Empty;
        }

        [HttpGet("{id}")]
        public string Get(string id)
        {
            return Request.Cookies.FirstOrDefault(c => c.Key == id).Value;
        }
    }
}
