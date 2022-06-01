using Microsoft.AspNetCore.Mvc;
using QrCodeWeb.Datas;
using QrCodeWeb.Services;
using System.Text.Json;

namespace QrCodeWeb.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CutImageController : ControllerBase
    {
        //public IActionResult Index()
        //{
        //    return View();
        //}
        private CutImageService Serice { get; set; }

        private readonly ILogger<CutImageController> Logger;
        private IWebHostEnvironment Environment { get; set; }

        public CutImageController(CutImageService deCode, ILogger<CutImageController> logger, IWebHostEnvironment environment)
        {
            Serice = deCode;
            Logger = logger;
            Environment = environment;
        }

        [HttpPost(Name = "CutImage")]
        public ImgRecognitionResponse CutImage([FromBody] ImgQrCodeContent str)
        {
            //var model = JsonSerializer.Deserialize<ImgQrCodeContent>("");

            if (str == null)
            {
                return new ImgRecognitionResponse()
                {
                    imgRecognitionState = "1101",
                    imgRecognitionMessage = "参数为空"
                };
            }
            var result = Serice.CutImage(str);

            return result;
            //var cutImageService = new CutImageService();
            //var src =  Base64ToMat.ToaMat(base64);
            //var image = ImageHelper.CutImage(imagePath, x, y, width, height);
            //return File(image, "image/jpeg");
        }
    }
}