using Microsoft.AspNetCore.Mvc;
using OpenCvSharp;
using QrCodeWeb.Datas;
using QrCodeWeb.Services;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace QrCodeWeb.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DeCodeController : Controller
    {
        private DeCodeService CodeService { get; set; }
        private readonly ILogger<DeCodeController> Logger;
        private IWebHostEnvironment Environment { get; set; }

        public DeCodeController(DeCodeService deCode, ILogger<DeCodeController> logger, IWebHostEnvironment environment)
        {
            CodeService = deCode;
            Logger = logger;
            Environment = environment;
        }

        //public IActionResult Index()
        //{
        //    return View();
        //}

        [HttpGet(Name = "DeCode")]
        public ResponseModel DeCode(string code)
        {
            var filepathw = Path.Combine(Environment.ContentRootPath, $"testdata");
            var codepath = Path.Combine(filepathw, $"{code}.jpg");
            //var codepath = Path.Combine(filepathw, $"333333.jpg");
            CodeService.FileNmae = code;

            ResponseModel response = new ResponseModel();
            var result = CodeService.DetectAndDecode(codepath, ref response);

            return response;
        }
    }
}