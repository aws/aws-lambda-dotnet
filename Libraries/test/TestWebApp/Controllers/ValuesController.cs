using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        // GET api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }

        [HttpPost]
        public async Task<IActionResult> ChectContentLength()
        {
            using (var sr = new StreamReader(Request.Body))
            {
                var content = await sr.ReadToEndAsync();

                var sb = new StringBuilder();
                sb.AppendLine($"Request content length: {Request.ContentLength}");
                return Content(sb.ToString());
            }
        }

        [HttpPut("no-body")]
        public IActionResult Test([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Body request = default)
        {
            return Accepted();
        }

        public class Body
        {
            public string Prop { get; set; }
        }
    }
}
