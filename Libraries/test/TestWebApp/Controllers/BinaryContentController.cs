using Microsoft.AspNetCore.Mvc;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace TestWebApp.Controllers
{
    [Route("api/binary")]
    public class BinaryContentController : Controller
    {
        [HttpGet]
        public IActionResult Get([FromQuery] string firstName, [FromQuery] string lastName)
        {
            var bytes = new byte[byte.MaxValue];

            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)i;

            return base.File(bytes, Application.Octet);
        }

        [HttpPut]
        public string Put()
        {
            using (var reader = new StreamReader(this.HttpContext.Request.Body))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
