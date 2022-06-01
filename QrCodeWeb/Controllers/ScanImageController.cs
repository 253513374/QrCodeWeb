using Microsoft.AspNetCore.Mvc;

using QrCodeWeb.Datas;
using QrCodeWeb.Services;

namespace QrCodeWeb.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ScanImageController : ControllerBase
    {
        private CutImageService Serice { get; set; }

        private readonly ILogger<CutImageController> Logger;
        private IWebHostEnvironment Environment { get; set; }

        public ScanImageController(CutImageService deCode, ILogger<CutImageController> logger, IWebHostEnvironment environment)
        {
            Serice = deCode;
            Logger = logger;
            Environment = environment;
        }

        /// <summary>
        /// 扫描上传原图像,检测二位码是否可用
        /// </summary>
        /// <param name="imgBase64"></param>
        /// <returns></returns>
        [HttpPost(Name = "scanqrimg")]
        public ImgRecognitionResponse ScanQrImg([FromBody] string imgBase64)
        {
            Logger.LogInformation($"ScanQrImg接收前端原图图");
            // Mat mat = new Mat(@"C:\Users\q4528\Desktop\测试数据\定位\3.jpg", ImreadModes.AnyColor);
            return Serice.IsQrImg(imgBase64);
        }
    }
}