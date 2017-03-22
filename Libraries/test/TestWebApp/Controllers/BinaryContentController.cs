using System.IO;
using Microsoft.AspNetCore.Mvc;

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

            return base.File(bytes, LambdaFunction.BinaryContentType);
        }
    }
}