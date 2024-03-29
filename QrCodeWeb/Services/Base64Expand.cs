﻿using OpenCvSharp;
using System.Drawing;

namespace QrCodeWeb.Services
{
    public class Base64Expand
    {
        public static Mat ToaMat(string base64)
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

        public static string ToBase64(Mat mat)
        {
            using MemoryStream stream = new MemoryStream();

            var base64 = Convert.ToBase64String(mat.ToBytes());
            // Mat.ImDecode
            return base64;
        }
    }
}