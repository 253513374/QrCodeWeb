using OpenCvSharp;
using QrCodeWeb.Datas;
using System.Drawing;

namespace QrCodeWeb.Services
{
    public class Base64ToMat
    {
        public static Mat ToaMat(string base64, ref ResponseModel response)
        {
            base64 = base64.Replace(" ", "+");
            base64 = base64.Trim().Substring(base64.IndexOf(",") + 1);   //将‘，’以前的多余字符串删除
            MemoryStream stream = new MemoryStream(Convert.FromBase64String(base64));

            byte[] bytes = Convert.FromBase64String(base64);

            Mat m = Cv2.ImDecode(bytes, ImreadModes.Color);

            if (m.Empty())
            {
                response.Message = "base64解码失败";
                response.Code = "500";
            }
            stream.Close();
            return m;
        }

        public static string ToBase64(Mat mat)
        {
            using MemoryStream stream = new MemoryStream();

            var base64 = Convert.ToBase64String(mat.ToBytes());
            // Mat.ImDecode
            return base64;
        }
    }
}