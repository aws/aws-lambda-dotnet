using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    /// <summary>
    /// Endpoint protected by a bare [Authorize] (no policy or role), used to verify that an API Gateway
    /// authorizer context without claims is not treated as an authenticated user.
    /// </summary>
    [Route("api/authtestbare")]
    public class AuthTestBareController : Controller
    {
        [HttpGet]
        [Authorize]
        public string Get() => $"Claims={User.Claims.Count()}";
    }
}
