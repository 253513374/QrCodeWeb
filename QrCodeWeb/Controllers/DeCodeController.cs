using Microsoft.AspNetCore.Mvc;
using OpenCvSharp;
using QrCodeWeb.Services;
using static System.Net.Mime.MediaTypeNames;

namespace QrCodeWeb.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DeCodeController : Controller
    {
        private DeCodeService CodeService { get; set; }
        private readonly ILogger<DeCodeController> Logger;

        public DeCodeController(DeCodeService deCode, ILogger<DeCodeController> logger)
        {
            CodeService = deCode;
            Logger = logger;
        }

        //public IActionResult Index()
        //{
        //    return View();
        //}

        [HttpGet(Name = "DeCode")]
        public string DeCode(string code)
        {
            var codepath = @"D:\Desktop\QRCode\111111.png";
            CodeService.Decode(codepath);
            return "";
        }
    }
}