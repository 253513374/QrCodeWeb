using OpenCvSharp;
using QrCodeWeb.Datas;
using ScanImgShared;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace QrCodeWeb.Services
{
    public class CutImageService
    {
        private const string _wechat_QCODE_detector_prototxt_path = "WechatQrCodeFiles/detect.prototxt";
        private const string _wechat_QCODE_detector_caffe_model_path = "WechatQrCodeFiles/detect.caffemodel";
        private const string _wechat_QCODE_super_resolution_prototxt_path = "WechatQrCodeFiles/sr.prototxt";
        private const string _wechat_QCODE_super_resolution_caffe_model_path = "WechatQrCodeFiles/sr.caffemodel";
        private IWebHostEnvironment Environment { get; }
        private readonly ILogger<DeCodeService> Logger;

        public string FileNmae { get; set; }
        public int FileNmaeIdnex { get; set; } = 1;

        public CutImageService(IWebHostEnvironment environment, ILogger<DeCodeService> logger)
        {
            Environment = environment;
            Logger = logger;
        }

        public ImgRecognitionResponse CutImage(ImgQrCodeContent model)
        {
            try
            {
                ImgQrCodeContent imgResponseModel = new ImgQrCodeContent();

                if (model.CutedImgBase64Content.Length < 10)
                {
                    return SetResponse("1101", "CutImage图片切图失败,Base64长度不对");
                }
                using Mat mat = Base64ToMat.ToaMat(model.CutedImgBase64Content);

                SaveMatFile(mat, $"CutImage_接收需要裁图的原图");

                //Rect rect = new Rect(model.X, model.Y, model.Width, model.Height);
                //Mat roi = mat.SubMat(rect);

                //SaveMatFile(roi, $"CutImage_二维码位置定位图");

                Point centerPoint = new Point(model.CenterX, model.CenterY);

                int qrsize = model.QrCodeWidth;

                int qr_x = ((int)centerPoint.X - qrsize / 2) - qrsize;
                int qr_y = ((int)centerPoint.Y - qrsize / 2) - qrsize;
                int qr_max_width = qrsize * 3;
                int qr_max_height = qrsize * 3;

                qr_x = qr_x > 0 ? qr_x : 1;
                qr_y = qr_y > 0 ? qr_y : 1;

                int maxw = mat.Width - qr_x;
                int maxh = mat.Height - qr_y;

                qr_max_width = (qr_x + qr_max_width) > maxw ? maxw - 1 : qr_max_width;
                qr_max_height = qr_max_height > maxh ? maxh - 1 : qr_max_height;

                Rect qr_rect = new Rect(qr_x, qr_y, qr_max_width, qr_max_height);

                Mat qr_roi = mat.SubMat(qr_rect);
                SaveMatFile(qr_roi, $"CutImage_裁剪成功的图片");
                if (qr_roi.Empty())
                {
                    return SetResponse("1102", "CutImage图片切图失败,qr_roi为空");
                }
                else
                {
                    return SetResponse("1000", $"CutImage图片切图成功{qr_roi.Width}:{qr_roi.Height}", null, $"data:image/jpeg;base64,{Base64ToMat.ToBase64(qr_roi)}");
                }
            }
            catch (Exception ex)
            {
                return SetResponse("1199", $"CutImage图片切图异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 计算二维码是否在指定范围内
        /// </summary>
        /// <param name="mat"></param>
        /// <returns></returns>
        public ImgRecognitionResponse IsQrImg(string imgBase64)
        {
            Mat mat = Base64Expand.ToaMat(imgBase64);
            if (mat is null)
            {
                return SetResponse("1102", "IsQrImg图像为空");
            }

            SaveMatFile(mat, $"IsQrImg_接收原图");

            Logger.LogInformation("IsQrImg开始计算二维码是否在指定范围内");

            try
            {
                Mat roi = new Mat();
                Rect rect = new Rect();

                var width = mat.Width;
                var height = mat.Height;
                int x = -1, y = -1, offxy = -1, roiWidth = -1;

                int centerx = width / 2;
                int centery = height / 2;
                if (width < height)
                {
                    offxy = width / 4;
                    x = centerx - offxy;
                    y = centery - offxy;
                    roiWidth = offxy * 2;
                }
                else
                {
                    offxy = height / 4;
                    x = centery - offxy;
                    y = centerx - offxy;
                }

                rect = new Rect(x, y, roiWidth, roiWidth);
                roi = new Mat(mat, rect);

                Logger.LogInformation($"IsQrImg获取切图范围,X:{rect.X},Y:{rect.Y},w:{rect.Width},h:{rect.Height}");

                SaveMatFile(roi, $"IsQrImg_范围图片");
                Logger.LogInformation("IsQrImg范围图获取成功");
                Mat gray = new Mat();
                List<ImgQrCodeContent> imgQrCodes;
                var isqrcode = DetectAndCode(roi, rect, out imgQrCodes);
                if (isqrcode)
                {
                    return SetResponse("1000", "IsQrImg识别成功", imgQrCodes);
                }
                else
                {
                    return SetResponse("1101", "IsQrImg识别失败,没有找到二维码"); ;
                }
            }
            catch (Exception ex)
            {
                return SetResponse("1199", $"IsQrImg识别出现异常:{ex.Message}"); ;
            }
        }

        public ImgRecognitionResponse SanCutImg(string base64)
        {
            try
            {
                Mat mat = Base64Expand.ToaMat(base64);
                if (mat is null)
                {
                    return SetResponse("1102", "IsQrImg图像为空");
                }

                SaveMatFile(mat, $"SanCutImg_接收原图");
                Rect rect = new Rect(1, 1, mat.Width - 1, mat.Height - 1);
                List<ImgQrCodeContent> imgQrCodes;
                var isqrcode = DetectAndCode(mat, rect, out imgQrCodes);
                if (isqrcode)
                {
                    return SetResponse("1000", "SanCutImg识别成功", imgQrCodes);
                }
                else
                {
                    return SetResponse("1101", "SanCutImg识别失败,没有找到二维码"); ;
                }
            }
            catch (Exception ex)
            {
                return SetResponse("1199", $"SanCutImg识别出现异常:{ex.Message}"); ;
            }
        }

        public bool DetectAndCode(Mat mat, Rect rect, out List<ImgQrCodeContent> imgQrCodeContents)
        {
            Logger.LogInformation("IsQrImg开始解析图片中的二维码内容");
            var wechatQrcode = WeChatQRCode.Create(_wechat_QCODE_detector_prototxt_path, _wechat_QCODE_detector_caffe_model_path,
                                                         _wechat_QCODE_super_resolution_prototxt_path, _wechat_QCODE_super_resolution_caffe_model_path);

            string[] texts;
            Mat[] rects;
            wechatQrcode.DetectAndDecode(mat, out rects, out texts);
            double minrect = 1.0;
            imgQrCodeContents = new List<ImgQrCodeContent>(50);
            if (texts.Length > 0)
            {
                // List<ImgQrCodeContent> imgQrCodes = new List<ImgQrCodeContent>(50);
                for (int i = 0; i < rects.Length; i++)
                {
                    ImgQrCodeContent imgQrCode = new ImgQrCodeContent();
                    Mat ss = rects[i];
                    Point pt1 = new Point((int)ss.At<float>(0, 0), (int)ss.At<float>(0, 1));
                    Point pt2 = new Point((int)ss.At<float>(1, 0), (int)ss.At<float>(1, 1));
                    Point pt3 = new Point((int)ss.At<float>(2, 0), (int)ss.At<float>(2, 1));
                    Point pt4 = new Point((int)ss.At<float>(3, 0), (int)ss.At<float>(3, 1));

                    Point[] points = new Point[] { pt1, pt2, pt3, pt4 };
                    for (int index = 0; index < points.Length; index++)
                    {
                        mat.Line(points[index], points[(index + 1) % 4], Scalar.Red, 3);
                    }
                    SaveMatFile(mat, $"DetectAndCode_二维码位置");
                    //mat.SaveImage(@"C:\Users\q4528\Desktop\测试数据\定位\roi.jpg");
                    RotatedRect rotated = Cv2.MinAreaRect(points);
                    minrect = Math.Min(rotated.Size.Width, rotated.Size.Height);// rotated.Size.Width;
                    imgQrCodeContents.Add(new ImgQrCodeContent()
                    {
                        QrCodeContent = texts[i],
                        QrCodeWidth = (int)minrect,
                        X = rect.X,
                        Y = rect.Y,
                        Width = rect.Width,
                        Height = rect.Height,
                        CenterX = rect.X + rotated.Center.ToPoint().X,
                        CenterY = rect.Y + rotated.Center.ToPoint().Y,
                    });
                }
                if (minrect / mat.Width > 0.20)
                {
                    return true;
                }
                else
                {
                    Logger.LogInformation("IsQrImg请靠近标签拍摄清晰图片");
                    return false;
                }
            }
            else
            {
                Logger.LogInformation("IsQrImg请吧二维码对准扫描框");
                return false;
            }
        }

        private ImgRecognitionResponse SetResponse(string state, string message, List<ImgQrCodeContent>? imgQrCodeContents = null, string context = "")
        {
            if (state == "1199")
            {
                Logger.LogError(message);
            }
            else
            {
                Logger.LogInformation(message);
            }
            ImgRecognitionResponse response = new ImgRecognitionResponse();
            response.imgRecognitionState = state;
            response.imgRecognitionMessage = message;
            if (context is not "")
            {
                response.cutedImgContent = context;
            }
            if (imgQrCodeContents != null)
            {
                response.fullAntiFakeCodes.AddRange(imgQrCodeContents);
            }

            return response;
        }

        private Task SaveMatFile(Mat array, string name)
        {
            var filepathw = Path.Combine(Environment.ContentRootPath, $"testdata");
            if (!Directory.Exists(filepathw))
            {
                Directory.CreateDirectory(filepathw);
            }

            var filepathw2 = Path.Combine(filepathw, $"{FileNmae}");

            if (!Directory.Exists(filepathw2))
            {
                Directory.CreateDirectory(filepathw2);
            }

            var filepath = Path.Combine(filepathw2, $"{FileNmaeIdnex}_{name}.jpg");
            // Mat image = array.GetMat();

            //ImageEncodingParam param = new ImageEncodingParam(ImageEncodingFlags.);
            array.SaveImage(filepath);
            return Task.CompletedTask;
        }
    }
}