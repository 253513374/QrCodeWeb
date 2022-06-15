using OpenCvSharp;
using System;

namespace QrCodeWeb.Services
{
    public class Uititys
    {

        private IWebHostEnvironment Environment { get; }
        private readonly ILogger<DeCodeService> Logger;

        private readonly string FolderName;

        public Uititys(IWebHostEnvironment environment, ILogger<DeCodeService> logger)
        {
            Environment = environment;
            Logger = logger;
            FolderName = DateTime.Now.ToString("yyyyMMddHHmmss");
        }
        /// <summary>
        /// 计算向量 两点之间的距离
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public  double Distance(Point2f a, Point2f b)
        {
            var selfx = Math.Abs(a.X - b.X);
            var selfy = Math.Abs(a.Y - b.Y);
            var selflen = Math.Sqrt((selfx * selfx) + (selfy * selfy));
            return selflen;
        }


        /// <summary>
        /// base64 转换为图片
        /// </summary>
        /// <param name="base64"></param>
        /// <returns></returns>
        public Mat ToaMat(string base64)
        {
            try
            {
                base64 = base64.Replace(" ", "+");
                base64 = base64.Trim().Substring(base64.IndexOf(",") + 1);   //将‘，’以前的多余字符串删除
                byte[] bytes = Convert.FromBase64String(base64);

                Mat m = Cv2.ImDecode(bytes, ImreadModes.Color);

                if (m.Empty())
                {
                    return null;
                }
                // stream.Close();
                return m;
            }
            catch (Exception)
            {
                //response.Message = "base64解码失败";
                //response.Code = "50";
                return null;
                //  throw;
            }
        }

        /// <summary>
        ///  图片转换为base64
        /// </summary>
        /// <param name="mat"></param>
        /// <returns></returns>
        public string ToBase64(Mat mat)
        {
            using MemoryStream stream = new MemoryStream();

            var base64 = Convert.ToBase64String(mat.ToBytes());
            // Mat.ImDecode
            return base64;
        }



        /// <summary>
        /// 保存图像文件
        /// </summary>
        /// <param name="array"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public Task SaveMatFile(Mat array, string name)
        {
            var filepathw = Path.Combine(Environment.ContentRootPath, $"ImageProcessing");
            if (!Directory.Exists(filepathw))
            {
                Directory.CreateDirectory(filepathw);
            }

            var filepathw2 = Path.Combine(filepathw, $"{FolderName}");

            if (!Directory.Exists(filepathw2))
            {
                Directory.CreateDirectory(filepathw2);
            }

            var filepath = Path.Combine(filepathw2, $"{name}.jpg");
            array.SaveImage(filepath);
            return Task.CompletedTask;
        }

    }
}
