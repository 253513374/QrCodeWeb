using OpenCvSharp;
using QrCodeWeb.Datas;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
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
        private readonly Uititys Uititys;

        public CutImageService(IWebHostEnvironment environment, ILogger<DeCodeService> logger, Uititys uititys)
        {
            Environment = environment;
            Logger = logger;
            Uititys = uititys;
        }

        /// <summary>
        /// 正式切图进入人工客服
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public ImgRecognitionResponse CutImage(ImgQrCodeContent model)
        {
            try
            {
              
                if(model is null) return SetResponse("1103", "传入图像为空", "CutImage");
                if (model.CutedImgBase64Content.Length < 10)
                {
                    return SetResponse("1103", "传入图像为空", "CutImage");
                }
                using Mat mat = Uititys.ToaMat(model.CutedImgBase64Content);

                Uititys.SaveMatFile(mat, $"CutImage_接收需要裁图的原图");



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

                Mat image =  ImageProcessing(qr_roi);
                List<RectPoints>  rectPoints =  GetPatternsPoints(image, model);
                string base64 = "";
                if (rectPoints.Count == 3)
                {
                    Point[] points = GetRoiPoint(rectPoints, 2, qr_rect);
                    Mat wrapmat = GetMatWarpPerspective(qr_roi, points);
                    base64 = $"data:image/jpeg;base64,{Uititys.ToBase64(wrapmat)}";
                }
                else
                {
                    base64 = $"data:image/jpeg;base64,{Uititys.ToBase64(qr_roi)}";
                }

                Uititys.SaveMatFile(qr_roi, $"CutImage_裁剪成功的图片");
                if (qr_roi.Empty())
                {
                    return SetResponse("1101", "图片切图失败", "CutImage");
                }
                else
                {
                    return SetResponse("1000", $"图片切图成功", $"CutImage,W:{qr_roi.Width} - H:{qr_roi.Height}", null, base64);
                }
            }
            catch (Exception ex)
            {
                return SetResponse("1199", $"图片切图异常", $"CutImage:{ex.Message}");
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

            Uititys.SaveMatFile(mat, $"IsQrImg_接收原图");

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
                    roiWidth = offxy * 2;
                }
                rect = new Rect(x, y, roiWidth, roiWidth);
                roi = new Mat(mat, rect);
                Logger.LogInformation($"IsQrImg获取切图范围,X:{rect.X},Y:{rect.Y},w:{rect.Width},h:{rect.Height}");

                Uititys.SaveMatFile(mat, $"IsQrImg_范围图片");
                Logger.LogInformation("IsQrImg范围图获取成功");
                Mat gray = new Mat();
                List<ImgQrCodeContent> imgQrCodes;
                var isqrcode = DetectAndCode(mat, rect, out imgQrCodes);
                switch (isqrcode)
                {
                    case ScanImgState.Succeed:
                        return SetResponse("1000", "识别成功", "IsQrImg", imgQrCodes);

                    case ScanImgState.Fail:

                        Uititys.SaveMatFile(roi, $"识别失败{Guid.NewGuid().ToString()}");
                        return SetResponse("1101", "识别失败,没有找到二维码", "IsQrImg");

                    case ScanImgState.TooFar:
                        return SetResponse("1102", "二维码太小，请将手机靠近二维码", "IsQrImg");
                    case ScanImgState.MultiCode:
                        return SetResponse("1104", "请不要扫描多个二维码", "IsQrImg");
                    case ScanImgState.NotCode:
                        return SetResponse("1105", "请对准标签上的二维码和防伪线拍摄", "IsQrImg");

                    default:
                        return SetResponse("1100", "未知状态", "IsQrImg");
                }
            }
            catch (Exception ex)
            {
                return SetResponse("1199", $"识别出现异常", $"IsQrImg:{ex.Message}");
            }
        }



        public ImgRecognitionResponse SanCutImg(string base64)
        {
            try
            {
                Mat mat = Base64Expand.ToaMat(base64);
                if (mat is null)
                {
                    return SetResponse("1103", "IsQrImg图像为空", "SanCutImg");
                }

                Uititys.SaveMatFile(mat, $"SanCutImg_接收原图");
                Rect rect = new Rect(1, 1, mat.Width - 1, mat.Height - 1);
                List<ImgQrCodeContent> imgQrCodes;
                var isqrcode = DetectAndCode(mat, rect, out imgQrCodes);
                switch (isqrcode)
                {
                    case ScanImgState.Succeed:
                        return SetResponse("1000", "识别成功", "SanCutImg", imgQrCodes);
                    case ScanImgState.Fail:
                        return SetResponse("1101", "识别失败,没有找到二维码", "SanCutImg");
                    case ScanImgState.TooFar:
                        return SetResponse("1102", "二维码太小,请将手机靠近二维码", "SanCutImg");
                    case ScanImgState.MultiCode:
                        return SetResponse("1104", "请勿拍摄多个二维码", "SanCutImg");
                    case ScanImgState.NotCode:
                        return SetResponse("1105", "请对准标签上的二维码和防伪线拍摄", "SanCutImg");
                    default:
                        return SetResponse("1100", "未知状态", "SanCutImg");
                }
            }
            catch (Exception ex)
            {
                return SetResponse("1199", $"识别出现异常", $"SanCutImg:{ex.Message}"); ;
            }
        }

        public ScanImgState DetectAndCode(Mat mat, Rect rect, out List<ImgQrCodeContent> imgQrCodeContents)
        {
            Logger.LogInformation("IsQrImg开始解析图片中的二维码内容");
            var wechatQrcode = WeChatQRCode.Create(_wechat_QCODE_detector_prototxt_path, _wechat_QCODE_detector_caffe_model_path,
                                                         _wechat_QCODE_super_resolution_prototxt_path, _wechat_QCODE_super_resolution_caffe_model_path);

            string[] texts;
            Mat[] rects;
            wechatQrcode.DetectAndDecode(mat, out rects, out texts);

         
            double minrect = 1.0;
            imgQrCodeContents = new List<ImgQrCodeContent>(50);

            if (texts.Length == 0)
            {
                return ScanImgState.NotCode;
            }

            if (texts.Length != 1)
            {
                return ScanImgState.MultiCode;
            }
          

            //懒得修改了
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
                    Uititys.SaveMatFile(mat, $"DetectAndCode_二维码位置");
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
                return ScanImgState.Succeed;
            }
            else
            {
                // Logger.LogInformation("IsQrImg请吧二维码对准扫描框");
                return ScanImgState.Fail;
            }
        }

        /// <summary>
        /// 返回Response，
        /// </summary>
        /// <param name="state"> 状态码</param>
        /// <param name="message">状态码信息</param>
        /// <param name="imgQrCodeContents">多个解析成功的二维码内容</param>
        /// <param name="context">切图成功的图像base64</param>
        /// <par
        /// <returns></returns>
        private ImgRecognitionResponse SetResponse(string state, string message, string msgsource = "", List<ImgQrCodeContent>? imgQrCodeContents = null, string context = "")
        {
            if (msgsource is not "")
            {
                msgsource = $"{msgsource}-{message}";
            }
            if (state == "1199")
            {
                Logger.LogError(msgsource);
            }
            else
            {
                Logger.LogInformation(msgsource);
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




        /// <summary>
        ///  根据定位点，获取需要透视变换的截图的坐标。 
        /// </summary>
        /// <param name="rectPoints">定位点坐标</param>
        /// <param name="roiscale">截图范围因子，范围最小1.5f，最大3.0f</param>
        /// <param name="rect">用来约束坐标范围，防止越界</param>
        /// <returns>返回顶点坐标</returns>
        Point[] GetRoiPoint(List<RectPoints> rectPoints, float roiscale = 2.0f, Rect? rect = null)
        {

            if (roiscale > 3.0f)
            {
                roiscale = 3.0f;
            }
            if (roiscale < 1.5f)
            {
                roiscale = 1.5f;
            }
            //Log.Information($"计算二维码边界坐标顺序：开始");
            //var width = rectPoints.Max(x => x.MarkPoints[0].X);
            int a = 0, b = 1, c = 2, d = 3;
            //计算三个定位点的距离
            int AB = (int)Point.Distance(rectPoints[a].CenterPoints, rectPoints[b].CenterPoints); //(int)Distance(rectPoints[a].CenterPoints, rectPoints[b].CenterPoints);
            int BC = (int)Point.Distance(rectPoints[b].CenterPoints, rectPoints[c].CenterPoints);
            int AC = (int)Point.Distance(rectPoints[c].CenterPoints, rectPoints[a].CenterPoints);
            //计算二维码的中心坐标,计算二维码定位点之间最长向量，二维码的中点为 最长向量的中心坐标。
            var max = Math.Max(AB, Math.Max(BC, AC));

            //var ab = Point.Distance(rectPoints[a].CenterPoints, rectPoints[b].CenterPoints);
            //var bc = Point.Distance(rectPoints[b].CenterPoints, rectPoints[c].CenterPoints);
            //var ac = Point.Distance(rectPoints[c].CenterPoints, rectPoints[a].CenterPoints);

            //外扩长度偏移量
            int offlength = max / 2;

            Point topPoint = new Point();
            Point2f QRMatCenterPoint = new Point();

            //顶点位置
            int selecttop = 0;
            if (max == AB)
            {
                topPoint = rectPoints[c].CenterPoints;//二维码直角顶点
                selecttop = 2;
                //计算二维码的中心坐标
                int X = (rectPoints[a].CenterPoints.X + rectPoints[b].CenterPoints.X) / 2;
                int Y = (rectPoints[a].CenterPoints.Y + rectPoints[b].CenterPoints.Y) / 2;
                QRMatCenterPoint.X = X;
                QRMatCenterPoint.Y = Y;
            }
            else if (max == BC)
            {
                selecttop = 0;
                topPoint = rectPoints[a].CenterPoints;
                int X = (rectPoints[b].CenterPoints.X + rectPoints[c].CenterPoints.X) / 2;
                int Y = (rectPoints[b].CenterPoints.Y + rectPoints[c].CenterPoints.Y) / 2;
                QRMatCenterPoint.X = X;
                QRMatCenterPoint.Y = Y;
            }
            else if (max == AC)
            {
                selecttop = 1;
                topPoint = rectPoints[b].CenterPoints;
                int X = (rectPoints[a].CenterPoints.X + rectPoints[c].CenterPoints.X) / 2;
                int Y = (rectPoints[a].CenterPoints.Y + rectPoints[c].CenterPoints.Y) / 2;

                QRMatCenterPoint.X = X;
                QRMatCenterPoint.Y = Y;
            }
            int RotationAngle = -1;

            Point[] QRrect = new Point[4];

            //根号二维码顶点所在象限，计算二维码矩形的四个点
            Point markPosotion = new Point();
            if (QRMatCenterPoint.X > topPoint.X && QRMatCenterPoint.Y > topPoint.Y)
            {
                #region 第一象限
                float offx = Math.Abs(rectPoints[selecttop].CenterPoints.X - QRMatCenterPoint.X) * roiscale;
                float offy = Math.Abs(rectPoints[selecttop].CenterPoints.Y - QRMatCenterPoint.Y) * roiscale;
                float x1 = QRMatCenterPoint.X - offx;
                float y1 = QRMatCenterPoint.Y - offy;
                QRrect[0] = new Point(x1, y1);
                Math.Max(x1, y1);
                float x2 = (QRMatCenterPoint.X + offx) > 0 ? QRMatCenterPoint.X + offx : 0;
                float y2 = (QRMatCenterPoint.Y + offy) > 0 ? QRMatCenterPoint.Y + offy : 0;
                QRrect[2] = new Point(x2, y2);
                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].CenterPoints;
                        float voffx = Math.Abs(marks.X - QRMatCenterPoint.X) * roiscale;
                        float voffy = Math.Abs(marks.Y - QRMatCenterPoint.Y) * roiscale;

                        if (marks.Y > QRMatCenterPoint.Y && marks.X < QRMatCenterPoint.X)
                        {
                            float x3 = (QRMatCenterPoint.X - voffx);
                            float y3 = (QRMatCenterPoint.Y + voffy);
                            QRrect[1] = new Point(x3, y3);
                        }
                        else
                        {
                            float x4 = (QRMatCenterPoint.X + voffx);
                            float y4 = (QRMatCenterPoint.Y - voffy);
                            QRrect[3] = new Point(x4, y4);
                        }

                    }
                }
                #endregion
                RotationAngle = 0;
            }
            else if (QRMatCenterPoint.X < topPoint.X && QRMatCenterPoint.Y > topPoint.Y)
            {
                #region 第二象限
                float offx = Math.Abs(rectPoints[selecttop].CenterPoints.X - QRMatCenterPoint.X) * roiscale;
                float offy = Math.Abs(rectPoints[selecttop].CenterPoints.Y - QRMatCenterPoint.Y) * roiscale;
                float x1 = QRMatCenterPoint.X + offx;
                float y1 = QRMatCenterPoint.Y + offy;
                QRrect[0] = new Point(x1, y1);
                Math.Max(x1, y1);
                float x2 = (QRMatCenterPoint.X - offx) > 0 ? QRMatCenterPoint.X - offx : 0;
                float y2 = (QRMatCenterPoint.Y + offy) > 0 ? QRMatCenterPoint.Y + offy : 0;
                QRrect[2] = new Point(x2, y2);

                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].CenterPoints;
                        float voffx = Math.Abs(marks.X - QRMatCenterPoint.X) * roiscale;
                        float voffy = Math.Abs(marks.Y - QRMatCenterPoint.Y) * roiscale;

                        if (marks.Y < QRMatCenterPoint.Y && marks.X < QRMatCenterPoint.X)
                        {
                            float x3 = (QRMatCenterPoint.X - voffx);
                            float y3 = (QRMatCenterPoint.Y - voffy);
                            QRrect[1] = new Point(x3, y3);
                        }
                        else
                        {
                            float x4 = (QRMatCenterPoint.X + voffx);
                            float y4 = (QRMatCenterPoint.Y + voffy);
                            QRrect[3] = new Point(x4, y4);
                        }

                    }
                }
                #endregion

                RotationAngle = 90;
            }
            else if (QRMatCenterPoint.X < topPoint.X && QRMatCenterPoint.Y < topPoint.Y)
            {
                #region 第三象限
                //int offset = (int)Distance(QRMatCenterPoint, rectPoints[selecttop].CenterPoints);
                float offx = Math.Abs(rectPoints[selecttop].CenterPoints.X - QRMatCenterPoint.X) * roiscale;
                float offy = Math.Abs(rectPoints[selecttop].CenterPoints.Y - QRMatCenterPoint.Y) * roiscale;
                float x1 = QRMatCenterPoint.X + offx;
                float y1 = QRMatCenterPoint.Y + offy;
                QRrect[0] = new Point(x1, y1);
                Math.Max(x1, y1);
                float x2 = (QRMatCenterPoint.X - offx) > 0 ? QRMatCenterPoint.X - offx : 0;
                float y2 = (QRMatCenterPoint.Y - offy) > 0 ? QRMatCenterPoint.Y - offy : 0;
                QRrect[2] = new Point(x2, y2);

                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].CenterPoints;
                        float voffx = Math.Abs(marks.X - QRMatCenterPoint.X) * roiscale;
                        float voffy = Math.Abs(marks.Y - QRMatCenterPoint.Y) * roiscale;

                        if (marks.Y < QRMatCenterPoint.Y && marks.X > QRMatCenterPoint.X)
                        {
                            float x3 = (QRMatCenterPoint.X + voffx);
                            float y3 = (QRMatCenterPoint.Y - voffy);
                            QRrect[1] = new Point(x3, y3);
                        }
                        else
                        {
                            float x4 = (QRMatCenterPoint.X - voffx);
                            float y4 = (QRMatCenterPoint.Y + voffy);
                            QRrect[3] = new Point(x4, y4);
                        }

                    }
                }
                #endregion

                RotationAngle = 180;
            }
            else if (QRMatCenterPoint.X > topPoint.X && QRMatCenterPoint.Y < topPoint.Y)
            {
                #region 第四象限
                float offx = Math.Abs(rectPoints[selecttop].CenterPoints.X - QRMatCenterPoint.X) * roiscale;
                float offy = Math.Abs(rectPoints[selecttop].CenterPoints.Y - QRMatCenterPoint.Y) * roiscale;
                float x1 = QRMatCenterPoint.X - offx;
                float y1 = QRMatCenterPoint.Y + offy;
                QRrect[0] = new Point(x1, y1);
                Math.Max(x1, y1);
                float x2 = (QRMatCenterPoint.X + offx) > 0 ? QRMatCenterPoint.X + offx : 0;
                float y2 = (QRMatCenterPoint.Y + offy) > 0 ? QRMatCenterPoint.Y + offy : 0;
                QRrect[2] = new Point(x2, y2);


                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].CenterPoints;
                        float voffx = Math.Abs(marks.X - QRMatCenterPoint.X) * roiscale;
                        float voffy = Math.Abs(marks.Y - QRMatCenterPoint.Y) * roiscale;

                        if (marks.Y < QRMatCenterPoint.Y && marks.X > QRMatCenterPoint.X)
                        {
                            float x3 = (QRMatCenterPoint.X + voffx);
                            float y3 = (QRMatCenterPoint.Y + voffy);
                            QRrect[1] = new Point(x3, y3);
                        }
                        else
                        {
                            float x4 = (QRMatCenterPoint.X - voffx);
                            float y4 = (QRMatCenterPoint.Y - voffy);
                            QRrect[3] = new Point(x4, y4);
                        }
                    }
                }
                #endregion
                RotationAngle = -90;
            }
            return QRrect;
        }


        /// <summary>
        /// 图像轮廓查找，找到二维码三个定位点坐标。
        /// </summary>
        /// <param name="dilatemat">输入的原图像</param>
        /// <returns>返回定位点坐标</returns>
        List<RectPoints> GetPatternsPoints(Mat dilatemat, ImgQrCodeContent codeContent)
        {
            try
            {
                List<RectPoints> rectPoints = new List<RectPoints>();
                Point[][] matcontours;
                HierarchyIndex[] hierarchy;
                ///算出二维码轮廓
                //dilatemat = dilatemat.Canny(100, 100);

                //Uititys.SaveMatFile(dilatemat, "FindContours");
                Cv2.FindContours(dilatemat, out matcontours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

                using Mat drawingmark = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
                using Mat drawingmarkminAreaSzie = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
                using Mat drawingAllmark = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
                using Mat drawingAllContours = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);

                using Mat drawing = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
                using Mat drawingf = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
                // 轮廓圈套层数
                rectPoints = new List<RectPoints>();

                Int64 maxarea = (((codeContent.Width+codeContent.Height)/2) * ((codeContent.Width + codeContent.Height) / 2))/ 4;

                //通过黑色定位角作为父轮廓，有两个子轮廓的特点，筛选出三个定位角
                int ic = 0;
                int parentIdx = -1;
                for (int i = 0; i < matcontours.Length; i++)
                {
                    var area2 = (int)Cv2.ContourArea(matcontours[i], false);
                    if (area2 <= 300 || area2 >= maxarea) { continue; }

                    int k = i;
                    int c = 0;
                    while (hierarchy[k].Child != -1) { k = hierarchy[k].Child; c++; }
                    if (hierarchy[k].Child != -1) { c++; }
                    if (c == 5)
                    {
                        var points2 = Cv2.ApproxPolyDP(matcontours[i], 0.02 * Cv2.ArcLength(matcontours[i].ToArray(), true), true);//
                        if (points2.Length == 4)
                        {
                            RotatedRect rotated = Cv2.MinAreaRect(points2);
                            var w = rotated.Size.Width;
                            var h = rotated.Size.Height;
                            if (Math.Min(w, h) / Math.Max(w, h) > 0.6)
                            {
                                var rects = new RectPoints()
                                {
                                    CenterPoints = rotated.Center.ToPoint(),
                                    MarkPoints = points2,
                                    Angle = rotated.Angle
                                };
                                //rectPoints.FindLast(x => x.CenterPoints == rects.CenterPoints).MarkPoints = points2;
                                rectPoints.Add(rects);
                                //画出三个黑色定位角的轮廓
                                Cv2.DrawContours(drawingmark, matcontours, i, new Scalar(0, 125, 255), 1, LineTypes.Link8);
                                Uititys.SaveMatFile(drawingmark, "三个定位点轮廓");
                            }
                          
                        }
                        Cv2.DrawContours(drawingAllContours, matcontours, i, new Scalar(255, 255, 255));
                        Uititys.SaveMatFile(drawingAllContours, "所有轮廓");
                    }

                }

                //SaveMatFile(drawingAllContours, "所有轮廓");
                //SaveMatFile(drawingmarkminAreaSzie, "三个定位点轮廓_小于指定面积");

                return rectPoints;
            }
            catch (Exception ee)
            {
                return new List<RectPoints>();
                // throw;
            }
        }


        /// <summary>
        /// 对图像进行透视变换，返回变换之后的图像。
        /// </summary>
        /// <param name="image">输入原图</param>
        /// <param name="points">输入变换坐标点</param>
        /// <returns></returns>
        private Mat GetMatWarpPerspective(Mat image,Point[] points)
        {
            RotatedRect rotated = Cv2.MinAreaRect(points);
            Point2f[] srcpoint = new Point2f[]
            {
                new Point2f(points[0].X,points[0].Y),
                new Point2f(points[1].X,points[1].Y),
                new Point2f(points[2].X,points[2].Y),
                new Point2f(points[3].X,points[3].Y)
            };
            //定义变换之后的二维码Size
            int sizew = Math.Max((int)rotated.Size.Width + 50, (int)rotated.Size.Height + 50);
            int boxw = 30;
            Rect rect = new Rect(0, 0, sizew, sizew);
            Point2f[] dstpoint = new Point2f[4];
            dstpoint[0] = new Point2f(boxw, boxw);
            dstpoint[1] = new Point2f(boxw, sizew - boxw);
            dstpoint[2] = new Point2f(sizew - boxw, sizew - boxw);
            dstpoint[3] = new Point2f(sizew - boxw, boxw);

            //对二值化的二维码区域进行透视变换，
            using Mat warpMatrix = Cv2.GetPerspectiveTransform(srcpoint, dstpoint);
            Mat dst = new Mat(rect.Size, MatType.CV_8UC3);
            Cv2.WarpPerspective(image, dst, warpMatrix, dst.Size(), InterpolationFlags.Linear, BorderTypes.Constant);

            Uititys.SaveMatFile(dst, "透视变换结果");
            return dst;
        }


        private Mat ImageProcessing(Mat image)
        {
            using Mat BGR2GRAY = new Mat();
            Cv2.CvtColor( image, BGR2GRAY, ColorConversionCodes.BGR2GRAY);
            using Mat AdaptiveThreshold = new Mat();
            Cv2.AdaptiveThreshold(BGR2GRAY, AdaptiveThreshold, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 101, 19);

            Mat Dilate = new Mat();
            Mat DilateElement = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.Dilate(AdaptiveThreshold, Dilate, DilateElement);
            Uititys.SaveMatFile(Dilate, "ThresholdDilate");

            Mat Erode = new Mat();
            Mat ErodeElement = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.Erode(Dilate, Erode, ErodeElement);
            Uititys.SaveMatFile(Erode, "ThresholdErode");


            Mat MorphologyEx = new Mat();
            Mat MorphologyExElement = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
            Cv2.MorphologyEx(Erode, MorphologyEx, MorphTypes.Gradient,MorphologyExElement);
            Uititys.SaveMatFile(MorphologyEx, "MorphologyEx");

            return MorphologyEx;
        }


    }
}