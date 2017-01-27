using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("api/[controller]")]
    public class ErrorTestsController
    {
        [HttpGet]
        public string Get([FromQuery]string id)
        {
            if (id == "typeload-test")
            {
                var fnfEx = new FileNotFoundException("Couldn't find file", "System.String.dll");
                throw new ReflectionTypeLoadException(new[] { typeof(String) }, new[] { fnfEx });
            }

            var ex = new Exception("Unit test exception, for test conditions.");
            if (id == "aggregate-test")
            {
                throw new AggregateException(ex);
            }
            else
            {
                throw ex;
            }
        }
    }
}
